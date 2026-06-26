using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.BackgroundServices;

/// <summary>
/// Event-driven GPS simulation service.
///
/// HOW IT WORKS
/// ─────────────
/// Instead of querying the database every 5 seconds to find InTransit shipments,
/// this service subscribes to an in-process Channel (IGpsSimulationChannel).
///
///   ShipmentService publishes GpsStarted  → driver transitions to InTransit
///   ShipmentService publishes GpsStopped  → driver transitions to Arrived
///   AdminService    publishes GpsStopped  → admin overrides away from InTransit
///
/// The service maintains an in-memory ConcurrentDictionary of active shipments.
/// Each tick:
///   1. Drain all pending channel events (start/stop) — non-blocking.
///   2. If the dictionary is empty → SKIP (zero DB connections, zero SignalR calls).
///   3. If there are active shipments → interpolate GPS, write to DB, broadcast.
///
/// STARTUP RECOVERY
/// ─────────────────
/// On first start (or restart), we run ONE query to load any shipments that are
/// already InTransit. After that, no DB polling happens — only event-driven updates.
///
/// THREADING
/// ──────────
/// Uses await Task.Delay (non-blocking). Never Thread.Sleep.
/// ITrackingService is singleton — safe to inject directly.
/// AppDbContext is scoped — resolved per tick via IServiceScopeFactory.
/// </summary>
public sealed class GpsSimulationService : BackgroundService
{
    private readonly int    _tickIntervalSeconds;
    private readonly double _stepFraction;

    /// <summary>Stop moving the pin once within 50m of the dropoff.</summary>
    private const double ArrivalThresholdMetres = 50.0;

    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ITrackingService             _tracking;
    private readonly IGpsSimulationChannel        _channel;
    private readonly ILogger<GpsSimulationService> _logger;

    /// <summary>
    /// In-memory registry: shipmentId → active GPS state.
    /// Only shipments currently InTransit appear here.
    /// </summary>
    private readonly ConcurrentDictionary<int, ActiveShipmentState> _active = new();

    public GpsSimulationService(
        IServiceScopeFactory          scopeFactory,
        ITrackingService              tracking,
        IGpsSimulationChannel         channel,
        ILogger<GpsSimulationService> logger,
        IConfiguration                configuration)
    {
        _scopeFactory        = scopeFactory;
        _tracking            = tracking;
        _channel             = channel;
        _logger              = logger;
        _tickIntervalSeconds = configuration.GetValue<int>   ("GpsSimulation:TickIntervalSeconds", 5);
        _stepFraction        = configuration.GetValue<double>("GpsSimulation:StepFraction",        0.05);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GpsSimulationService started (event-driven pub/sub). " +
            "Tick={Interval}s, StepFraction={Step}",
            _tickIntervalSeconds, _stepFraction);

        // One-time startup query: load any shipments that were already InTransit
        // before this process started (handles restarts / hot reloads).
        await LoadExistingInTransitShipmentsAsync(stoppingToken);

        // Small startup delay so the API and SignalR hub are fully initialised.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GpsSimulationService: tick failed. Retrying next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_tickIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("GpsSimulationService stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        // ── Step 1: Drain all pending channel events (non-blocking) ──────────
        // Process every Start / Stop event that arrived since the last tick.
        while (_channel.Reader.TryRead(out var evt))
        {
            if (evt.EventType == GpsEventType.Started)
            {
                _active[evt.ShipmentId] = new ActiveShipmentState
                {
                    TrackingNumber  = evt.TrackingNumber,
                    DriverProfileId = evt.DriverProfileId,
                    CurrentLat      = evt.CurrentLat,
                    CurrentLng      = evt.CurrentLng,
                    DropoffLat      = evt.DropoffLat,
                    DropoffLng      = evt.DropoffLng,
                };

                _logger.LogInformation(
                    "GpsSimulationService: ▶ started simulating {TN} " +
                    "(driverProfile={DPId}, {ActiveCount} total active)",
                    evt.TrackingNumber, evt.DriverProfileId, _active.Count);
            }
            else // Stopped
            {
                if (_active.TryRemove(evt.ShipmentId, out _))
                    _logger.LogInformation(
                        "GpsSimulationService: ■ stopped simulating {TN} " +
                        "({ActiveCount} remaining active)",
                        evt.TrackingNumber, _active.Count);
            }
        }

        // ── Step 2: Nothing to do → skip ─────────────────────────────────────
        // Zero DB connections opened. Zero SignalR calls made.
        if (_active.IsEmpty)
        {
            _logger.LogDebug(
                "GpsSimulationService: no active InTransit shipments — tick skipped.");
            return;
        }

        _logger.LogInformation(
            "GpsSimulationService: tick — processing {Count} active InTransit shipment(s).",
            _active.Count);

        // ── Step 3: Interpolate GPS positions ─────────────────────────────────
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var broadcasts = new List<(string TrackingNumber, double Lat, double Lng)>(_active.Count);
        var toRemove   = new List<int>();

        foreach (var (shipmentId, state) in _active)
        {
            if (!IsValidCoord(state.CurrentLat, state.CurrentLng) ||
                !IsValidCoord(state.DropoffLat, state.DropoffLng))
            {
                _logger.LogWarning(
                    "GpsSimulationService: {TN} has invalid coordinates — removing from active set.",
                    state.TrackingNumber);
                toRemove.Add(shipmentId);
                continue;
            }

            var distance = HaversineMetres(
                state.CurrentLat, state.CurrentLng,
                state.DropoffLat, state.DropoffLng);

            if (distance <= ArrivalThresholdMetres)
            {
                // Pin is at destination — broadcast final position without moving.
                _logger.LogDebug(
                    "GpsSimulationService: {TN} — driver within {D:F0}m of destination. Pin locked.",
                    state.TrackingNumber, distance);
                broadcasts.Add((state.TrackingNumber, state.CurrentLat, state.CurrentLng));
                continue;
            }

            // Capture old position for logging
            var oldLat = state.CurrentLat;
            var oldLng = state.CurrentLng;

            // Interpolate one step toward dropoff
            var newLat = Lerp(state.CurrentLat, state.DropoffLat, _stepFraction);
            var newLng = Lerp(state.CurrentLng, state.DropoffLng, _stepFraction);

            // Update in-memory state for next tick
            state.CurrentLat = newLat;
            state.CurrentLng = newLng;

            // Bulk UPDATE — no entity load needed
            await db.DriverProfiles
                .Where(dp => dp.Id == state.DriverProfileId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(dp => dp.CurrentLat, newLat)
                    .SetProperty(dp => dp.CurrentLng, newLng),
                    ct);

            _logger.LogDebug(
                "GpsSimulationService: {TN} ({OLat:F6},{OLng:F6}) → ({NLat:F6},{NLng:F6}), {Rem:F0}m remaining.",
                state.TrackingNumber, oldLat, oldLng, newLat, newLng,
                HaversineMetres(newLat, newLng, state.DropoffLat, state.DropoffLng));

            broadcasts.Add((state.TrackingNumber, newLat, newLng));
        }

        // Remove any entries with invalid coordinates
        foreach (var id in toRemove)
            _active.TryRemove(id, out _);

        // ── Step 4: All DB writes done — now broadcast via SignalR ────────────
        foreach (var (trackingNumber, lat, lng) in broadcasts)
            await _tracking.BroadcastLocationUpdateAsync(trackingNumber, lat, lng);
    }

    /// <summary>
    /// One-time startup recovery query.
    /// Populates the in-memory active set with any shipments that are already
    /// InTransit. Runs exactly once on startup; after that all updates are event-driven.
    /// </summary>
    private async Task LoadExistingInTransitShipmentsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.Shipments
            .AsNoTracking()
            .Where(s => s.Status == ShipmentStatus.InTransit
                     && s.DriverId != null
                     && s.Driver!.DriverProfileUser!.CurrentLat != null
                     && s.Driver.DriverProfileUser.CurrentLng != null)
            .Select(s => new
            {
                s.Id,
                s.TrackingNumber,
                DriverProfileId = s.Driver!.DriverProfileUser!.Id,
                CurrentLat      = s.Driver.DriverProfileUser.CurrentLat!.Value,
                CurrentLng      = s.Driver.DriverProfileUser.CurrentLng!.Value,
                DropoffLat      = s.ShipmentAddresses
                                   .Where(a => a.AddressType == AddressType.Dropoff)
                                   .Select(a => a.Lat)
                                   .FirstOrDefault(),
                DropoffLng      = s.ShipmentAddresses
                                   .Where(a => a.AddressType == AddressType.Dropoff)
                                   .Select(a => a.Lng)
                                   .FirstOrDefault(),
            })
            .ToListAsync(ct);

        foreach (var s in existing)
        {
            if (s.DropoffLat == null || s.DropoffLng == null) continue;

            _active[s.Id] = new ActiveShipmentState
            {
                TrackingNumber  = s.TrackingNumber,
                DriverProfileId = s.DriverProfileId,
                CurrentLat      = s.CurrentLat,
                CurrentLng      = s.CurrentLng,
                DropoffLat      = s.DropoffLat.Value,
                DropoffLng      = s.DropoffLng.Value,
            };
        }

        _logger.LogInformation(
            "GpsSimulationService: startup recovery — loaded {Count} existing InTransit shipment(s).",
            _active.Count);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsValidCoord(double lat, double lng)
        => lat is >= -90  and <= 90
        && lng is >= -180 and <= 180;

    private static double Lerp(double from, double to, double t)
        => from + (to - from) * t;

    private static double HaversineMetres(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6_371_000;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double deg) => deg * Math.PI / 180.0;

    // ── Nested state class ────────────────────────────────────────────────────

    /// <summary>
    /// Mutable in-memory snapshot of one actively simulated shipment.
    /// CurrentLat / CurrentLng are updated each tick to avoid re-reading the DB.
    /// </summary>
    private sealed class ActiveShipmentState
    {
        public string TrackingNumber  { get; init; } = default!;
        public int    DriverProfileId { get; init; }
        public double CurrentLat      { get; set; }   // updated each tick
        public double CurrentLng      { get; set; }   // updated each tick
        public double DropoffLat      { get; init; }
        public double DropoffLng      { get; init; }
    }
}