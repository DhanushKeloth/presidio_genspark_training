using System.Threading.Channels;
using ShipmentTrackingAPI.Models;

namespace ShipmentTrackingAPI.Interfaces;

/// <summary>
/// In-process pub/sub channel for GPS simulation lifecycle events.
///
/// Producers: ShipmentService (InTransit / Arrived transitions)
///            AdminService    (override from InTransit to any other status)
///
/// Consumer:  GpsSimulationService (singleton background service)
///
/// Registered as a singleton so both scoped services (publishers) and the
/// singleton background service (consumer) share the same channel instance.
/// </summary>
public interface IGpsSimulationChannel
{
    /// <summary>
    /// Non-blocking publish. Safe to call from any thread / any lifetime.
    /// </summary>
    void Publish(GpsSimulationEvent evt);

    /// <summary>
    /// Read side — consumed exclusively by GpsSimulationService.
    /// </summary>
    ChannelReader<GpsSimulationEvent> Reader { get; }
}
