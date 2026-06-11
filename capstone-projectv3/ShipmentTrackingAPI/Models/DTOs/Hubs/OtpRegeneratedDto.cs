namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Broadcast to ALL members of the shipment group when an OTP is regenerated.
/// Intentionally does NOT contain the OTP code.
///
/// Sent by: ShipmentService → ITrackingService.BroadcastOtpRegeneratedAsync()
///          immediately AFTER PushOtpToSenderAsync or PushOtpToRecipientAsync
///          sends the new code to the correct user.
///
/// Received by: all group members (Customer/Sender, Recipient, Driver)
///
/// Purpose:
///   - Driver's screen shows "New OTP has been sent to the customer."
///   - Sender/Recipient's screen resets the countdown timer (they receive
///     the new code via OtpGeneratedDto on their individual connection).
///   - Separating the "notification" (group broadcast) from the "code"
///     (targeted push) ensures the Driver never sees the OTP.
///
/// Angular listener:
///   this.hubConnection.on('OtpRegenerated', (dto: OtpRegeneratedDto) => { ... });
/// </summary>
public sealed record OtpRegeneratedDto
{
    /// <summary>
    /// "Pickup" or "Delivery" — tells Angular which OTP type was regenerated.
    /// </summary>
    public string   OtpType        { get; init; } = default!;

    public string   TrackingNumber { get; init; } = default!;

    /// <summary>
    /// UTC expiry of the new OTP window.
    /// Angular uses this to reset the countdown timer on the Sender/Recipient screen.
    /// </summary>
    public DateTime ExpiresAt      { get; init; }

    // OTP code is intentionally absent.
    // The code is sent separately via ITrackingService.PushOtpToSenderAsync()
    // or PushOtpToRecipientAsync() to the specific connection only.
}
