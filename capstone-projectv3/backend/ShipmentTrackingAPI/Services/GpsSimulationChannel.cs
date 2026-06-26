using System.Threading.Channels;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;

namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Singleton in-process pub/sub channel backed by System.Threading.Channels.
///
/// UnboundedChannel — no backpressure needed: GPS events are low-frequency
/// (only triggered by explicit driver status transitions, not per request).
///
/// SingleReader=true — only GpsSimulationService reads.
/// SingleWriter=false — ShipmentService and AdminService both write.
/// </summary>
public sealed class GpsSimulationChannel : IGpsSimulationChannel
{
    private readonly Channel<GpsSimulationEvent> _channel =
        Channel.CreateUnbounded<GpsSimulationEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });

    public ChannelReader<GpsSimulationEvent> Reader => _channel.Reader;

    /// <summary>
    /// Fire-and-forget publish. TryWrite on an unbounded channel never fails.
    /// </summary>
    public void Publish(GpsSimulationEvent evt)
        => _channel.Writer.TryWrite(evt);
}
