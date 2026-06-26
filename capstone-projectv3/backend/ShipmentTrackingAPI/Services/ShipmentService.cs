using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using Microsoft.Extensions.Options;
using ShipmentTrackingAPI.Models.DTOs.Common;

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
    private readonly AppDbContext _ctx;
    private readonly ITrackingService _tracking;
    private readonly IGpsSimulationChannel _gpsChannel;
    private readonly ILogger<ShipmentService> _logger;
    private readonly PricingSettings _pricingSettings;

    public ShipmentService(
        IShipmentRepository repo,
        AppDbContext ctx,
        ITrackingService tracking,
        IGpsSimulationChannel gpsChannel,
        ILogger<ShipmentService> logger,
        IOptions<PricingSettings> pricingSettings)
    {
        _repo = repo;
        _ctx = ctx;
        _tracking = tracking;
        _gpsChannel = gpsChannel;
        _logger = logger;
        _pricingSettings = pricingSettings.Value;
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
    /// Generates a price quote without saving anything to the database.
    /// </summary>
    public Task<ShipmentQuoteResponseDto> GetShipmentQuoteAsync(BookShipmentRequestDto req)
    {
        // 1. Map request items to in-memory models (no DB tracking needed)
        var tempItems = req.Items.Select(i => new ShipmentItem
        {
            WeightKg = i.Weight,
            LengthCm = i.Length,
            WidthCm = i.Width,
            HeightCm = i.Height,
            Quantity = i.Quantity,
        }).ToList();

        // 2. Run your existing pricing math, which now returns a detailed breakdown
        var breakdown = CalculateCostBreakdown(
            tempItems,
            req.PickupLat, req.PickupLng,
            req.DropoffLat, req.DropoffLng);

        return Task.FromResult(new ShipmentQuoteResponseDto
        {
            BaseRate = breakdown.BaseRate,
            DistanceCharge = Math.Round(breakdown.DistanceCharge,2),
            WeightSurcharge = breakdown.WeightCharge + breakdown.DimSurcharge,
            PlatformFee = breakdown.PlatformFee,
            EstimatedCost = breakdown.Total
        });
    }

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
            throw new ConflictException(
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
                CustomerId = customerId,
                TrackingNumber = trackingNumber,
                Status = ShipmentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await _repo.AddAsync(shipment);

            // 2. Address rows — Pickup and Dropoff in ShipmentAddresses child table.
            //    Sender identity comes from customer_id on Shipment (no contact fields on Pickup).
            //    Recipient contact details live on the Dropoff row only.
            var pickup = new ShipmentAddress
            {
                Shipment = shipment,
                AddressType = AddressType.Pickup,
                AddressLine = req.PickupAddress,
                Lat = req.PickupLat,
                Lng = req.PickupLng,
                // ContactName and ContactPhone intentionally null on Pickup row
            };

            var dropoff = new ShipmentAddress
            {
                Shipment = shipment,
                AddressType = AddressType.Dropoff,
                AddressLine = req.DropoffAddress,
                Lat = req.DropoffLat,
                Lng = req.DropoffLng,
                ContactName = req.RecipientName,   // Required — DB CHECK enforces this
                ContactPhone = req.RecipientPhone,  // Required — DB CHECK enforces this
            };

            await _ctx.Set<ShipmentAddress>().AddRangeAsync(pickup, dropoff);

            // 3. Item rows — all dimension fields are required by DB CHECK constraints.
            //    Missing LengthCm/WidthCm/HeightCm causes constraint violation on insert.
            var items = req.Items.Select(i => new ShipmentItem
            {
                Shipment = shipment,
                Description = i.Description,
                WeightKg = i.Weight,
                LengthCm = i.Length,   // ← was missing in original
                WidthCm = i.Width,    // ← was missing in original
                HeightCm = i.Height,   // ← was missing in original
                Quantity = i.Quantity,
            }).ToList();

            await _ctx.Set<ShipmentItem>().AddRangeAsync(items);
            
            var breakdown = CalculateCostBreakdown(
                     items,
                    req.PickupLat, req.PickupLng,
                    req.DropoffLat, req.DropoffLng);
            
            shipment.TotalCost = breakdown.Total; // Just save the final total to the DB

            // 4. Initial audit event
            await _repo.AddEventAsync(new ShipmentEvent
            {
                Shipment = shipment,
                Status = ShipmentStatus.Pending,
                Description = "Shipment booked and pending driver assignment.",
                OccurredAt = DateTime.UtcNow,
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
        // Clamp pagination bounds: page ≥ 1, size between 1 and 50
        q.Page = Math.Max(q.Page, 1);
        q.Size = Math.Clamp(q.Size, 1, 50);

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
            Id = s.Id,
            TrackingNumber = s.TrackingNumber,
            Status = s.Status,
            CreatedAt = s.CreatedAt,
            TotalCost = s.TotalCost,
            PickupArea = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Pickup)
                              ?.AddressLine ?? "N/A",
            DropoffArea = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Dropoff)
                              ?.AddressLine ?? "N/A",
        }).ToList();

        return new PaginatedResponse<ShipmentSummaryDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = q.Page,
            Size = q.Size,
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
            ?? throw new NotFoundException("Shipment not found.");

        if (role == UserRole.Customer && shipment.CustomerId != requesterId)
            throw new ForbiddenException("You do not have access to this shipment.");

        if (role == UserRole.Driver && shipment.DriverId != requesterId)
            throw new ForbiddenException("You do not have access to this shipment.");

        var pickup = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
        var dropoff = shipment.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

        return new ShipmentDetailDto
        {
            Id = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status,
            CreatedAt = shipment.CreatedAt,
            UpdatedAt = shipment.UpdatedAt,
            CustomerName = shipment.Customer?.FullName ?? "Unknown",
            RecipientName = dropoff?.ContactName ?? "Unknown",
            RecipientPhone = dropoff?.ContactPhone ?? "Unknown",
            DriverId = shipment.DriverId,
            DriverName = shipment.Driver?.FullName,
            DriverPhone = shipment.Driver?.DriverProfileUser?.PhoneNumber,
            VehicleNumber = shipment.Driver?.DriverProfileUser?.VehicleNumber,
            TotalCost = shipment.TotalCost,
            PickupAddress = new ShipmentAddressDto
            {
                AddressLine1 = pickup?.AddressLine ?? "",
                Latitude = pickup?.Lat,
                Longitude = pickup?.Lng,
            },
            DropoffAddress = new ShipmentAddressDto
            {
                AddressLine1 = dropoff?.AddressLine ?? "",
                Latitude = dropoff?.Lat,
                Longitude = dropoff?.Lng,
            },

            Items = shipment.ShipmentItems.Select(i => new ShipmentItemDto
            {
                Description = i.Description,
                Weight = i.WeightKg,
                Quantity = i.Quantity,
            }).ToList(),

            Events = shipment.ShipmentEvents
                .OrderBy(e => e.OccurredAt)
                .Select(e => new ShipmentEventDto
                {
                    Status = e.Status,
                    Description = e.Description,
                    Timestamp = e.OccurredAt,
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
            ?? throw new NotFoundException("Shipment not found.");

        if (shipment.CustomerId != customerId)
            throw new ForbiddenException("You can only cancel your own shipments.");

        if (shipment.Status != ShipmentStatus.Pending)
            throw new ConflictException(
                $"Cannot cancel a shipment with status '{shipment.Status}'. " +
                "Only Pending shipments can be cancelled by the customer.");

        shipment.Status = ShipmentStatus.Cancelled;
        shipment.UpdatedAt = DateTime.UtcNow;

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.Cancelled,
            Description = "Shipment cancelled by customer.",
            OccurredAt = DateTime.UtcNow,
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
            ?? throw new NotFoundException("Invalid tracking number. Please check the tracking number and try again.");

        var pickup = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup);
        var dropoff = details.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff);

        return new PublicTrackingDto
        {
            TrackingNumber = details.TrackingNumber,
            Status = details.Status,
            PickupAddress = pickup?.AddressLine ?? "N/A",
            DropoffAddress = dropoff?.AddressLine ?? "N/A",
            // Only expose driver location when actively in transit
            DriverLat = details.Status == ShipmentStatus.InTransit
                                ? details.Driver?.DriverProfileUser?.CurrentLat
                                : null,
            DriverLng = details.Status == ShipmentStatus.InTransit
                                ? details.Driver?.DriverProfileUser?.CurrentLng
                                : null,
            Events = details.ShipmentEvents
                .OrderBy(e => e.OccurredAt)
                .Select(e => new ShipmentEventDto
                {
                    Status = e.Status,
                    Description = e.Description,
                    Timestamp = e.OccurredAt,
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
        // Clamp pagination bounds
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 50);

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
            Id = s.Id,
            TrackingNumber = s.TrackingNumber,
            PickupArea = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Pickup)
                              ?.AddressLine ?? "N/A",
            DropoffArea = s.ShipmentAddresses
                              .FirstOrDefault(a => a.AddressType == AddressType.Dropoff)
                              ?.AddressLine ?? "N/A",
            TotalWeightKg = s.ShipmentItems.Sum(i => i.WeightKg * i.Quantity),
            ItemCount = s.ShipmentItems.Sum(i => i.Quantity),
            CreatedAt = s.CreatedAt,
        }).ToList();

        return new PaginatedResponse<PendingJobDto>
        {
            Data = items,
            TotalCount = totalCount,
            Page = page,
            Size = size,
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
        // ── GPS coordinate guard ──────────────────────────────────────────────
        // A driver must have a known location before accepting any job.
        // This ensures the GPS simulation and live-tracking work correctly
        // from the moment the job is assigned, and prevents a driver from
        // picking up a shipment they cannot properly navigate.
        var driverProfile = await _ctx.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(dp => dp.UserId == driverId);

        if (driverProfile == null ||
            !driverProfile.CurrentLat.HasValue ||
            !driverProfile.CurrentLng.HasValue)
        {
            throw new BadRequestException(
                "Your current GPS location must be set before accepting a shipment. " +
                "Please update your operational status with valid latitude and longitude first.");
        }

        // ── One-at-a-time guard ──────────────────────────────────────────────────────
        // A driver can only hold one active shipment at a time.
        // Blocks acceptance if any shipment assigned to this driver
        // is still in a non-terminal status (Assigned, PickedUp, InTransit, Arrived).
        var alreadyHasActiveShipment = await _repo.HasActiveShipmentAsync(driverId);
        if (alreadyHasActiveShipment)
            throw new ConflictException(
                "You already have an active shipment in progress. " +
                "Please complete or report the current delivery before accepting a new one.");

        using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var shipment = await _repo.GetByIdWithLockAsync(shipmentId, tx)
                ?? throw new NotFoundException("Shipment not found.");

            if (shipment.DriverId != null || shipment.Status != ShipmentStatus.Pending)
                throw new ConflictException(
                    "This shipment is no longer available. Another driver may have claimed it.");

            shipment.DriverId = driverId;
            shipment.Status = ShipmentStatus.Assigned;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId = shipment.Id,
                Status = ShipmentStatus.Assigned,
                Description = "Driver accepted the job.",
                ActorId = driverId,
                OccurredAt = DateTime.UtcNow,
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
            ?? throw new NotFoundException("Shipment not found.");

        // ── Ownership check ──────────────────────────────────────
        if (shipment.DriverId != driverId)
            throw new ForbiddenException(
                "You can only update shipments assigned to you.");

        // ── Terminal status guard ────────────────────────────────
        // These statuses have their own dedicated endpoints — reject here explicitly.
        if (newStatus is ShipmentStatus.Delivered
                      or ShipmentStatus.Cancelled
                      or ShipmentStatus.FailedDelivery)
            throw new BadRequestException(
                $"'{newStatus}' cannot be set via the status endpoint. " +
                "Use the dedicated verify-otp or fail-delivery endpoint.");

        // ── Explicit state machine guard ─────────────────────────
        if (!ValidDriverTransitions.TryGetValue(shipment.Status, out var allowedNext)
            || allowedNext != newStatus)
        {
            var hint = ValidDriverTransitions.ContainsKey(shipment.Status)
                ? $"Expected next status: '{ValidDriverTransitions[shipment.Status]}'."
                : $"No further transitions permitted from '{shipment.Status}'.";

            throw new ConflictException(
                $"Cannot transition from '{shipment.Status}' to '{newStatus}'. {hint}");
        }

        shipment.Status = newStatus;
        shipment.UpdatedAt = DateTime.UtcNow;

        var description = newStatus switch
        {
            ShipmentStatus.InTransit => "Driver is on the way to the recipient.",
            ShipmentStatus.Arrived => "Driver has arrived at the delivery address.",
            _ => $"Status updated to {newStatus}.",
        };

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId = shipment.Id,
            Status = newStatus,
            Description = description,
            ActorId = driverId,
            OccurredAt = DateTime.UtcNow,
        });

        await _ctx.SaveChangesAsync();

        // ── SignalR status broadcast (all transitions) ─────────────────────────
        await _tracking.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            newStatus.ToString(),
            description);

        // ── GPS simulation pub/sub ─────────────────────────────────────────────
        if (newStatus == ShipmentStatus.InTransit)
        {
            // Load driver GPS + dropoff coordinates to seed the simulation.
            // Only runs once on InTransit transition — not on every tick.
            var driverProfile = await _ctx.DriverProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(dp => dp.UserId == driverId);

            var dropoff = await _ctx.ShipmentAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.ShipmentId == shipment.Id &&
                    a.AddressType == AddressType.Dropoff);

            if (driverProfile?.CurrentLat.HasValue == true &&
                driverProfile.CurrentLng.HasValue &&
                dropoff?.Lat.HasValue == true &&
                dropoff.Lng.HasValue)
            {
                _gpsChannel.Publish(new GpsSimulationEvent
                {
                    EventType = GpsEventType.Started,
                    ShipmentId = shipment.Id,
                    TrackingNumber = shipment.TrackingNumber,
                    DriverProfileId = driverProfile.Id,
                    CurrentLat = driverProfile.CurrentLat.Value,
                    CurrentLng = driverProfile.CurrentLng.Value,
                    DropoffLat = dropoff.Lat.Value,
                    DropoffLng = dropoff.Lng.Value,
                });
            }
            else
            {
                _logger.LogWarning(
                    "ShipmentService: InTransit GPS event for {TN} not published — " +
                    "driver or dropoff coordinates are missing.",
                    shipment.TrackingNumber);
            }
        }
        else if (newStatus == ShipmentStatus.Arrived)
        {
            // Stop GPS simulation for this shipment
            _gpsChannel.Publish(new GpsSimulationEvent
            {
                EventType = GpsEventType.Stopped,
                ShipmentId = shipment.Id,
                TrackingNumber = shipment.TrackingNumber,
            });

            // Also broadcast DriverArrived with the final GPS snapshot
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
            ?? throw new NotFoundException("Shipment not found.");

        if (shipment.DriverId != driverId)
            throw new ForbiddenException(
                "You can only report failed delivery for your own assigned shipments.");

        // ── Status guard ─────────────────────────────────────────
        // Must be Arrived — driver must be at the destination to declare failure.
        if (shipment.Status != ShipmentStatus.Arrived)
            throw new ConflictException(
                $"Delivery can only be marked as failed when status is 'Arrived'. " +
                $"Current status: '{shipment.Status}'.");

        shipment.Status = ShipmentStatus.FailedDelivery;
        shipment.FailedAt = DateTime.UtcNow;
        shipment.UpdatedAt = DateTime.UtcNow;

        var description = $"Delivery failed: {reason}";

        await _repo.AddEventAsync(new ShipmentEvent
        {
            ShipmentId = shipment.Id,
            Status = ShipmentStatus.FailedDelivery,
            Description = description,
            ActorId = driverId,
            OccurredAt = DateTime.UtcNow,
        });

        await _ctx.SaveChangesAsync();

        await _tracking.BroadcastStatusUpdateAsync(
            shipment.TrackingNumber,
            ShipmentStatus.FailedDelivery.ToString(),
            description);

        return MapToShipmentDto(shipment);
    }

    private static ShipmentDto MapToShipmentDto(Shipment shipment) => new()
    {
        Id = shipment.Id,
        TrackingNumber = shipment.TrackingNumber,
        Status = shipment.Status,
        CreatedAt = shipment.CreatedAt,
        TotalCost = shipment.TotalCost
    };

    /// <summary>
    /// Calculates the cost and returns a breakdown of the fees using a Tuple.
    private (decimal BaseRate, decimal WeightCharge, decimal DimSurcharge, decimal DistanceCharge, decimal PlatformFee, decimal Total) CalculateCostBreakdown(
    IEnumerable<ShipmentItem> items,
    double? pickupLat, double? pickupLng,
    double? dropoffLat, double? dropoffLng)
    {
        // Now using values from appsettings.json!
        decimal baseRate = _pricingSettings.BaseRate;
        decimal weightRatePerKg = _pricingSettings.WeightRatePerKg;
        decimal distanceRatePerKm = _pricingSettings.DistanceRatePerKm;
        decimal dimWeightRate = _pricingSettings.DimWeightRate;
        decimal dimWeightDivisor = _pricingSettings.DimWeightDivisor;
        decimal platformFee = _pricingSettings.PlatformFee;

        var itemList = items.ToList();

        // ── 1. Actual weight charge ────────────────────────────────
        var totalActualWeight = itemList.Sum(i => i.WeightKg * i.Quantity);
        var weightCharge = totalActualWeight * weightRatePerKg;

        // ── 2. Dimensional weight surcharge ────────────────────────
        var dimSurcharge = 0m;
        foreach (var item in itemList)
        {
            if (item.LengthCm.HasValue && item.WidthCm.HasValue && item.HeightCm.HasValue)
            {
                var volumetricKg = (item.LengthCm.Value * item.WidthCm.Value * item.HeightCm.Value)
                                   / dimWeightDivisor * item.Quantity;
                var actualKg = item.WeightKg * item.Quantity;

                if (volumetricKg > actualKg)
                    dimSurcharge += (volumetricKg - actualKg) * dimWeightRate;
            }
        }

        // ── 3. Distance charge ─────────────────────────────────────
        var distanceCharge = 0m;
        if (pickupLat.HasValue && pickupLng.HasValue &&
            dropoffLat.HasValue && dropoffLng.HasValue)
        {
            var km = HaversineKm(pickupLat.Value, pickupLng.Value,
                                 dropoffLat.Value, dropoffLng.Value);
            distanceCharge = (decimal)km * distanceRatePerKm;
        }

        // ── 4. Calculate Final Total ─────────────────────────────────────
        var finalTotal = Math.Round(baseRate + weightCharge + dimSurcharge + distanceCharge + platformFee, 2);

        // Return all the calculated pieces together as a Tuple
        return (baseRate, weightCharge, dimSurcharge, distanceCharge, platformFee, finalTotal);
    }

    /// <summary>
    /// Haversine formula — great-circle distance in KM between two GPS points.
    /// </summary>
    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}