using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.BackgroundServices;

/// <summary>
/// Hosted background service that simulates GPS movement for every
/// shipment currently in InTransit status.
///
/// CHANGE FROM ORIGINAL VERSION
/// ─────────────────────────────
/// The original GpsSimulationService injected IHubContext directly and
/// called _hubContext.Clients.Group(...).LocationUpdated(...) itself.
///
/// This version instead calls ITrackingService.BroadcastLocationUpdateAsync().
/// Why: TrackingService is now the single place all SignalR sends happen.
/// This is consistent with how ShipmentService triggers StatusUpdated,
/// DriverArrived, etc. — all go through ITrackingService, not IHubContext.
///
/// THREADING
/// ─────────
/// Uses await Task.Delay (non-blocking). Never Thread.Sleep.
/// Runs on a background thread — does NOT block the ASP.NET Core pipeline.
///
/// SCOPED SERVICES IN A SINGLETON
/// ───────────────────────────────
/// BackgroundService is a singleton. AppDbContext is scoped.
/// We resolve a new IServiceScope per tick — standard pattern.
/// ITrackingService is a singleton — injected directly into constructor.
/// </summary>
public sealed class GpsSimulationService : BackgroundService
{
    private readonly int    _tickIntervalSeconds;
    private readonly double _stepFraction;

    /// <summary>
    /// Stop moving the pin once within 50m of the dropoff.
    /// Prevents floating-point jitter on the final ticks.
    /// </summary>
    private const double ArrivalThresholdMetres = 50.0;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITrackingService     _tracking;   // singleton — safe to inject directly
    private readonly ILogger<GpsSimulationService> _logger;

    public GpsSimulationService(
        IServiceScopeFactory scopeFactory,
        ITrackingService     tracking,
        ILogger<GpsSimulationService> logger,
        IConfiguration configuration)
    {
        _scopeFactory        = scopeFactory;
        _tracking            = tracking;
        _logger              = logger;
        _tickIntervalSeconds = configuration.GetValue<int>   ("GpsSimulation:TickIntervalSeconds", 5);
        _stepFraction        = configuration.GetValue<double>("GpsSimulation:StepFraction",        0.05);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "GpsSimulationService started. Tick={Interval}s StepFraction={Step}",
            _tickIntervalSeconds, _stepFraction);

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
                // Log and continue — one bad tick must never crash the service.
                _logger.LogError(ex, "GpsSimulationService: tick failed. Retrying next interval.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_tickIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("GpsSimulationService stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Query only InTransit shipments — never scan the whole table.
        var shipments = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.InTransit && s.DriverId != null)
            .Select(s => new InTransitProjection
            {
                ShipmentId      = s.Id,
                TrackingNumber  = s.TrackingNumber,
                DriverProfileId = s.Driver!.DriverProfileUser!.Id,
                CurrentLat      = s.Driver.DriverProfileUser.CurrentLat,
                CurrentLng      = s.Driver.DriverProfileUser.CurrentLng,
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

        if (shipments.Count == 0) return;

        _logger.LogInformation(
            "GpsSimulationService: tick — processing {Count} InTransit shipment(s).",
            shipments.Count);

        // Collect broadcasts — do all DB writes first, then all SignalR sends.
        var broadcasts = new List<(string TrackingNumber, double Lat, double Lng)>(shipments.Count);

        foreach (var s in shipments)
        {
            if (!IsValidCoord(s.CurrentLat, s.CurrentLng) ||
                !IsValidCoord(s.DropoffLat, s.DropoffLng))
            {
                _logger.LogWarning(
                    "GpsSimulationService: shipment {TN} — missing coordinates, skipping tick.",
                    s.TrackingNumber);
                continue;
            }

            var currLat  = s.CurrentLat!.Value;
            var currLng  = s.CurrentLng!.Value;
            var destLat  = s.DropoffLat!.Value;
            var destLng  = s.DropoffLng!.Value;

            var distance = HaversineMetres(currLat, currLng, destLat, destLng);

            if (distance <= ArrivalThresholdMetres)
            {
                // Pin is at destination — broadcast current position without moving.
                _logger.LogDebug(
                    "GpsSimulationService: shipment {TN} — driver within {D:F0}m, pin locked.",
                    s.TrackingNumber, distance);
                broadcasts.Add((s.TrackingNumber, currLat, currLng));
                continue;
            }

            // Interpolate one step toward dropoff.
            var newLat = Lerp(currLat, destLat, _stepFraction);
            var newLng = Lerp(currLng, destLng, _stepFraction);

            // ExecuteUpdateAsync — bulk UPDATE without loading the full entity.
            await db.DriverProfiles
                .Where(dp => dp.Id == s.DriverProfileId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(dp => dp.CurrentLat, newLat)
                    .SetProperty(dp => dp.CurrentLng, newLng),
                    ct);

            _logger.LogDebug(
                "GpsSimulationService: shipment {TN} — ({OLat:F6},{OLng:F6}) → ({NLat:F6},{NLng:F6}), " +
                "remaining {Rem:F0}m.",
                s.TrackingNumber, currLat, currLng, newLat, newLng,
                HaversineMetres(newLat, newLng, destLat, destLng));

            broadcasts.Add((s.TrackingNumber, newLat, newLng));
        }

        // All DB writes done — now broadcast via TrackingService.
        foreach (var (trackingNumber, lat, lng) in broadcasts)
        {
            await _tracking.BroadcastLocationUpdateAsync(trackingNumber, lat, lng);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsValidCoord(double? lat, double? lng)
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

    // ── Projection ───────────────────────────────────────────────────────────

    private sealed class InTransitProjection
    {
        public int     ShipmentId      { get; init; }
        public string  TrackingNumber  { get; init; } = default!;
        public int     DriverProfileId { get; init; }
        public double? CurrentLat      { get; init; }
        public double? CurrentLng      { get; init; }
        public double? DropoffLat      { get; init; }
        public double? DropoffLng      { get; init; }
    }
}