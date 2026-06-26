using System.Security.Cryptography;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Otp;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Services.Interfaces;

using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;


namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Manages the dual-OTP chain of custody (Pickup OTP + Delivery OTP).
///
/// DRY design: RequestOtpAsync / VerifyOtpAsync / RegenerateOtpAsync
/// each handle both OtpType.Pickup and OtpType.Delivery through a
/// single method body — the otpType parameter drives the branching.
///
/// Security rules enforced here:
///   - OTP code is generated using RandomNumberGenerator (cryptographic).
///   - Max 3 verification attempts; 4th attempt throws 429.
///   - OTP code is cleared from the DB immediately on successful verification.
///   - Regeneration is only permitted when locked (attempt_count = 3) or expired.
///   - OTP codes are NEVER returned to the driver — they are pushed only
///     to the Sender (Pickup) or Recipient (Delivery) via SignalR.
/// </summary>
public class OtpService : IOtpService
{
    private const int OtpExpiryMinutes = 15;
    private const int MaxAttempts      = 3;

    private readonly IShipmentRepository _shipmentRepo;
    private readonly ITrackingService    _tracking;
    private readonly AppDbContext _ctx;

    public OtpService(
        IShipmentRepository shipmentRepo,
        ITrackingService tracking,
        AppDbContext ctx)
    {
        _shipmentRepo = shipmentRepo;
        _tracking     = tracking;
        _ctx          = ctx;
    }

    // ─────────────────────────────────────────────────────────
    //  REQUEST OTP  (Pickup or Delivery)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a 4-digit OTP and upserts it into shipment_otp_windows.
    /// Pushes the code to the Sender (Pickup) or Recipient (Delivery)
    /// via a targeted SignalR push — never to the group broadcast channel.
    ///
    /// Pre-conditions validated here:
    ///   - Pickup OTP: shipment must be Assigned and driver must own it.
    ///   - Delivery OTP: shipment must be Arrived and driver must own it.
    /// </summary>
    public async Task<OtpWindowDto> RequestOtpAsync(
        int shipmentId,
        OtpType otpType,
        int driverId)
    {
        var shipment = await _shipmentRepo.GetByIdWithAddressesAsync(shipmentId)
            ?? throw new NotFoundException($"Shipment {shipmentId}");

        ValidateDriverOwnership(shipment, driverId);
        ValidatePreOtpStatus(shipment, otpType);

        var code      = GenerateCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);

        // Upsert: inserts on first call, resets on re-request
        var window = new ShipmentOtpWindow
        {
            ShipmentId    = shipmentId,
            OtpType       = otpType,
            OtpCode       = code,
            ExpiresAt     = expiresAt,
            AttemptCount  = 0,
            GeneratedAt   = DateTime.UtcNow,
            VerifiedAt    = null
        };

        await _shipmentRepo.UpsertOtpWindowAsync(window);

        // Push code to the correct party — NOT to the driver, NOT to the group.
        // Both Pickup and Delivery OTPs are pushed to shipment.CustomerId because
        // in this capstone the booking customer manages both OTP flows.
        if (otpType == OtpType.Pickup)
            await _tracking.PushOtpToSenderAsync(shipment.CustomerId,
                shipment.TrackingNumber, code, expiresAt);
        else
            await _tracking.PushOtpToRecipientAsync(shipment.CustomerId,
                shipment.TrackingNumber, code, expiresAt);

        return new OtpWindowDto
        {
            OtpType      = otpType.ToString(),
            ExpiresAt    = expiresAt,
            AttemptCount = 0,
            GeneratedAt  = DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────────────────────
    //  VERIFY OTP  (Pickup or Delivery)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the 4-digit code submitted by the driver.
    ///
    /// On success:
    ///   - Clears otp_code and expires_at from the window row.
    ///   - Sets verified_at timestamp.
    ///   - Transitions shipment status (Pickup → PickedUp, Delivery → Delivered).
    ///   - Broadcasts the appropriate SignalR event.
    ///
    /// On failure:
    ///   - Increments attempt_count.
    ///   - Returns remaining attempts.
    ///   - At attempt_count = 3: throws 429 requiring regeneration.
    /// </summary>
    public async Task<VerifyOtpResultDto> VerifyOtpAsync(
        int shipmentId,
        OtpType otpType,
        string code,
        int driverId)
    {
        var shipment = await _shipmentRepo.GetByIdWithAddressesAsync(shipmentId)
            ?? throw new NotFoundException($"Shipment {shipmentId}");

        ValidateDriverOwnership(shipment, driverId);

        // Load OTP window with tracking so we can modify and save it
        var window = await _ctx.ShipmentOtpWindows
            .FirstOrDefaultAsync(w =>
                w.ShipmentId == shipmentId &&
                w.OtpType    == otpType)
            ?? throw new BadRequestException(
                $"No {otpType} OTP has been generated for this shipment. " +
                "Please request an OTP first.");

        // Guard: already locked out
        if (window.IsLocked)
            throw new RateLimitException(
                "Maximum OTP attempts reached. Please regenerate the OTP.");

        // Guard: window has expired
        if (window.IsExpired)
            throw new BadRequestException(
                "The OTP has expired. Please regenerate it.");

        // Guard: already verified (prevents replay)
        if (window.VerifiedAt.HasValue)
            throw new ConflictException(
                $"The {otpType} OTP for this shipment has already been verified.");

        // ── Wrong code path ─────────────────────────────────
        if (window.OtpCode != code.Trim())
        {
            window.AttemptCount++;
            _ctx.ShipmentOtpWindows.Update(window);
            await _ctx.SaveChangesAsync();

            var remaining = MaxAttempts - window.AttemptCount;

            if (window.IsLocked)
                throw new RateLimitException(
                    "Maximum OTP attempts reached. Please regenerate the OTP.");

            return new VerifyOtpResultDto
            {
                Success           = false,
                RemainingAttempts = remaining,
                Message           = $"Incorrect OTP. {remaining} attempt(s) remaining."
            };
        }

        // ── Correct code path ────────────────────────────────
        window.OtpCode     = null;             // clear — never stored after use
        window.ExpiresAt   = null;
        window.VerifiedAt  = DateTime.UtcNow;
        window.AttemptCount++;                 // records the successful attempt

        _ctx.ShipmentOtpWindows.Update(window);

        // Transition shipment status in the same SaveChanges call
        var newStatus = otpType == OtpType.Pickup
            ? ShipmentStatus.PickedUp
            : ShipmentStatus.Delivered;

        shipment.Status    = newStatus;
        shipment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == ShipmentStatus.PickedUp)
            shipment.PickedUpAt = DateTime.UtcNow;

        if (newStatus == ShipmentStatus.Delivered)
            shipment.DeliveredAt = DateTime.UtcNow;

        _ctx.Shipments.Update(shipment);

        // Append audit event
        _ctx.ShipmentEvents.Add(new ShipmentEvent
        {
            ShipmentId  = shipmentId,
            Status      = newStatus,
            Description = otpType == OtpType.Pickup
                ? "Parcel collected from sender — Proof of Pickup confirmed."
                : "Parcel delivered to recipient — Proof of Delivery confirmed.",
            ActorId     = driverId,
            OccurredAt  = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync();

        // SignalR broadcast
        if (newStatus == ShipmentStatus.Delivered)
            await _tracking.BroadcastDeliverySuccessAsync(shipment.TrackingNumber);
        else
            await _tracking.BroadcastStatusUpdateAsync(
                shipment.TrackingNumber,
                newStatus.ToString(),
                otpType == OtpType.Pickup
                    ? "Parcel collected from sender."
                    : "Parcel delivered to recipient.");

        return new VerifyOtpResultDto
        {
            Success           = true,
            RemainingAttempts = 0,
            NewStatus         = newStatus,
            Message           = otpType == OtpType.Pickup
                ? "Pickup confirmed. Start transit when ready."
                : "Delivery confirmed. Shipment complete."
        };
    }

    // ─────────────────────────────────────────────────────────
    //  REGENERATE OTP  (Pickup or Delivery)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resets the OTP window with a fresh code and a new 15-minute expiry.
    /// Only permitted when the window is locked (3 failed attempts)
    /// or the window has expired.
    /// Pushes the new code to the correct party via SignalR.
    /// </summary>
    public async Task<OtpWindowDto> RegenerateOtpAsync(
        int shipmentId,
        OtpType otpType,
        int driverId)
    {
        var shipment = await _shipmentRepo.GetByIdWithAddressesAsync(shipmentId)
            ?? throw new NotFoundException($"Shipment {shipmentId}");

        ValidateDriverOwnership(shipment, driverId);

        var window = await _ctx.ShipmentOtpWindows
            .FirstOrDefaultAsync(w =>
                w.ShipmentId == shipmentId &&
                w.OtpType    == otpType)
            ?? throw new BadRequestException(
                $"No {otpType} OTP window found. Please request an OTP first.");

        // Regeneration only permitted when locked or expired
        if (!window.IsLocked && !window.IsExpired)
            throw new ConflictException(
                "OTP can only be regenerated after it has expired or " +
                "the maximum attempt count has been reached.");

        var code      = GenerateCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(OtpExpiryMinutes);

        window.OtpCode      = code;
        window.ExpiresAt    = expiresAt;
        window.AttemptCount = 0;
        window.GeneratedAt  = DateTime.UtcNow;
        window.VerifiedAt   = null;

        _ctx.ShipmentOtpWindows.Update(window);
        await _ctx.SaveChangesAsync();

        // Push new code to correct party — CustomerId used for both OTP types (capstone design)
        if (otpType == OtpType.Pickup)
            await _tracking.PushOtpToSenderAsync(shipment.CustomerId,
                shipment.TrackingNumber, code, expiresAt);
        else
            await _tracking.PushOtpToRecipientAsync(shipment.CustomerId,
                shipment.TrackingNumber, code, expiresAt);

        // Notify the entire tracking group that the OTP was regenerated
        // (no code exposed — just signals clients to refresh their UI state)
        await _tracking.BroadcastOtpRegeneratedAsync(
            shipment.TrackingNumber,
            otpType.ToString(),
            expiresAt);

        return new OtpWindowDto
        {
            OtpType      = otpType.ToString(),
            ExpiresAt    = expiresAt,
            AttemptCount = 0,
            GeneratedAt  = DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────────────────────
    //  PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a cryptographically random 4-digit numeric string.
    /// Pads with leading zeros: values 0–9 become "0000"–"0009".
    /// NEVER uses System.Random — cryptographic source required for security.
    /// </summary>
    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");

    /// <summary>
    /// Confirms that the driver calling the endpoint is the one
    /// assigned to this shipment. Prevents drivers from generating
    /// or verifying OTPs for shipments they do not own.
    /// </summary>
    private static void ValidateDriverOwnership(Shipment shipment, int driverId)
    {
        if (shipment.DriverId != driverId)
            throw new ForbiddenException(
                "You can only manage OTPs for your own assigned shipment.");
    }

    /// <summary>
    /// Validates that the shipment is in the correct state for OTP generation.
    /// Pickup OTP: status must be Assigned (driver has accepted job).
    /// Delivery OTP: status must be Arrived (driver is at destination).
    /// </summary>
    private static void ValidatePreOtpStatus(Shipment shipment, OtpType otpType)
    {
        var required = otpType == OtpType.Pickup
            ? ShipmentStatus.Assigned
            : ShipmentStatus.Arrived;

        if (shipment.Status != required)
            throw new ConflictException(
                $"{otpType} OTP can only be requested when shipment status " +
                $"is '{required}'. Current status: '{shipment.Status}'.");
    }
}