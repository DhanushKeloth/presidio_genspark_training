namespace ShipmentTrackingAPI.Models;

/// <summary>
/// Discriminates between starting and stopping GPS simulation for a shipment.
/// </summary>
public enum GpsEventType { Started, Stopped }

/// <summary>
/// Published to IGpsSimulationChannel by ShipmentService whenever a shipment
/// transitions into or out of InTransit status.
///
/// Started → GpsSimulationService adds the shipment to its in-memory active set.
/// Stopped → GpsSimulationService removes it. No DB call happens while the set is empty.
/// </summary>
public sealed class GpsSimulationEvent
{
    public GpsEventType EventType      { get; init; }
    public int          ShipmentId     { get; init; }
    public string       TrackingNumber { get; init; } = default!;

    // ── Only populated for Started events ────────────────────────────────────
    public int    DriverProfileId { get; init; }
    public double CurrentLat      { get; init; }
    public double CurrentLng      { get; init; }
    public double DropoffLat      { get; init; }
    public double DropoffLng      { get; init; }
}
