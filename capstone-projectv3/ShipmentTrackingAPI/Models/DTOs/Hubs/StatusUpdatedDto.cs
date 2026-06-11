namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Broadcast to all members of the shipment's SignalR group
/// whenever the shipment status changes.
///
/// Sent by: ShipmentService → ITrackingService.BroadcastStatusUpdateAsync()
///          on every transition: Assigned, PickedUp, InTransit, Arrived,
///          Delivered, Cancelled, FailedDelivery.
///
/// Received by: all group members (Customer/Sender, Recipient, Driver)
///
/// Angular listener:
///   this.hubConnection.on('StatusUpdated', (dto: StatusUpdatedDto) => { ... });
/// </summary>
public sealed record StatusUpdatedDto
{
    public string   TrackingNumber { get; init; } = default!;

    /// <summary>
    /// The new status as a string matching the ShipmentStatus enum name.
    /// e.g. "Assigned", "PickedUp", "InTransit", "Arrived", "Delivered"
    /// Angular maps this string to its local ShipmentStatus enum for display.
    /// </summary>
    public string   NewStatus      { get; init; } = default!;

    /// <summary>
    /// Human-readable description of the event.
    /// e.g. "Driver assigned to your shipment."
    ///      "Parcel collected from sender — POP confirmed."
    ///      "Driver is on the way."
    /// Displayed in the shipment event timeline on the tracking page.
    /// </summary>
    public string   Description    { get; init; } = default!;

    /// <summary>
    /// UTC timestamp of the transition. Angular renders this in the timeline.
    /// </summary>
    public DateTime Timestamp      { get; init; }
}
