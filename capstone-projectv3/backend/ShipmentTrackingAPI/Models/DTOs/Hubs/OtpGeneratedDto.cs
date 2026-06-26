namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Sent to the Sender's OR Recipient's connection ID only — NEVER broadcast
/// to the full group. The Driver intentionally does not receive this.
///
/// The same DTO shape is used for both Pickup and Delivery OTPs.
/// The OtpType field tells Angular which label to show.
///
/// Pickup OTP:
///   Sent by: ShipmentService → ITrackingService.PushOtpToSenderAsync()
///   Angular listener (Sender's tracking page):
///     this.hubConnection.on('PickupOtpGenerated', (dto: OtpGeneratedDto) => { ... });
///
/// Delivery OTP:
///   Sent by: ShipmentService → ITrackingService.PushOtpToRecipientAsync()
///   Angular listener (Recipient's tracking page):
///     this.hubConnection.on('DeliveryOtpGenerated', (dto: OtpGeneratedDto) => { ... });
///
/// SECURITY NOTE:
///   This DTO contains the raw OTP code. It is only ever sent via
///   Clients.Client(connectionId) — never via Clients.Group().
///   The connectionId is resolved from the userId → connectionId map
///   in TrackingService. If the user is not connected, the push is
///   silently skipped (they poll the REST endpoint instead).
/// </summary>
public sealed record OtpGeneratedDto
{
    /// <summary>
    /// "Pickup" or "Delivery".
    /// Angular uses this to render "Your Pickup OTP" vs "Your Delivery OTP".
    /// </summary>
    public string   OtpType        { get; init; } = default!;

    public string   TrackingNumber { get; init; } = default!;

    /// <summary>
    /// The 4-digit OTP code. e.g. "4827"
    /// Displayed prominently on the Sender/Recipient tracking screen.
    /// Never logged, never broadcast to the group.
    /// </summary>
    public string   OtpCode        { get; init; } = default!;

    /// <summary>
    /// UTC expiry of this OTP window (15 minutes from generation).
    /// Angular starts a countdown timer from this value.
    /// </summary>
    public DateTime ExpiresAt      { get; init; }
}
