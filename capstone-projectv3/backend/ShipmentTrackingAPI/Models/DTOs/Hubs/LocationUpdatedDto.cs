namespace ShipmentTrackingAPI.Hubs.DTOs;

/// <summary>
/// Broadcast to all members of the shipment's SignalR group
/// every GPS tick while the shipment is InTransit.
///
/// Sent by: GpsSimulationService → ITrackingService.BroadcastLocationUpdateAsync()
/// Received by: all group members (Customer/Sender, Recipient, Driver)
///
/// Angular listener:
///   this.hubConnection.on('LocationUpdated', (dto: LocationUpdatedDto) => { ... });
/// </summary>
public sealed record LocationUpdatedDto
{
    /// <summary>
    /// The shipment's public tracking number. e.g. "TRK-A3X9B1"
    /// Angular uses this to confirm the event belongs to the correct shipment
    /// in case a client is subscribed to multiple groups.
    /// </summary>
    public string   TrackingNumber { get; init; } = default!;

    public double   Latitude       { get; init; }
    public double   Longitude      { get; init; }

    /// <summary>
    /// UTC timestamp of when the coordinates were written to DriverProfile.
    /// Angular can use this to display "Last updated X seconds ago".
    /// </summary>
    public DateTime Timestamp      { get; init; }
}
