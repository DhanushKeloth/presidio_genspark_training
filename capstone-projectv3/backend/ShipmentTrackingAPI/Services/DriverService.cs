using ShipmentTrackingAPI.DTOs.Driver;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Handles driver-scoped operations: profile retrieval and
/// operational status toggling.
///
/// Account status (Active / Suspended / Deleted) is Admin-controlled
/// and lives in AdminService. Op status (Available / InTransit / Offline)
/// is Driver-controlled and lives here.
/// </summary>
public class DriverService : IDriverService
{
    private readonly IDriverRepository   _driverRepo;
    private readonly IShipmentRepository _shipmentRepo;

    public DriverService(
        IDriverRepository driverRepo,
        IShipmentRepository shipmentRepo)
    {
        _driverRepo   = driverRepo;
        _shipmentRepo = shipmentRepo;
    }

    // ─────────────────────────────────────────────────────────
    //  GET PROFILE
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the calling driver's own profile.
    /// Scoped strictly to the JWT user id — a driver cannot
    /// view another driver's profile through this endpoint.
    /// </summary>
    public async Task<DriverProfileDto> GetMyProfileAsync(int userId)
    {
        var profile = await _driverRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Driver profile not found for this account.");

        return MapToDto(profile);
    }

    // ─────────────────────────────────────────────────────────
    //  UPDATE OPERATIONAL STATUS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Allows an Active driver to toggle between Available,
    /// InTransit, and Offline.
    ///
    /// Guards enforced:
    ///   1. Account must be Active — Suspended / Deleted / PendingApproval
    ///      drivers are blocked entirely.
    ///   2. Toggling to Available is blocked if the driver has an active
    ///      (non-terminal) assigned shipment — prevents abandoning a delivery.
    ///   3. Toggling to InTransit directly is blocked — InTransit is set
    ///      automatically by ShipmentService.UpdateStatusAsync when the
    ///      driver triggers Start Transit on a shipment.
    ///   4. Toggling to Offline is blocked if the driver has an active
    ///      shipment — prevents wiping GPS coordinates mid-delivery, which
    ///      would break live tracking and the GPS simulation.
    /// </summary>
    public async Task<DriverProfileDto> UpdateOpStatusAsync(
        int userId,
        UpdateOpStatusRequestDto request)
    {
        var newStatus = request.NewStatus;
        var profile = await _driverRepo.GetByUserIdAsync(userId)
            ?? throw new NotFoundException(
                "Driver profile not found for this account.");

        // Guard 1: account must be Active
        if (profile.AccountStatus != DriverAccountStatus.Active)
            throw new ForbiddenException(
                "Only active driver accounts can update operational status. " +
                $"Your account status is '{profile.AccountStatus}'.");

        // Guard 2: drivers cannot self-set InTransit
        // This status is set by ShipmentService when Start Transit is triggered
        if (newStatus == DriverOpStatus.InTransit)
            throw new ConflictException(
                "InTransit status is set automatically when you start transit " +
                "on an assigned shipment. Use the shipment status endpoint instead.");

        // Guard 3: cannot go Available while holding an active shipment
        if (newStatus == DriverOpStatus.Available)
        {
            var hasActiveShipment = await _shipmentRepo
                .HasActiveShipmentAsync(userId);

            if (hasActiveShipment)
                throw new ConflictException(
                    "You cannot set yourself as Available while you have an " +
                    "active shipment in progress. Complete or fail the current " +
                    "delivery first.");
        }

        // Guard 4: cannot go Offline while holding an active shipment
        // Going Offline wipes CurrentLat/CurrentLng from the driver profile.
        // If a driver has an active shipment, wiping their GPS coordinates would
        // break live tracking for the customer and crash the GPS simulation service.
        if (newStatus == DriverOpStatus.Offline)
        {
            var hasActiveShipment = await _shipmentRepo
                .HasActiveShipmentAsync(userId);

            if (hasActiveShipment)
                throw new ConflictException(
                    "You cannot go offline while you have an active shipment in progress. " +
                    "Complete or report the current delivery first.");
        }

        profile.OpStatus   = newStatus;
        if (newStatus == DriverOpStatus.Available && request.CurrentLat.HasValue && request.CurrentLng.HasValue)
        {
            // Update location when they clock in
            profile.CurrentLat = request.CurrentLat;
            profile.CurrentLng = request.CurrentLng;
        }
        else if (newStatus == DriverOpStatus.Offline)
        {
            // Wipe their location when they clock out for privacy
            profile.CurrentLat = null;
            profile.CurrentLng = null;
        }
        profile.UpdatedAt  = DateTime.UtcNow;

        await _driverRepo.UpdateAsync(profile);
        await _driverRepo.SaveAsync();

        return MapToDto(profile);
    }

    // ─────────────────────────────────────────────────────────
    //  PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────

    private static DriverProfileDto MapToDto(
        ShipmentTrackingAPI.Models.DriverProfile profile) => new()
    {
        Id              = profile.Id,
        UserId          = profile.UserId,
        FullName        = profile.User.FullName,
        Email           = profile.User.Email,
        VehicleType     = profile.VehicleType,
        VehicleNumber   = profile.VehicleNumber,
        LicenseNumber   = profile.LicenseNumber,
        AccountStatus   = profile.AccountStatus,
        OpStatus        = profile.OpStatus,
        CurrentLat      = profile.CurrentLat,
        CurrentLng      = profile.CurrentLng,
        // ApprovedAt      = profile.ApprovedAt,
        CreatedAt       = profile.CreatedAt
    };
}