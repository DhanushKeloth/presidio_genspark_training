using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Repositories
{
    /// <summary>
    /// Handles all database operations for Shipment and its child entities
    /// (ShipmentAddress, ShipmentItem, ShipmentOtpWindow, ShipmentEvent).
    /// Inherits GetByIdAsync, AddAsync, UpdateAsync, SaveAsync from
    /// BaseRepository{Shipment}.
    /// </summary>
    public class ShipmentRepository : BaseRepository<Shipment>, IShipmentRepository
    {
        public ShipmentRepository(AppDbContext ctx) : base(ctx) { }

        /// <summary>
        /// Loads the shipment with all child collections needed by
        /// detail pages and OTP operations. Using .Include() here ensures
        /// the service layer never triggers lazy-load N+1 queries.
        /// </summary>
        public async Task<Shipment?> GetByIdWithAddressesAsync(int id)
        {
            return await _ctx.Shipments
                .AsSplitQuery()
                .Include(s => s.ShipmentAddresses)
                .Include(s => s.ShipmentItems)
                .Include(s => s.ShipmentOtpWindows)
                .Include(s => s.Customer)
                .Include(s => s.Driver)
                    // Simplified: EF Core automatically handles null drivers in LEFT JOINs
                    .ThenInclude(d => d.DriverProfileUser) 
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        /// <summary>
        /// Acquires a PostgreSQL row-level lock (SELECT ... FOR UPDATE)
        /// on the shipment row within the provided transaction.
        ///
        /// Why raw SQL: EF Core does not natively support FOR UPDATE on a
        /// single entity. We use FromSqlRaw scoped to the active transaction
        /// so the lock is held until the transaction commits or rolls back.
        ///
        /// This is the only place in the codebase that uses raw SQL for
        /// a read — every other query uses LINQ.
        ///
        /// Called only by ShipmentService.AssignDriverAsync.
        /// </summary>
        public async Task<Shipment?> GetByIdWithLockAsync(int id, IDbContextTransaction transaction)
        {
            // Ensure EF Core uses the same connection and transaction
            _ctx.Database.UseTransaction(transaction.GetDbTransaction());

            return await _ctx.Shipments
                .FromSqlRaw("SELECT * FROM shipments WHERE id = {0} FOR UPDATE", id)
                .Include(s => s.ShipmentAddresses)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Lightweight lookup for the public /api/track/{trackingNumber} endpoint.
        /// No navigation includes — the public DTO only needs the shipment row
        /// plus address strings, which are joined in the vw_shipment_public_tracking
        /// view queried separately.
        /// </summary>
        public async Task<Shipment?> GetShipmentByTrackingNumberAsync(string trackingNumber)
        {
            return await _ctx.Shipments
                .AsNoTracking()
                .Include(s => s.ShipmentEvents.OrderByDescending(e => e.OccurredAt))
                .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber);
        }

        /// <summary>
        /// Returns all InTransit shipments with the assigned driver's
        /// DriverProfile included so GpsSimulationService can read
        /// current_lat/lng and the pickup/dropoff coordinates from
        /// ShipmentAddresses.
        ///
        /// Performance note: this runs every 5 seconds. The partial index
        /// idx_shipments_in_transit (WHERE status = 'InTransit') in PostgreSQL
        /// keeps this query fast even as the shipments table grows.
        /// AsNoTracking() used — the GPS service does not modify shipments,
        /// only DriverProfile coordinates via a targeted ExecuteUpdateAsync.
        /// </summary>
        public async Task<List<Shipment>> GetInTransitShipmentsAsync()
        {
            return await _ctx.Shipments
                .AsNoTracking()
                .Include(s => s.ShipmentAddresses)
                .Include(s => s.Driver)
                    // Simplified: EF Core automatically handles null drivers in LEFT JOINs
                    .ThenInclude(d => d.DriverProfileUser)
                .Where(s => s.Status == ShipmentStatus.InTransit)
                .ToListAsync();
        }

        /// <summary>
        /// Checks uniqueness of a generated TrackingNumber before the booking
        /// transaction commits. Called in a retry loop — if a collision is found
        /// (extremely unlikely but possible) the service generates a new number
        /// and tries again.
        /// </summary>
        public async Task<bool> IsTrackingNumberUniqueAsync(string trackingNumber)
        {
            return !await _ctx.Shipments.AnyAsync(s => s.TrackingNumber == trackingNumber);
        }

        /// <summary>
        /// Idempotency guard for the booking endpoint (FR1.4).
        /// Returns true if the same customer submitted an identical
        /// pickup + dropoff address pair within the last 60 seconds.
        /// Prevents accidental double-bookings from network retries.
        /// </summary>
        public async Task<bool> IsDuplicateBookingAsync(int customerId, string pickupAddress, string dropoffAddress)
        {
            var threshold = DateTime.UtcNow.AddSeconds(-60);

            return await _ctx.Shipments
                .AsNoTracking()
                .Where(s => s.CustomerId == customerId && s.CreatedAt >= threshold)
                .AnyAsync(s =>
                    s.ShipmentAddresses.Any(a => 
                        a.AddressType == AddressType.Pickup && a.AddressLine == pickupAddress) &&
                    s.ShipmentAddresses.Any(a => 
                        a.AddressType == AddressType.Dropoff && a.AddressLine == dropoffAddress));
        }

        /// <summary>
        /// Inserts one ShipmentEvent row into the append-only audit log.
        ///
        /// IMPORTANT: This must always be called within the same unit of work
        /// (before SaveAsync) as the corresponding shipment status update.
        /// The event row and the status change must be committed atomically —
        /// a status update with no corresponding event is an incomplete record.
        /// </summary>
        public async Task AddEventAsync(ShipmentEvent evt)
        {
            await _ctx.Set<ShipmentEvent>().AddAsync(evt);
        }

        /// <summary>
        /// Upserts a ShipmentOtpWindow row using PostgreSQL's
        /// INSERT ... ON CONFLICT ... DO UPDATE syntax.
        ///
        /// Why raw SQL: EF Core's SaveChanges does not support upsert on
        /// a non-PK unique constraint (shipment_id, otp_type). We need the
        /// database to atomically insert-or-update in one round trip.
        ///
        /// On insert:  creates the window row with the new OTP code.
        /// On conflict: updates otp_code, expires_at, attempt_count, generated_at,
        ///              and resets verified_at to NULL (regeneration resets the window).
        ///
        /// The otp_code is only NULL after successful verification —
        /// UpsertOtpWindowAsync is never called after verification.
        /// </summary>
        public async Task UpsertOtpWindowAsync(ShipmentOtpWindow window)
        {
            var sql = @"
                INSERT INTO shipment_otp_windows
                    (shipment_id, otp_type, otp_code, expires_at,
                     attempt_count, generated_at, verified_at)
                VALUES
                    ({0}, {1}::otp_type, {2}, {3}, {4}, {5}, {6})
                ON CONFLICT (shipment_id, otp_type)
                DO UPDATE SET
                    otp_code      = EXCLUDED.otp_code,
                    expires_at    = EXCLUDED.expires_at,
                    attempt_count = EXCLUDED.attempt_count,
                    generated_at  = EXCLUDED.generated_at,
                    verified_at   = EXCLUDED.verified_at;";

            await _ctx.Database.ExecuteSqlRawAsync(sql,
                window.ShipmentId,
                window.OtpType.ToString(), // Cast to the PostgreSQL string Enum type
                window.OtpCode,
                window.ExpiresAt,
                window.AttemptCount,
                window.GeneratedAt,
                window.VerifiedAt
            );
        }
        public async Task<bool> HasActiveShipmentAsync(int driverId)
        {
            var activeStatuses = new[]
            {
                ShipmentStatus.Assigned,
                ShipmentStatus.PickedUp,
                ShipmentStatus.InTransit,
                ShipmentStatus.Arrived     // ← driver is at the door, still on the job
            };

            return await _ctx.Shipments.AnyAsync(s =>
                s.DriverId == driverId &&
                activeStatuses.Contains(s.Status));
        }
    }
}