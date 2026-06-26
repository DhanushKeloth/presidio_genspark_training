using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Admin;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Services;

public class AdminService : IAdminService
{
    private readonly IShipmentRepository _shipmentRepo;
    private readonly IDriverRepository _driverRepo;
    private readonly IUserRepository _userRepo;
    private readonly AppDbContext _ctx;
    private readonly IGpsSimulationChannel _gpsChannel;

    public AdminService(
        IShipmentRepository shipmentRepo,
        IDriverRepository driverRepo,
        IUserRepository userRepo,
        AppDbContext ctx,
        IGpsSimulationChannel gpsChannel)
    {
        _shipmentRepo = shipmentRepo;
        _driverRepo = driverRepo;
        _userRepo = userRepo;
        _ctx = ctx;
        _gpsChannel = gpsChannel;
    }

    // ─────────────────────────────────────────────────────────
    //  DRIVER MANAGEMENT
    // ─────────────────────────────────────────────────────────

    public async Task<PaginatedResponse<AdminDriverDto>> GetAllDriversAsync(
        DriverAccountStatus? status,
        int page,
        int size)
    {
        // Clamp pagination bounds
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 50);

        // BUG 1 FIXED: Bypassed the SQL View and used standard Entity Framework Includes
        var query = _ctx.DriverProfiles
            .AsNoTracking()
            .Include(dp => dp.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(d => d.AccountStatus == status.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PaginatedResponse<AdminDriverDto>
        {
            Data = items.Select(d => new AdminDriverDto
            {
                Id = d.Id,
                FullName = d.User?.FullName ?? "Unknown",
                Email = d.User?.Email ?? "Unknown",
                PhoneNumber = d.PhoneNumber ?? "N/A",
                AccountStatus = d.AccountStatus,
                VehicleType = d.VehicleType ?? "Unknown",
                CreatedAt = d.CreatedAt
            }).ToList(),

            TotalCount = total,
            Page = page,
            Size = size
        };
    }

    public async Task<AdminDriverDetailDto> GetDriverDetailAsync(int driverProfileId)
    {
        var profile = await _ctx.DriverProfiles
            .AsNoTracking() // Added for performance
            .Include(dp => dp.User)
            .Include(dp => dp.ApprovedByNavigation)
            .FirstOrDefaultAsync(dp => dp.Id == driverProfileId)
            ?? throw new NotFoundException($"Driver profile {driverProfileId}");

        // BUG 3 FIXED: Deleted the unused recentShipments query to make the API faster

        return new AdminDriverDetailDto
        {
            Id = profile.Id,
            FullName = profile.User?.FullName ?? "Unknown",
            Email = profile.User?.Email ?? "Unknown",
            PhoneNumber = profile.PhoneNumber ?? "N/A",
            AccountStatus = profile.AccountStatus,
            VehicleType = profile.VehicleType ?? "Unknown",
            CreatedAt = profile.CreatedAt,

            VehicleNumber = profile.VehicleNumber,
            LicenseNumber = profile.LicenseNumber,
            OpStatus = profile.OpStatus,
            ApprovedAt = profile.ApprovedAt,
            ApprovedByName = profile.ApprovedByNavigation?.FullName
        };
    }

    public async Task UpdateDriverAccountStatusAsync(
        int adminId,
        int driverProfileId,
        DriverAccountStatus newStatus)
    {
        var profile = await _ctx.DriverProfiles
            .Include(dp => dp.User)
            .FirstOrDefaultAsync(dp => dp.Id == driverProfileId)
            ?? throw new NotFoundException($"Driver profile {driverProfileId}");

        // Guard: block Suspend or Delete while driver has an active delivery in progress
        var hasActiveDelivery = await _ctx.Shipments
            .AnyAsync(s =>
                s.DriverId == profile.UserId &&
                (s.Status == ShipmentStatus.Assigned ||
                 s.Status == ShipmentStatus.PickedUp ||
                 s.Status == ShipmentStatus.InTransit ||
                 s.Status == ShipmentStatus.Arrived));

        if (hasActiveDelivery &&
            (newStatus == DriverAccountStatus.Suspended ||
             newStatus == DriverAccountStatus.Deleted))
        {
            throw new ConflictException(
                $"Cannot change driver {driverProfileId} status to '{newStatus}' because they " +
                "have an active delivery in progress. " +
                "Use the admin shipment override to resolve the delivery first.");
        }

        if (newStatus == DriverAccountStatus.Active)
        {
            profile.ApprovedBy = adminId;
            profile.ApprovedAt = DateTime.UtcNow;
        }

        if (newStatus is DriverAccountStatus.Suspended or DriverAccountStatus.Deleted)
            profile.OpStatus = null;

        if (newStatus == DriverAccountStatus.Deleted)
        {
            profile.User.IsActive = false;
            profile.User.UpdatedAt = DateTime.UtcNow;
            _ctx.Users.Update(profile.User);
        }

        profile.AccountStatus = newStatus;
        profile.UpdatedAt = DateTime.UtcNow;

        _ctx.DriverProfiles.Update(profile);
        await _ctx.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────
    //  SHIPMENT OVERSIGHT
    // ─────────────────────────────────────────────────────────

    public async Task<PaginatedResponse<AdminShipmentDto>> GetAllShipmentsAsync(
        ShipmentStatus? status,
        int? driverUserId,
        int page,
        int size)
    {
        // Clamp pagination bounds
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 50);

        var query = _ctx.Shipments
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Driver)
            .Include(s => s.ShipmentAddresses) // BUG 2 FIXED: Added this so the Address mappings below actually work!
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (driverUserId.HasValue)
            query = query.Where(s => s.DriverId == driverUserId.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PaginatedResponse<AdminShipmentDto>
        {
            Data = items.Select(s => new AdminShipmentDto
            {
                Id = s.Id,
                TrackingNumber = s.TrackingNumber,
                Status = s.Status,
                CustomerName = s.Customer?.FullName ?? "Unknown",
                DriverName = s.Driver?.FullName,
                PickupArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Pickup)?.AddressLine ?? "N/A",
                DropoffArea = s.ShipmentAddresses.FirstOrDefault(a => a.AddressType == AddressType.Dropoff)?.AddressLine ?? "N/A",
                CreatedAt = s.CreatedAt,
                TotalCost=s.TotalCost
            }).ToList(),

            TotalCount = total,
            Page = page,
            Size = size
        };
    }

    public async Task OverrideShipmentStatusAsync(
        int adminId,
        int shipmentId,
        ShipmentStatus newStatus,
        string reason)
    {
        var shipment = await _ctx.Shipments
            .Include(s => s.ShipmentEvents)
            .FirstOrDefaultAsync(s => s.Id == shipmentId)
            ?? throw new NotFoundException($"Shipment {shipmentId}");

        var previousStatus = shipment.Status;

        shipment.Status = newStatus;
        shipment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == ShipmentStatus.Delivered && !shipment.DeliveredAt.HasValue)
            shipment.DeliveredAt = DateTime.UtcNow;

        if (newStatus == ShipmentStatus.Cancelled && !shipment.CancelledAt.HasValue)
            shipment.CancelledAt = DateTime.UtcNow;

        if (newStatus == ShipmentStatus.FailedDelivery && !shipment.FailedAt.HasValue)
            shipment.FailedAt = DateTime.UtcNow;

        // If the shipment was InTransit and is being overridden to any other status,
        // stop GPS simulation immediately so the background service stops broadcasting.
        if (previousStatus == ShipmentStatus.InTransit && newStatus != ShipmentStatus.InTransit)
        {
            _gpsChannel.Publish(new GpsSimulationEvent
            {
                EventType = GpsEventType.Stopped,
                ShipmentId = shipmentId,
                TrackingNumber = shipment.TrackingNumber,
            });
        }

        if (newStatus is ShipmentStatus.Delivered or ShipmentStatus.Cancelled or ShipmentStatus.FailedDelivery)
        {
            if (shipment.DriverId.HasValue)
            {
                var driverProfile = await _driverRepo
                    .GetByUserIdAsync(shipment.DriverId.Value);

                if (driverProfile is not null &&
                    driverProfile.AccountStatus == DriverAccountStatus.Active)
                {
                    driverProfile.OpStatus = DriverOpStatus.Available;
                    driverProfile.UpdatedAt = DateTime.UtcNow;
                    await _driverRepo.UpdateAsync(driverProfile);
                }
            }
        }

        _ctx.Shipments.Update(shipment);

        _ctx.ShipmentEvents.Add(new ShipmentEvent
        {
            ShipmentId = shipmentId,
            Status = newStatus,
            Description = $"Status overridden by Admin from '{previousStatus}' " +
                          $"to '{newStatus}'. Reason: {reason.Trim()}",
            ActorId = adminId,
            OccurredAt = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────
    //  DASHBOARD METRICS
    // ─────────────────────────────────────────────────────────

    public async Task<DashboardDto> GetDashboardMetricsAsync()
    {
        // Single query: group all shipments by status and count each group
        var statusCounts = await _ctx.Shipments
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var pending = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Pending)?.Count ?? 0;
        var assigned = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Assigned)?.Count ?? 0;
        var pickedUp = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.PickedUp)?.Count ?? 0;
        var inTransit = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.InTransit)?.Count ?? 0;
        var arrived = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Arrived)?.Count ?? 0;

        // Today's deliveries — separate targeted query needed for the date filter
        var today = DateTime.UtcNow.Date;
        var deliveredToday = await _ctx.Shipments.CountAsync(s =>
            s.Status == ShipmentStatus.Delivered &&
            s.DeliveredAt.HasValue &&
            s.DeliveredAt.Value >= today);

        var driversPending = await _ctx.DriverProfiles.CountAsync(d => d.AccountStatus == DriverAccountStatus.PendingApproval);
        var driversActive = await _ctx.DriverProfiles.CountAsync(d => d.AccountStatus == DriverAccountStatus.Active);

        return new DashboardDto
        {
            TotalPendingShipments = pending,
            ActiveDeliveries = assigned + pickedUp + inTransit + arrived,
            DeliveriesToday = deliveredToday,
            DriversPendingApproval = driversPending,
            TotalActiveDrivers = driversActive
        };
    }
}