using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ShipmentTrackingAPI.Hubs;
using ShipmentTrackingAPI.Hubs.DTOs;
using ShipmentTrackingAPI.Interfaces;   // ← matches ITrackingService namespace

namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Central SignalR broadcast service.
///
/// All server → client pushes in the system go through this class —
/// GpsSimulationService and ShipmentService both call methods here.
/// The hub itself only handles client → server calls
/// (JoinShipmentGroup / LeaveShipmentGroup).
///
/// RESPONSIBILITY SPLIT
/// ────────────────────
/// TrackingHub     : client → server  (Join/Leave group, connection lifecycle)
/// TrackingService : server → client  (all broadcasts, all targeted OTP pushes)
///
/// USERID → CONNECTIONID MAPPING
/// ──────────────────────────────
/// OTP codes must be pushed to a specific browser tab, not the whole group.
/// We maintain a ConcurrentDictionary<int, string> (userId → connectionId).
/// Populated in TrackingHub.OnConnectedAsync, cleaned up in OnDisconnectedAsync.
///
/// LIFETIME
/// ────────
/// Registered as a Singleton in Program.cs.
/// IHubContext<T> is also a singleton — safe to inject directly.
/// ConcurrentDictionary is thread-safe — no locks needed.
/// </summary>
public sealed class TrackingService : ITrackingService
{
    // ── userId → connectionId ─────────────────────────────────────────────────
    // For capstone simplicity: one connectionId per user (latest tab wins).
    // Production would use ConcurrentDictionary<int, HashSet<string>>.
    private readonly ConcurrentDictionary<int, string> _connections      = new();
    private readonly ConcurrentDictionary<string, int> _connectionToUser = new();

    private readonly IHubContext<TrackingHub, ITrackingClient> _hub;
    private readonly ILogger<TrackingService> _logger;

    public TrackingService(
        IHubContext<TrackingHub, ITrackingClient> hub,
        ILogger<TrackingService> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    // ── Connection registry ──────────────────────────────────────────────────

    public void RegisterConnection(int userId, string connectionId)
    {
        _connections[userId]          = connectionId;
        _connectionToUser[connectionId] = userId;

        _logger.LogDebug(
            "TrackingService: registered userId={UserId} → connectionId={ConnId}",
            userId, connectionId);
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connectionToUser.TryRemove(connectionId, out var userId))
        {
            // Only remove from _connections if this is still the current connectionId
            // for that user — they may have already reconnected with a new one.
            if (_connections.TryGetValue(userId, out var current) && current == connectionId)
                _connections.TryRemove(userId, out _);

            _logger.LogDebug(
                "TrackingService: removed connectionId={ConnId} (userId={UserId})",
                connectionId, userId);
        }
    }

    // ── Group broadcasts ─────────────────────────────────────────────────────

    public async Task BroadcastLocationUpdateAsync(
        string trackingNumber,
        double lat,
        double lng)
    {
        await _hub.Clients
            .Group(GroupName(trackingNumber))
            .LocationUpdated(new LocationUpdatedDto
            {
                TrackingNumber = trackingNumber,
                Latitude       = lat,
                Longitude      = lng,
                Timestamp      = DateTime.UtcNow,
            });

        _logger.LogDebug(
            "TrackingService: LocationUpdated → group={Group} ({Lat:F6}, {Lng:F6})",
            GroupName(trackingNumber), lat, lng);
    }

    public async Task BroadcastStatusUpdateAsync(
        string trackingNumber,
        string newStatus,
        string description)
    {
        Console.WriteLine($"[SERVICE] Broadcasting to group='{GroupName(trackingNumber)}'");

        await _hub.Clients
            .Group(GroupName(trackingNumber))
            .StatusUpdated(new StatusUpdatedDto
            {
                TrackingNumber = trackingNumber,
                NewStatus      = newStatus,
                Description    = description,
                Timestamp      = DateTime.UtcNow,
            });

        _logger.LogInformation(
            "TrackingService: StatusUpdated → group={Group}, status={Status}",
            GroupName(trackingNumber), newStatus);
    }

    public async Task BroadcastDriverArrivedAsync(
        string trackingNumber,
        double lat,
        double lng)
    {
        await _hub.Clients
            .Group(GroupName(trackingNumber))
            .DriverArrived(new DriverArrivedDto
            {
                TrackingNumber = trackingNumber,
                Timestamp      = DateTime.UtcNow,
                DriverLat      = lat,
                DriverLng      = lng,
            });

        _logger.LogInformation(
            "TrackingService: DriverArrived → group={Group}",
            GroupName(trackingNumber));
    }

    public async Task BroadcastDeliverySuccessAsync(string trackingNumber)
    {
        await _hub.Clients
            .Group(GroupName(trackingNumber))
            .ShipmentDelivered(new ShipmentDeliveredDto
            {
                TrackingNumber = trackingNumber,
                DeliveredAt    = DateTime.UtcNow,
            });

        _logger.LogInformation(
            "TrackingService: ShipmentDelivered → group={Group}",
            GroupName(trackingNumber));
    }

    public async Task BroadcastOtpRegeneratedAsync(
        string trackingNumber,
        string otpType,
        DateTime expiresAt)
    {
        await _hub.Clients
            .Group(GroupName(trackingNumber))
            .OtpRegenerated(new OtpRegeneratedDto
            {
                OtpType        = otpType,
                TrackingNumber = trackingNumber,
                ExpiresAt      = expiresAt,
            });

        _logger.LogInformation(
            "TrackingService: OtpRegenerated → group={Group}, type={OtpType}",
            GroupName(trackingNumber), otpType);
    }

    // ── Targeted OTP pushes ──────────────────────────────────────────────────

    public async Task PushOtpToSenderAsync(
        int      senderUserId,
        string   trackingNumber,
        string   otpCode,
        DateTime expiresAt)
    {
        var connectionId = GetConnectionId(senderUserId);

        if (connectionId is null)
        {
            _logger.LogWarning(
                "TrackingService: PushOtpToSenderAsync — userId={UserId} not connected. " +
                "Shipment={TrackingNumber}. SignalR push skipped.",
                senderUserId, trackingNumber);
            return;
        }

        await _hub.Clients
            .Client(connectionId)
            .PickupOtpGenerated(new OtpGeneratedDto
            {
                OtpType        = "Pickup",
                TrackingNumber = trackingNumber,
                OtpCode        = otpCode,
                ExpiresAt      = expiresAt,
            });

        _logger.LogInformation(
            "TrackingService: PickupOtpGenerated → userId={UserId}, shipment={TrackingNumber}",
            senderUserId, trackingNumber);
    }

    public async Task PushOtpToRecipientAsync(
        int      recipientUserId,
        string   trackingNumber,
        string   otpCode,
        DateTime expiresAt)
    {
        var connectionId = GetConnectionId(recipientUserId);

        if (connectionId is null)
        {
            _logger.LogWarning(
                "TrackingService: PushOtpToRecipientAsync — userId={UserId} not connected. " +
                "Shipment={TrackingNumber}. SignalR push skipped.",
                recipientUserId, trackingNumber);
            return;
        }

        await _hub.Clients
            .Client(connectionId)
            .DeliveryOtpGenerated(new OtpGeneratedDto
            {
                OtpType        = "Delivery",
                TrackingNumber = trackingNumber,
                OtpCode        = otpCode,
                ExpiresAt      = expiresAt,
            });

        _logger.LogInformation(
            "TrackingService: DeliveryOtpGenerated → userId={UserId}, shipment={TrackingNumber}",
            recipientUserId, trackingNumber);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string? GetConnectionId(int userId)
        => _connections.TryGetValue(userId, out var connId) ? connId : null;

    private static string GroupName(string trackingNumber)
        => $"shipment-{trackingNumber}";
}