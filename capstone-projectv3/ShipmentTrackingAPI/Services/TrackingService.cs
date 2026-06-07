using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using ShipmentTrackingAPI.Hubs;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.Services;

/// <summary>
/// Abstracts all SignalR communication so no other service ever directly
/// references IHubContext or knows that SignalR exists.
///
/// Two responsibilities:
///
///   1. Connection registry — a ConcurrentDictionary maps userId → connectionId.
///      Populated by TrackingHub.OnConnectedAsync, cleared on disconnect.
///      This is how OTP codes reach the correct browser tab without being
///      broadcast to the entire group.
///
///   2. Event dispatch — every push to SignalR goes through a named method
///      here. Each method name corresponds exactly to the Angular client-side
///      event that the SignalR service subscribes to.
///
/// Thread safety:
///   ConcurrentDictionary handles concurrent access from:
///     - GpsSimulationService (background thread, every 5 seconds)
///     - TrackingHub lifecycle callbacks (connection/disconnect events)
///     - OtpService (HTTP request threads)
///
/// SignalR group naming: "shipment-{trackingNumber}"
/// Example:              "shipment-TRK-A3X9B1"
/// </summary>
public class TrackingService : ITrackingService
{
    private readonly IHubContext<TrackingHub> _hub;
    private readonly ILogger<TrackingService> _logger;

    // userId → connectionId
    // Newest connection wins if a user opens multiple tabs.
    private readonly ConcurrentDictionary<int, string> _connections = new();

    // trackingNumber → (senderId, recipientId)
    // Registered at booking time so OTP pushes avoid DB round-trips.
    // recipientId = 0 when recipient is not a registered user.
    private readonly ConcurrentDictionary<string, (int SenderId, int RecipientId)>
        _shipmentParties = new();

    public TrackingService(
        IHubContext<TrackingHub> hub,
        ILogger<TrackingService> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────
    //  CONNECTION REGISTRY
    // ─────────────────────────────────────────────────────────

    public void RegisterConnection(int userId, string connectionId)
    {
        _connections[userId] = connectionId;
        _logger.LogDebug(
            "TrackingService: registered userId {UserId} → {ConnId}",
            userId, connectionId);
    }

    public void RemoveConnection(string connectionId)
    {
        var entry = _connections.FirstOrDefault(kv => kv.Value == connectionId);
        if (entry.Key != 0)
        {
            _connections.TryRemove(entry.Key, out _);
            _logger.LogDebug(
                "TrackingService: removed connectionId {ConnId} for userId {UserId}",
                connectionId, entry.Key);
        }
    }

    /// <summary>
    /// Registers Sender and Recipient user IDs for a shipment.
    /// Called by ShipmentService.BookShipmentAsync so OTP pushes
    /// can target the correct connection without a DB query.
    /// </summary>
    public void RegisterShipmentParties(
        string trackingNumber,
        int senderUserId,
        int recipientUserId)
        => _shipmentParties[trackingNumber] = (senderUserId, recipientUserId);

    // ─────────────────────────────────────────────────────────
    //  GROUP BROADCASTS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// GPS coordinate push — called by GpsSimulationService every 5 seconds.
    /// Angular event: "LocationUpdated"
    /// </summary>
    public async Task BroadcastLocationUpdateAsync(
        string trackingNumber,
        double lat,
        double lng)
        => await _hub.Clients
            .Group(Group(trackingNumber))
            .SendAsync("LocationUpdated", new
            {
                trackingNumber,
                latitude  = lat,
                longitude = lng,
                timestamp = DateTime.UtcNow
            });

    /// <summary>
    /// Status change broadcast — called on every transition.
    /// Angular event: "StatusUpdated"
    /// </summary>
    public async Task BroadcastStatusUpdateAsync(
        string trackingNumber,
        string newStatus,
        string description)
        => await _hub.Clients
            .Group(Group(trackingNumber))
            .SendAsync("StatusUpdated", new
            {
                trackingNumber,
                status      = newStatus,
                description,
                timestamp   = DateTime.UtcNow
            });

    /// <summary>
    /// Driver reached destination — includes coordinates for map pin.
    /// Angular event: "DriverArrived"
    /// </summary>
    public async Task BroadcastDriverArrivedAsync(
        string trackingNumber,
        double lat,
        double lng)
        => await _hub.Clients
            .Group(Group(trackingNumber))
            .SendAsync("DriverArrived", new
            {
                trackingNumber,
                driverLatitude  = lat,
                driverLongitude = lng,
                timestamp       = DateTime.UtcNow
            });

    /// <summary>
    /// Final delivery confirmation — Angular clients leave group on receipt.
    /// Cleans up the party registry — shipment lifecycle complete.
    /// Angular event: "DeliverySuccess"
    /// </summary>
    public async Task BroadcastDeliverySuccessAsync(string trackingNumber)
    {
        await _hub.Clients
            .Group(Group(trackingNumber))
            .SendAsync("DeliverySuccess", new
            {
                trackingNumber,
                deliveredAt = DateTime.UtcNow
            });

        _shipmentParties.TryRemove(trackingNumber, out _);
    }

    // ─────────────────────────────────────────────────────────
    //  TARGETED PUSHES (one specific connection only)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Pickup OTP → Sender's browser tab only. Never to the group.
    /// Driver cannot intercept this from any group channel.
    /// Silent no-op if sender not currently connected.
    /// Angular event: "PickupOtpReceived"
    /// </summary>
    public async Task PushOtpToSenderAsync(
        string trackingNumber,
        string otpCode,
        DateTime expiresAt)
    {
        if (!_shipmentParties.TryGetValue(trackingNumber, out var parties)) return;
        if (!_connections.TryGetValue(parties.SenderId, out var connId))    return;

        await _hub.Clients
            .Client(connId)
            .SendAsync("PickupOtpReceived", new
            {
                trackingNumber,
                otpCode,
                expiresAt,
                expiresInSeconds = Math.Max(0, (int)(expiresAt - DateTime.UtcNow).TotalSeconds)
            });

        // Log type only — OTP code must never appear in logs (NFR5)
        _logger.LogInformation(
            "Pickup OTP pushed to sender for shipment {Tn}", trackingNumber);
    }

    /// <summary>
    /// Delivery OTP → Recipient's browser tab only. Never to the group.
    /// Silent no-op if recipient is not a registered user (id = 0)
    /// or not currently connected.
    /// Angular event: "DeliveryOtpReceived"
    /// </summary>
    public async Task PushOtpToRecipientAsync(
        string trackingNumber,
        string otpCode,
        DateTime expiresAt)
    {
        if (!_shipmentParties.TryGetValue(trackingNumber, out var parties)) return;
        if (parties.RecipientId == 0)                                       return;
        if (!_connections.TryGetValue(parties.RecipientId, out var connId)) return;

        await _hub.Clients
            .Client(connId)
            .SendAsync("DeliveryOtpReceived", new
            {
                trackingNumber,
                otpCode,
                expiresAt,
                expiresInSeconds = Math.Max(0, (int)(expiresAt - DateTime.UtcNow).TotalSeconds)
            });

        // Log type only — OTP code must never appear in logs (NFR5)
        _logger.LogInformation(
            "Delivery OTP pushed to recipient for shipment {Tn}", trackingNumber);
    }

    // ─────────────────────────────────────────────────────────
    //  PRIVATE
    // ─────────────────────────────────────────────────────────

    private static string Group(string trackingNumber)
        => $"shipment-{trackingNumber}";
}