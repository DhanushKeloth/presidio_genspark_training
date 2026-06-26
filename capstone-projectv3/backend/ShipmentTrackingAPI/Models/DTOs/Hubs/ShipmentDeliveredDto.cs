namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Broadcast to all members of the shipment group when the Delivery OTP
/// is verified successfully and the shipment reaches terminal status Delivered.
///
/// Sent by: ShipmentService → ITrackingService.BroadcastDeliverySuccessAsync()
///          after verify-delivery-otp succeeds.
///
/// Received by: all group members (Customer/Sender, Recipient, Driver)
///
/// Angular behaviour on receipt:
///   - ALL tracking screens: show delivery success state
///     ("Your parcel has been delivered!").
///   - Angular automatically calls LeaveShipmentGroup after receiving this
///     event — the shipment is terminal, no more live events will follow.
///   - The GPS Background Service will stop sending LocationUpdated for
///     this shipment on the next tick (status is no longer InTransit).
///
/// Angular listener:
///   this.hubConnection.on('ShipmentDelivered', (dto: ShipmentDeliveredDto) => {
///     // show success UI
///     await this.hubConnection.invoke('LeaveShipmentGroup', dto.trackingNumber);
///   });
/// </summary>
public sealed record ShipmentDeliveredDto
{
    public string   TrackingNumber { get; init; } = default!;

    /// <summary>
    /// UTC timestamp when the delivery OTP was verified (delivered_at on Shipments table).
    /// This is the legal Proof of Delivery timestamp.
    /// Angular displays this on the success screen and in the event timeline.
    /// </summary>
    public DateTime DeliveredAt    { get; init; }
}
