// using Microsoft.EntityFrameworkCore;
// using ShipmentTrackingAPI.DTOs.Common;
// using ShipmentTrackingAPI.DTOs.Shipment;
// using ShipmentTrackingAPI.Models;
// using ShipmentTrackingAPI.Models.Enums;
// using ShipmentTrackingAPI.Repositories.RepoInterfaces; // Assuming this is your Repo interface namespace
// using ShipmentTrackingAPI.Data;
// using ShipmentTrackingAPI.Interfaces;

// namespace ShipmentTrackingAPI.Services
// {
//     public class ShipmentService : IShipmentService
//     {
//         private readonly IShipmentRepository _repo;
//         private readonly AppDbContext _ctx; // Required for explicit transaction control

//         public ShipmentService(IShipmentRepository repo, AppDbContext ctx)
//         {
//             _repo = repo;
//             _ctx = ctx;
//         }

//         #region 1. CUSTOMER ACTIONS

//         public async Task<ShipmentDto> BookShipmentAsync(int customerId, BookShipmentRequestDto req)
//         {
//             // 1. Idempotency Check (Prevents double-billing/booking)
//             if (await _repo.IsDuplicateBookingAsync(customerId, req.PickupAddress, req.DropoffAddress))
//             {
//                 throw new InvalidOperationException("A duplicate booking was detected. Please wait a moment before trying again.");
//             }

//             // 2. Generate Unique Tracking Number
//             // 2. Generate Unique Tracking Number
//             string trackingNumber;
//             do
//             {
//                 // Guid generates letxters A-F and numbers 0-9. 
//                 // Substring(0,6) grabs the first 6 characters.
//                 // Result looks exactly like: TRK-9F3B2A
//                 trackingNumber = $"TRK-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
//             }
//             while (!await _repo.IsTrackingNumberUniqueAsync(trackingNumber));

//             // 3. Begin Transaction to ensure all or nothing
//             // 3. Begin Transaction to ensure all or nothing
//             using var tx = await _ctx.Database.BeginTransactionAsync();
//             try
//             {
//                 // 1. Core Shipment is now much leaner!
//                 var shipment = new Shipment
//                 {
//                     CustomerId = customerId,
//                     TrackingNumber = trackingNumber,
//                     Status = ShipmentStatus.Pending,
//                     CreatedAt = DateTime.UtcNow,
//                     UpdatedAt = DateTime.UtcNow
//                 };

//                 await _repo.AddAsync(shipment);

//                 // 2. Map Addresses (Recipient Info goes to Dropoff!)
//                 var pickup = new ShipmentAddress
//                 {
//                     Shipment = shipment,
//                     AddressType = AddressType.Pickup,
//                     AddressLine = req.PickupAddress,
//                     Lat = req.PickupLat,
//                     Lng = req.PickupLng
//                     // If you track SenderName/Phone, it would go here
//                 };

//                 var dropoff = new ShipmentAddress
//                 {
//                     Shipment = shipment,
//                     AddressType = AddressType.Dropoff,
//                     AddressLine = req.DropoffAddress,
//                     Lat = req.DropoffLat,
//                     Lng = req.DropoffLng,

//                     // Assuming your ShipmentAddress model has properties like this:
//                     ContactName = req.RecipientName,
//                     ContactPhone = req.RecipientPhone
//                 };

//                 await _ctx.Set<ShipmentAddress>().AddRangeAsync(pickup, dropoff);

//                 // 3. Map Items (Weight belongs here!)
//                 var items = req.Items.Select(i => new ShipmentItem
//                 {
//                     Shipment = shipment,
//                     Description = i.Description,
//                     WeightKg = i.Weight,       // Weight per item
//                     Quantity = i.Quantity,
//                     HeightCm = i.Height,
//     WidthCm = i.Width,
//     LengthCm = i.Length
//                 }).ToList();

//                 await _ctx.Set<ShipmentItem>().AddRangeAsync(items);
//                 // Initial Event
//                 var initialEvent = new ShipmentEvent
//                 {
//                     Shipment = shipment,
//                     Status = ShipmentStatus.Pending,
//                     Description = "Shipment booked and pending driver assignment.",
//                     OccurredAt = DateTime.UtcNow
//                 };
//                 await _repo.AddEventAsync(initialEvent);

//                 await _ctx.SaveChangesAsync();
//                 await tx.CommitAsync();

//                 return new ShipmentDto
//                 {
//                     Id = shipment.Id,
//                     TrackingNumber = shipment.TrackingNumber,
//                     Status = shipment.Status,
//                     CreatedAt = shipment.CreatedAt
//                 };
//             }
//             catch
//             {
//                 await tx.RollbackAsync();
//                 throw;
//             }
//         }

//         public async Task<PaginatedResponse<ShipmentSummaryDto>> GetCustomerShipmentsAsync(int customerId, ShipmentQueryParams q)
//         {
//             var query = _ctx.Shipments
//                 .Include(s => s.ShipmentAddresses)
//                 .Where(s => s.CustomerId == customerId);

//             if (q.Status.HasValue)
//             {
//                 query = query.Where(s => s.Status == q.Status.Value);
//             }

//             var totalCount = await query.CountAsync();
//             var shipments = await query
//                 .OrderByDescending(s => s.CreatedAt)
//                 .Skip((q.Page - 1) * q.Size)
//                 .Take(q.Size)
//                 .ToListAsync();

//             var items = shipments.Select(s => new ShipmentSummaryDto
//             {
//                 Id = s.Id,
//                 TrackingNumber = s.TrackingNumber,
//                 Status = s.Status,
//                 CreatedAt = s.CreatedAt,
//                 PickupArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup)?.AddressLine ?? "N/A",
//                 DropoffArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff)?.AddressLine ?? "N/A"
//             }).ToList();

//             return new PaginatedResponse<ShipmentSummaryDto>
//             {
//                 Data = items,
//                 TotalCount = totalCount,
//                 Page = q.Page,
//                 Size = q.Size
//             };
//         }

//         public async Task<ShipmentDetailDto> GetShipmentByIdAsync(int id, int requesterId, UserRole role)
//         {
//             var shipment = await _repo.GetByIdWithAddressesAsync(id)
//                 ?? throw new KeyNotFoundException("Shipment not found.");

//             // Strict Authorization Bounds
//             if (role == UserRole.Customer && shipment.CustomerId != requesterId)
//                 throw new UnauthorizedAccessException("Access denied.");
//             if (role == UserRole.Driver && shipment.DriverId != requesterId)
//                 throw new UnauthorizedAccessException("Access denied.");

//             var pickup = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
//             var dropoff = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

//             return new ShipmentDetailDto
//             {
//                 Id = shipment.Id,
//                 TrackingNumber = shipment.TrackingNumber,
//                 Status = shipment.Status,
//                 CreatedAt = shipment.CreatedAt,
//                 UpdatedAt = shipment.UpdatedAt,
//                 CustomerName = shipment.Customer?.FullName ?? "Unknown",
//                 RecipientName = dropoff?.ContactName ?? "Unknown",
//                 RecipientPhone = dropoff?.ContactPhone ?? "Unknown",
//                 DriverId = shipment.DriverId,
//                 DriverName = shipment.Driver?.FullName,
//                 DriverPhone = shipment.Driver?.DriverProfileUser?.PhoneNumber,
//                 VehicleNumber = shipment.Driver?.DriverProfileUser?.VehicleNumber,

//                 PickupAddress = new ShipmentAddressDto
//                 {
//                     AddressLine1 = pickup?.AddressLine ?? "",
//                     Latitude = pickup?.Lat,
//                     Longitude = pickup?.Lng
//                 },
//                 DropoffAddress = new ShipmentAddressDto
//                 {
//                     AddressLine1 = dropoff?.AddressLine ?? "",
//                     Latitude = dropoff?.Lat,
//                     Longitude = dropoff?.Lng
//                 },

//                 Items = shipment.ShipmentItems.Select(i => new ShipmentItemDto
//                 {
//                     Description = i.Description,
//                     Weight = i.WeightKg,
//                     Quantity = i.Quantity
//                 }).ToList(),

//                 Events = shipment.ShipmentEvents.OrderByDescending(e => e.OccurredAt).Select(e => new ShipmentEventDto
//                 {
//                     Status = e.Status,
//                     Description = e.Description,
//                     Timestamp = e.OccurredAt
//                 }).ToList()
//             };
//         }

//         public async Task<ShipmentDto> CancelShipmentAsync(int shipmentId, int customerId)
//         {
//             var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

//             if (shipment.CustomerId != customerId)
//                 throw new UnauthorizedAccessException("You can only cancel your own shipments.");

//             if (shipment.Status != ShipmentStatus.Pending)
//                 throw new InvalidOperationException($"Cannot cancel shipment. Current status is {shipment.Status}.");

//             shipment.Status = ShipmentStatus.Cancelled;
//             shipment.UpdatedAt = DateTime.UtcNow;

//             await _repo.AddEventAsync(new ShipmentEvent
//             {
//                 ShipmentId = shipment.Id,
//                 Status = ShipmentStatus.Cancelled,
//                 Description = "Shipment cancelled by customer.",
//                 OccurredAt = DateTime.UtcNow
//             });

//             await _ctx.SaveChangesAsync();

//             return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
//         }

//         #endregion

//         #region 2. PUBLIC ACTIONS

//         public async Task<PublicTrackingDto> GetPublicTrackingAsync(string trackingNumber)
//         {
//             var shipment = await _repo.GetShipmentByTrackingNumberAsync(trackingNumber)
//                 ?? throw new KeyNotFoundException("Invalid tracking number.");

//             // Re-fetch events and addresses here because GetByTrackingNumberAsync was built to be ultra-lightweight.
//             var details = await _ctx.Shipments
//                 .AsNoTracking()
//                 .Include(s => s.ShipmentAddresses)
//                 .Include(s => s.ShipmentEvents)
//                 .Include(s => s.Driver).ThenInclude(d => d.DriverProfileUser)
//                 .FirstOrDefaultAsync(s => s.Id == shipment.Id);

//             var pickup = details!.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
//             var dropoff = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

//             return new PublicTrackingDto
//             {
//                 TrackingNumber = details.TrackingNumber,
//                 Status = details.Status,
//                 PickupAddress = pickup?.AddressLine ?? "N/A",
//                 DropoffAddress = dropoff?.AddressLine ?? "N/A",
//                 DriverLat = details.Driver?.DriverProfileUser?.CurrentLat,
//                 DriverLng = details.Driver?.DriverProfileUser?.CurrentLng,
//                 Events = details.ShipmentEvents.OrderByDescending(e => e.OccurredAt).Select(e => new ShipmentEventDto
//                 {
//                     Status = e.Status,
//                     Description = e.Description,
//                     Timestamp = e.OccurredAt
//                 }).ToList()
//             };
//         }

//         #endregion

//         #region 3. DRIVER ACTIONS

//         public async Task<PaginatedResponse<PendingJobDto>> GetPendingQueueAsync(int driverId, int page, int size)
//         {
//             var query = _ctx.Shipments
//                 .AsNoTracking()
//                 .Include(s => s.ShipmentAddresses)
//                 .Include(s => s.ShipmentItems)
//                 .Where(s => s.Status == ShipmentStatus.Pending && s.DriverId == null);

//             var totalCount = await query.CountAsync();
//             var shipments = await query
//                 .OrderBy(s => s.CreatedAt) // Oldest pending jobs first
//                 .Skip((page - 1) * size)
//                 .Take(size)
//                 .ToListAsync();

//             var items = shipments.Select(s => new PendingJobDto
//             {
//                 TrackingNumber = s.TrackingNumber,
//                 PickupArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup)?.AddressLine ?? "N/A",
//                 DropoffArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff)?.AddressLine ?? "N/A",
//                 TotalWeightKg = s.ShipmentItems.Sum(i => i.WeightKg),
//                 ItemCount = s.ShipmentItems.Sum(i => i.Quantity),
//                 CreatedAt = s.CreatedAt
//             }).ToList();

//             return new PaginatedResponse<PendingJobDto> { Data = items, TotalCount = totalCount, Page = page, Size = size };
//         }

//         public async Task<ShipmentDto> AssignDriverAsync(int shipmentId, int driverId)
//         {
//             using var tx = await _ctx.Database.BeginTransactionAsync();
//             try
//             {
//                 // This calls your raw SQL pessimistic lock! Driver B will wait here if Driver A is locking it.
//                 var shipment = await _repo.GetByIdWithLockAsync(shipmentId, tx)
//                     ?? throw new KeyNotFoundException();

//                 if (shipment.DriverId != null || shipment.Status != ShipmentStatus.Pending)
//                     throw new InvalidOperationException("This shipment is no longer available.");

//                 shipment.DriverId = driverId;
//                 shipment.Status = ShipmentStatus.Assigned;
//                 shipment.UpdatedAt = DateTime.UtcNow;

//                 await _repo.AddEventAsync(new ShipmentEvent
//                 {
//                     ShipmentId = shipment.Id,
//                     Status = ShipmentStatus.Assigned,
//                     Description = "Driver accepted the job.",
//                     OccurredAt = DateTime.UtcNow
//                 });

//                 await _ctx.SaveChangesAsync();
//                 await tx.CommitAsync();

//                 return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
//             }
//             catch
//             {
//                 await tx.RollbackAsync();
//                 throw;
//             }
//         }

//         public async Task<ShipmentDto> UpdateStatusAsync(int shipmentId, int driverId, ShipmentStatus newStatus)
//         {
//             var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

//             if (shipment.DriverId != driverId)
//                 throw new UnauthorizedAccessException("You can only update your own assigned shipments.");

//             // Simple State Machine validation to prevent skipping steps
//             if (newStatus <= shipment.Status)
//                 throw new InvalidOperationException($"Cannot revert or set status to {newStatus} from {shipment.Status}.");

//             shipment.Status = newStatus;
//             shipment.UpdatedAt = DateTime.UtcNow;

//             await _repo.AddEventAsync(new ShipmentEvent
//             {
//                 ShipmentId = shipment.Id,
//                 Status = newStatus,
//                 Description = $"Status updated to {newStatus}.",
//                 OccurredAt = DateTime.UtcNow
//             });

//             await _ctx.SaveChangesAsync();

//             return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
//         }

//         public async Task<ShipmentDto> FailDeliveryAsync(int shipmentId, int driverId, string reason)
//         {
//             var shipment = await _repo.GetByIdAsync(shipmentId) ?? throw new KeyNotFoundException();

//             if (shipment.DriverId != driverId)
//                 throw new UnauthorizedAccessException();

//             shipment.Status = ShipmentStatus.FailedDelivery;
//             shipment.UpdatedAt = DateTime.UtcNow;

//             await _repo.AddEventAsync(new ShipmentEvent
//             {
//                 ShipmentId = shipment.Id,
//                 Status = ShipmentStatus.FailedDelivery,
//                 Description = $"Delivery Failed: {reason}",
//                 OccurredAt = DateTime.UtcNow
//             });

//             await _ctx.SaveChangesAsync();

//             return new ShipmentDto { Id = shipment.Id, TrackingNumber = shipment.TrackingNumber, Status = shipment.Status, CreatedAt = shipment.CreatedAt };
//         }

//         #endregion
//     }
// }






using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Handles all shipment lifecycle operations:
///   Customer  — book, list, get detail, cancel
///   Public    — track by tracking number
///   Driver    — pending queue, self-assign, status update, fail delivery
///
/// FIXES APPLIED vs original version
/// ───────────────────────────────────
/// 1. ITrackingService injected — all status transitions now broadcast via SignalR.
/// 2. BookShipmentAsync — ShipmentItem now maps LengthCm, WidthCm, HeightCm
///    (required by DB CHECK constraints — missing caused constraint violation on insert).
/// 3. AssignDriverAsync — BroadcastStatusUpdateAsync called after commit.
/// 4. UpdateStatusAsync — explicit state machine transition map replaces
///    loose enum integer comparison. Terminal statuses rejected explicitly.
///    BroadcastStatusUpdateAsync + BroadcastDriverArrivedAsync called after save.
/// 5. FailDeliveryAsync — guard added (status must be Arrived).
///    BroadcastStatusUpdateAsync called after save.
/// 6. GetPublicTrackingAsync — collapsed from two DB queries into one.
/// </summary>
public class ShipmentService : IShipmentService
{
    private readonly IShipmentRepository _repo;
    private readonly AppDbContext        _ctx;
    private readonly ITrackingService    _tracking;

    public ShipmentService(
        IShipmentRepository repo,
        AppDbContext        ctx,
        ITrackingService    tracking)
    {
        _repo     = repo;
        _ctx      = ctx;
        _tracking = tracking;
    }

    // ── Valid driver status transitions (OTP transitions handled by OtpService) ──
    //
    // Only these two transitions come through UpdateStatusAsync:
    //   PickedUp  → InTransit  (driver starts driving)
    //   InTransit → Arrived    (driver reaches destination)
    //
    // Pending→Assigned  : AssignDriverAsync
    // Assigned→PickedUp : OtpService.VerifyOtpAsync (Pickup OTP)
    // Arrived→Delivered : OtpService.VerifyOtpAsync (Delivery OTP)
    // Arrived→FailedDelivery : FailDeliveryAsync
    // Any→Cancelled     : CancelShipmentAsync (Customer) or AdminService (Admin)
    private static readonly Dictionary<ShipmentStatus, ShipmentStatus> ValidDriverTransitions = new()
    {
        { ShipmentStatus.PickedUp,  ShipmentStatus.InTransit },
        { ShipmentStatus.InTransit, ShipmentStatus.Arrived   },
    };

    // ═══════════════════════════════════════════════════════════
    //  SECTION 1 — CUSTOMER ACTIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Books a new shipment. Creates:
    ///   - Shipment row (core contract)
    ///   - ShipmentAddress rows (Pickup + Dropoff in child table)
    ///   - ShipmentItem rows (one per item type)
    ///   - Initial ShipmentEvent ("Shipment booked")
    ///
    /// All four writes happen in one transaction — partial writes are unacceptable.
    /// </summary>
    public async Task<ShipmentDto> BookShipmentAsync(int customerId, BookShipmentRequestDto req)
    {
        // ── Idempotency guard ────────────────────────────────────
        // Prevents double-booking if the user double-taps the submit button.
        if (await _repo.IsDuplicateBookingAsync(customerId, req.PickupAddress, req.DropoffAddress))
            throw new InvalidOperationException(
                "A duplicate booking was detected. Please wait a moment before trying again.");

        // ── Generate unique tracking number ──────────────────────
        // Format: TRK-XXXXXX (6 uppercase alphanumeric chars)
        // Guid.NewGuid().ToString("N") produces only hex chars (0-9, A-F).
        // For a capstone this is fine — collision probability is negligible.
        string trackingNumber;
        do
        {
            trackingNumber = $"TRK-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        }
        while (!await _repo.IsTrackingNumberUniqueAsync(trackingNumber));

        // ── Transactional write ──────────────────────────────────
        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            // 1. Core shipment row
            var shipment = new Shipment
            {
                CustomerId     = customerId,
                TrackingNumber = trackingNumber,
                Status         = ShipmentStatus.Pending,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };
            await _repo.AddAsync(shipment);

            // 2. Address rows — Pickup and Dropoff in ShipmentAddresses child table.
            //    Sender identity comes from customer_id on Shipment (no contact fields on Pickup).
            //    Recipient contact details live on the Dropoff row only.
            var pickup = new ShipmentAddress
            {
                Shipment    = shipment,
                AddressType = AddressType.Pickup,
                AddressLine = req.PickupAddress,
                Lat         = req.PickupLat,
                Lng         = req.PickupLng,
                // ContactName and ContactPhone intentionally null on Pickup row
            };

            var dropoff = new ShipmentAddress
            {
                Shipment     = shipment,
                AddressType  = AddressType.Dropoff,
                AddressLine  = req.DropoffAddress,
                Lat          = req.DropoffLat,
                Lng          = req.DropoffLng,
                ContactName  = req.RecipientName,   // Required — DB CHECK enforces this
                ContactPhone = req.RecipientPhone,  // Required — DB CHECK enforces this
            };

            await _ctx.Set<ShipmentAddress>().AddRangeAsync(pickup, dropoff);

            // 3. Item rows — all dimension fields are required by DB CHECK constraints.
            //    Missing LengthCm/WidthCm/HeightCm causes constraint violation on insert.
            var items = req.Items.Select(i => new ShipmentItem
            {
                Shipment    = shipment,
                Description = i.Description,
                WeightKg    = i.Weight,
                LengthCm    = i.Length,   // ← was missing in original
                WidthCm     = i.Width,    // ← was missing in original
                HeightCm    = i.Height,   // ← was missing in original
                Quantity    = i.Quantity,
            }).ToList();

            await _ctx.Set<ShipmentItem>().AddRangeAsync(items);

            // 4. Initial audit event
            await _repo.AddEventAsync(new ShipmentEvent
            {
                Shipment    = shipment,
                Status      = ShipmentStatus.Pending,
                Description = "Shipment booked and pending driver assignment.",
                OccurredAt  = DateTime.UtcNow,
            });

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            return MapToShipmentDto(shipment);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Returns a paginated list of shipments owned by the customer.
    /// Supports optional ?status= filter.
    /// </summary>
    public async Task<PaginatedResponse<ShipmentSummaryDto>> GetCustomerShipmentsAsync(
        int customerId, ShipmentQueryParams q)
    {
        var query = _ctx.Shipments
            .Include(s => s.ShipmentAddresses)
            .Where(s => s.CustomerId == customerId);

        if (q.Status.HasValue)
            query = query.Where(s => s.Status == q.Status.Value);

        var totalCount = await query.CountAsync();

        var shipments = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((q.Page - 1) * q.Size)
            .Take(q.Size)
            .ToListAsync();

        var items = shipments.Select(s => new ShipmentSummaryDto
        {
            Id             = s.Id,
            TrackingNumber = s.TrackingNumber,
            Status         = s.Status,
            CreatedAt      = s.CreatedAt,
            PickupArea     = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Pickup)
                              ?.AddressLine ?? "N/A",
            DropoffArea    = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Dropoff)
                              ?.AddressLine ?? "N/A",
        }).ToList();

        return new PaginatedResponse<ShipmentSummaryDto>
        {
            Data       = items,
            TotalCount = totalCount,
            Page       = q.Page,
            Size       = q.Size,
        };
    }

    /// <summary>
    /// Returns full shipment detail including addresses, items, and event timeline.
    /// Enforces ownership: Customer sees only own shipments, Driver sees only assigned ones.
    /// Admin sees all (role check handled at controller level via [Authorize(Roles="Admin")]).
    /// </summary>
    public async Task<ShipmentDetailDto> GetShipmentByIdAsync(int id, int requesterId, UserRole role)
    {
        var shipment = await _repo.GetByIdWithAddressesAsync(id)
            ?? throw new KeyNotFoundException("Shipment not found.");

        if (role == UserRole.Customer && shipment.CustomerId != requesterId)
            throw new UnauthorizedAccessException("Access denied.");

        if (role == UserRole.Driver && shipment.DriverId != requesterId)
            throw new UnauthorizedAccessException("Access denied.");

        var pickup  = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
        var dropoff = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

        return new ShipmentDetailDto
        {
            Id             = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            Status         = shipment.Status,
            CreatedAt      = shipment.CreatedAt,
            UpdatedAt      = shipment.UpdatedAt,
            CustomerName   = shipment.Customer?.FullName ?? "Unknown",
            RecipientName  = dropoff?.ContactName        ?? "Unknown",
            RecipientPhone = dropoff?.ContactPhone        ?? "Unknown",
            DriverId       = shipment.DriverId,
            DriverName     = shipment.Driver?.FullName,
            DriverPhone    = shipment.Driver?.DriverProfileUser?.PhoneNumber,
            VehicleNumber  = shipment.Driver?.DriverProfileUser?.VehicleNumber,

            PickupAddress = new ShipmentAddressDto
            {
                AddressLine1 = pickup?.AddressLine ?? "",
                Latitude     = pickup?.Lat,
                Longitude    = pickup?.Lng,
            },
            DropoffAddress = new ShipmentAddressDto
            {
                AddressLine1 = dropoff?.AddressLine ?? "",
                Latitude     = dropoff?.Lat,
                Longitude    = dropoff?.Lng,
            },

            Items = shipment.ShipmentItems.Select(i => new ShipmentItemDto
            {
                Description = i.Description,
                Weight      = i.WeightKg,
                Quantity    = i.Quantity,
            }).ToList(),

            Events = shipment.ShipmentEvents
                .OrderBy(e => e.OccurredAt)
                .Select(e => new ShipmentEventDto
                {
                    Status      = e.Status,
                    Description = e.Description,
                    Timestamp   = e.OccurredAt,
                }).ToList(),
        };
    }

    /// <summary>
    /// Cancels a shipment. Only the owning Customer can cancel,
    /// and only while status = Pending (before a driver has claimed it).
    /// </summary>
    public async Task<ShipmentDto> CancelShipmentAsync(int shipmentId, int customerId)
    {
        var shipment = await _repo.GetByIdAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found.");

        if (shipment.CustomerId != customerId)
            throw new UnauthorizedAccessException("You can only cancel your own shipments.");

        if (shipment.Status != ShipmentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot cancel a shipment with status '{shipment.Status}'. " +
                "Only Pending shipments can be cancelled by the customer.");

        shipment.Status    = ShipmentStatus.Cancelled;
        shipment.UpdatedAt = DateTime.UtcNow;

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId  = shipment.Id,
            Status      = ShipmentStatus.Cancelled,
            Description = "Shipment cancelled by customer.",
            OccurredAt  = DateTime.UtcNow,
        });

        await _ctx.SaveChangesAsync();

        await _tracking.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.Cancelled.ToString(),
            "Shipment cancelled by customer.");

        return MapToShipmentDto(shipment);
    }

    // ═══════════════════════════════════════════════════════════
    //  SECTION 2 — PUBLIC ACTIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Public tracking endpoint — no auth required.
    /// Returns status, driver location (if InTransit), and event timeline.
    /// OTP codes, recipient phone, and internal IDs are never exposed here.
    ///
    /// FIX: was doing two DB round-trips (lightweight fetch then full fetch).
    /// Now collapses into a single query by tracking number.
    /// </summary>
    public async Task<PublicTrackingDto> GetPublicTrackingAsync(string trackingNumber)
    {
        // Single query — no double fetch
        var details = await _ctx.Shipments
            .AsNoTracking()
            .Include(s => s.ShipmentAddresses)
            .Include(s => s.ShipmentEvents)
            .Include(s => s.Driver).ThenInclude(d => d!.DriverProfileUser)
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber)
            ?? throw new KeyNotFoundException("Invalid tracking number.");

        var pickup  = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
        var dropoff = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

        return new PublicTrackingDto
        {
            TrackingNumber = details.TrackingNumber,
            Status         = details.Status,
            PickupAddress  = pickup?.AddressLine  ?? "N/A",
            DropoffAddress = dropoff?.AddressLine ?? "N/A",
            // Only expose driver location when actively in transit
            DriverLat      = details.Status == ShipmentStatus.InTransit
                                ? details.Driver?.DriverProfileUser?.CurrentLat
                                : null,
            DriverLng      = details.Status == ShipmentStatus.InTransit
                                ? details.Driver?.DriverProfileUser?.CurrentLng
                                : null,
            Events = details.ShipmentEvents
                .OrderBy(e => e.OccurredAt)
                .Select(e => new ShipmentEventDto
                {
                    Status      = e.Status,
                    Description = e.Description,
                    Timestamp   = e.OccurredAt,
                }).ToList(),
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  SECTION 3 — DRIVER ACTIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns paginated list of Pending, unassigned shipments for the job queue.
    /// Only shows public-safe fields — no customer name, no full contact details.
    /// </summary>
    public async Task<PaginatedResponse<PendingJobDto>> GetPendingQueueAsync(
        int driverId, int page, int size)
    {
        var query = _ctx.Shipments
            .AsNoTracking()
            .Include(s => s.ShipmentAddresses)
            .Include(s => s.ShipmentItems)
            .Where(s => s.Status == ShipmentStatus.Pending && s.DriverId == null);

        var totalCount = await query.CountAsync();

        var shipments = await query
            .OrderBy(s => s.CreatedAt)   // Oldest first — FIFO queue
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = shipments.Select(s => new PendingJobDto
        {
            Id            = s.Id,
            TrackingNumber = s.TrackingNumber,
            PickupArea     = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Pickup)
                              ?.AddressLine ?? "N/A",
            DropoffArea    = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Dropoff)
                              ?.AddressLine ?? "N/A",
            TotalWeightKg  = s.ShipmentItems.Sum(i => i.WeightKg * i.Quantity),
            ItemCount      = s.ShipmentItems.Sum(i => i.Quantity),
            CreatedAt      = s.CreatedAt,
        }).ToList();

        return new PaginatedResponse<PendingJobDto>
        {
            Data       = items,
            TotalCount = totalCount,
            Page       = page,
            Size       = size,
        };
    }

    /// <summary>
    /// Driver self-assigns a Pending shipment.
    ///
    /// Race condition protection: uses a pessimistic row-level lock via
    /// GetByIdWithLockAsync. If two drivers attempt simultaneously, the
    /// second transaction will see Status = Assigned and throw 409.
    ///
    /// SignalR: broadcasts StatusUpdated (Assigned) to all group members
    /// AFTER the transaction commits — never before.
    /// </summary>
    public async Task<ShipmentDto> AssignDriverAsync(int shipmentId, int driverId)
    {
        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var shipment = await _repo.GetByIdWithLockAsync(shipmentId, tx)
                ?? throw new KeyNotFoundException("Shipment not found.");

            if (shipment.DriverId != null || shipment.Status != ShipmentStatus.Pending)
                throw new InvalidOperationException(
                    "This shipment is no longer available. Another driver may have claimed it.");

            shipment.DriverId  = driverId;
            shipment.Status    = ShipmentStatus.Assigned;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId  = shipment.Id,
                Status      = ShipmentStatus.Assigned,
                Description = "Driver accepted the job.",
                ActorId     = driverId,
                OccurredAt  = DateTime.UtcNow,
            });

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            // Broadcast AFTER commit — client always receives committed state
            await _tracking.BroadcastStatusUpdateAsync(
                shipment.TrackingNumber,
                ShipmentStatus.Assigned.ToString(),
                "Driver accepted the job.");

            return MapToShipmentDto(shipment);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Driver progresses the shipment through transit stages.
    ///
    /// ONLY handles these two transitions (all others have dedicated methods):
    ///   PickedUp  → InTransit  (driver starts driving to Recipient)
    ///   InTransit → Arrived    (driver reaches Recipient's address)
    ///
    /// State machine is explicit — enum integer comparison is NOT used
    /// because it allowed skipping steps (e.g. PickedUp → Arrived directly).
    ///
    /// SignalR:
    ///   All transitions  → BroadcastStatusUpdateAsync
    ///   Arrived only     → BroadcastDriverArrivedAsync (includes GPS snapshot)
    /// </summary>
    public async Task<ShipmentDto> UpdateStatusAsync(
        int shipmentId, int driverId, ShipmentStatus newStatus)
    {
        var shipment = await _repo.GetByIdAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found.");

        // ── Ownership check ──────────────────────────────────────
        if (shipment.DriverId != driverId)
            throw new UnauthorizedAccessException(
                "You can only update shipments assigned to you.");

        // ── Terminal status guard ────────────────────────────────
        // These statuses have their own dedicated endpoints — reject here explicitly.
        if (newStatus is ShipmentStatus.Delivered
                      or ShipmentStatus.Cancelled
                      or ShipmentStatus.FailedDelivery)
            throw new InvalidOperationException(
                $"'{newStatus}' cannot be set via the status endpoint. " +
                "Use the dedicated verify-otp or fail-delivery endpoint.");

        // ── Explicit state machine guard ─────────────────────────
        if (!ValidDriverTransitions.TryGetValue(shipment.Status, out var allowedNext)
            || allowedNext != newStatus)
        {
            var hint = ValidDriverTransitions.ContainsKey(shipment.Status)
                ? $"Expected next status: '{ValidDriverTransitions[shipment.Status]}'."
                : $"No further transitions permitted from '{shipment.Status}'.";

            throw new InvalidOperationException(
                $"Cannot transition from '{shipment.Status}' to '{newStatus}'. {hint}");
        }

        shipment.Status    = newStatus;
        shipment.UpdatedAt = DateTime.UtcNow;

        var description = newStatus switch
        {
            ShipmentStatus.InTransit => "Driver is on the way to the recipient.",
            ShipmentStatus.Arrived   => "Driver has arrived at the delivery address.",
            _                        => $"Status updated to {newStatus}.",
        };

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId  = shipment.Id,
            Status      = newStatus,
            Description = description,
            ActorId     = driverId,
            OccurredAt  = DateTime.UtcNow,
        });

        await _ctx.SaveChangesAsync();

        // ── SignalR broadcasts ───────────────────────────────────
        await _tracking.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            newStatus.ToString(),
            description);

        // Arrived: also broadcast DriverArrived with current GPS snapshot
        if (newStatus == ShipmentStatus.Arrived)
        {
            var driverProfile = await _ctx.DriverProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(dp => dp.UserId == driverId);

            await _tracking.BroadcastDriverArrivedAsync(
                shipment.TrackingNumber,
                driverProfile?.CurrentLat ?? 0,
                driverProfile?.CurrentLng ?? 0);
        }

        return MapToShipmentDto(shipment);
    }

    /// <summary>
    /// Driver reports that delivery could not be completed.
    ///
    /// Guard: shipment must be Arrived — the driver must have reached the
    /// destination and attempted delivery before marking it as failed.
    /// (Cannot fail from InTransit — that would be abandoning the parcel mid-route.)
    ///
    /// SignalR: broadcasts FailedDelivery status to all group members.
    /// </summary>
    public async Task<ShipmentDto> FailDeliveryAsync(
        int shipmentId, int driverId, string reason)
    {
        var shipment = await _repo.GetByIdAsync(shipmentId)
            ?? throw new KeyNotFoundException("Shipment not found.");

        if (shipment.DriverId != driverId)
            throw new UnauthorizedAccessException(
                "You can only report failed delivery for your own assigned shipments.");

        // ── Status guard ─────────────────────────────────────────
        // Must be Arrived — driver must be at the destination to declare failure.
        if (shipment.Status != ShipmentStatus.Arrived)
            throw new InvalidOperationException(
                $"Delivery can only be marked as failed when status is 'Arrived'. " +
                $"Current status: '{shipment.Status}'.");

        shipment.Status    = ShipmentStatus.FailedDelivery;
        shipment.FailedAt  = DateTime.UtcNow;
        shipment.UpdatedAt = DateTime.UtcNow;

        var description = $"Delivery failed: {reason}";

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId  = shipment.Id,
            Status      = ShipmentStatus.FailedDelivery,
            Description = description,
            ActorId     = driverId,
            OccurredAt  = DateTime.UtcNow,
        });

        await _ctx.SaveChangesAsync();

        await _tracking.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.FailedDelivery.ToString(),
            description);

        return MapToShipmentDto(shipment);
    }

    // ═══════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Centralised Shipment → ShipmentDto mapper.
    /// Keeps all return statements consistent — no inline mapping scattered
    /// across methods.
    /// </summary>
    private static ShipmentDto MapToShipmentDto(Shipment shipment) => new()
    {
        Id             = shipment.Id,
        TrackingNumber = shipment.TrackingNumber,
        Status         = shipment.Status,
        CreatedAt      = shipment.CreatedAt,
    };
}