namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Broadcast to all members of the shipment group when the driver
/// updates status to Arrived (reached the Recipient's address).
///
/// Sent by: ShipmentService → ITrackingService.BroadcastDriverArrivedAsync()
///          when the driver calls PUT /api/shipments/{id}/status with Arrived.
///
/// Received by: all group members (Customer/Sender, Recipient, Driver)
///
/// Angular behaviour on receipt:
///   - Recipient's tracking screen: shows "Your driver has arrived!",
///     hides the live map pin, prepares to display the Delivery OTP
///     (which arrives separately via DeliveryOtpGenerated after the
///     driver calls request-delivery-otp).
///   - Driver's active job screen: transitions UI to the Delivery OTP
///     submission panel.
///
/// Angular listener:
///   this.hubConnection.on('DriverArrived', (dto: DriverArrivedDto) => { ... });
/// </summary>
public sealed record DriverArrivedDto
{
    public string   TrackingNumber { get; init; } = default!;

    /// <summary>
    /// UTC timestamp when the driver triggered the Arrived status.
    /// </summary>
    public DateTime Timestamp      { get; init; }

    /// <summary>
    /// Driver's GPS coordinates at the moment of arrival.
    /// Nullable — coordinates may be missing if the booking had no lat/lng.
    /// Angular can use these to snap the map pin to the final position.
    /// </summary>
    public double?  DriverLat      { get; init; }
    public double?  DriverLng      { get; init; }
}
