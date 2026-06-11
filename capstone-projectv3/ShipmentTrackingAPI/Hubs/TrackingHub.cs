using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShipmentTrackingAPI.Hubs.DTOs;
using ShipmentTrackingAPI.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ShipmentTrackingAPI.Hubs;

/// <summary>
/// The single SignalR hub in the project.
///
/// Every real-time event — GPS coordinates, status transitions,
/// OTP delivery, driver arrival, and delivery confirmation — is
/// routed through this hub because they all relate to one domain:
/// tracking a specific shipment.
///
/// CONNECTION MODEL
/// ────────────────
/// Group name convention : "shipment-{trackingNumber}"
///                         e.g.  "shipment-TRK-A3X9B1"
///
/// When a client opens a tracking page they call JoinShipmentGroup.
/// When they leave (delivery confirmed, page close, navigation) they
/// call LeaveShipmentGroup or the browser fires OnDisconnectedAsync.
///
/// AUTHENTICATION
/// ──────────────
/// [Authorize] is applied at the hub level — every connection must
/// carry a valid JWT. The public /api/track/{trackingNumber} REST
/// endpoint handles unauthenticated tracking lookups; the hub is
/// only for live push events to authenticated users.
///
/// OTP SECURITY
/// ────────────
/// OTP codes are NEVER broadcast to the group. They are sent via
/// Clients.Client(connectionId) to the specific Sender (Pickup OTP)
/// or Recipient (Delivery OTP). The Driver is intentionally excluded.
/// TrackingService maintains the userId → connectionId mapping that
/// makes targeted sends possible.
///
/// THREAD SAFETY
/// ─────────────
/// OnConnectedAsync and OnDisconnectedAsync can fire concurrently.
/// TrackingService uses a ConcurrentDictionary internally — no
/// locking is required here.
///
/// CLIENT-SIDE EVENTS (Angular subscribes to these)
/// ─────────────────────────────────────────────────
///   LocationUpdated      — driver GPS coordinates (every 5 s)
///   StatusUpdated        — any shipment status change
///   DriverArrived        — driver reached destination
///   DeliverySuccess      — delivery OTP verified, shipment complete
///   PickupOtpReceived    — sent to Sender only
///   DeliveryOtpReceived  — sent to Recipient only
/// </summary>
[Authorize]
public class TrackingHub : Hub<ITrackingClient>
{
    private readonly ITrackingService _tracking;
    private readonly ILogger<TrackingHub> _logger;

    public TrackingHub(
        ITrackingService tracking,
        ILogger<TrackingHub> logger)
    {
        _tracking = tracking;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────
    //  CONNECTION LIFECYCLE
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called automatically by SignalR when a client connects.
    ///
    /// Registers the userId → connectionId mapping in TrackingService
    /// so that OTP codes can be pushed to the correct browser tab.
    ///
    /// userId is extracted from the JWT sub claim that was validated
    /// by the [Authorize] attribute before the connection was accepted.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();

        if (userId.HasValue)
        {
            _tracking.RegisterConnection(userId.Value, Context.ConnectionId);

            _logger.LogDebug(
                "TrackingHub: user {UserId} connected — connectionId {ConnId}",
                userId.Value, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called automatically by SignalR when a client disconnects
    /// (page close, navigation away, network drop, explicit disconnect).
    ///
    /// Removes the connectionId from the TrackingService registry
    /// to prevent stale entries from building up.
    ///
    /// The client is automatically removed from all groups by SignalR —
    /// no manual group removal is needed here.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _tracking.RemoveConnection(Context.ConnectionId);

        var userId = GetUserId();

        _logger.LogDebug(
            "TrackingHub: user {UserId} disconnected — connectionId {ConnId}. " +
            "Exception: {Exception}",
            userId?.ToString() ?? "unknown",
            Context.ConnectionId,
            exception?.Message ?? "none");

        await base.OnDisconnectedAsync(exception);
    }

    // ─────────────────────────────────────────────────────────
    //  CLIENT-INVOKABLE METHODS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the calling connection to the shipment's SignalR group.
    ///
    /// Called by the Angular client when:
    ///   - Customer opens the tracking page for their shipment.
    ///   - Recipient opens the tracking page via TrackingNumber.
    ///   - Driver opens the active job screen.
    ///
    /// Once joined, the connection receives:
    ///   LocationUpdated, StatusUpdated, DriverArrived, DeliverySuccess.
    /// OTP events are pushed to the individual connectionId — joining
    /// the group is not enough to receive OTP codes.
    ///
    /// Angular call:
    ///   await hubConnection.invoke("JoinShipmentGroup", trackingNumber);
    /// </summary>
    public async Task JoinShipmentGroup(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            _logger.LogWarning(
                "TrackingHub: JoinShipmentGroup called with empty trackingNumber " +
                "by connectionId {ConnId}", Context.ConnectionId);
            return;
        }


        var groupName = GroupName(trackingNumber);
        Console.WriteLine($"[HUB] ConnectionId={Context.ConnectionId} joining group='{groupName}'");

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "TrackingHub: connectionId {ConnId} joined group {Group}",
            Context.ConnectionId, groupName);

        // Acknowledge so the Angular client knows the join succeeded
        // and can start rendering the live tracking UI
        await Clients.Caller.StatusUpdated(new StatusUpdatedDto
        {
            TrackingNumber = trackingNumber,
            NewStatus = "Connected",
            Description = "Joined tracking group. Live updates active.",
            Timestamp = DateTime.UtcNow,
        });
    }

    /// <summary>
    /// Removes the calling connection from the shipment's SignalR group.
    ///
    /// Called by the Angular client when:
    ///   - DeliverySuccess event is received (automatic leave).
    ///   - User navigates away from the tracking page.
    ///   - Driver completes a delivery and returns to the job queue.
    ///
    /// Explicit group leave is preferable to relying on disconnect alone
    /// because the user may stay connected to the hub but stop tracking
    /// a specific shipment (e.g. navigates to their shipment list).
    ///
    /// Angular call:
    ///   await hubConnection.invoke("LeaveShipmentGroup", trackingNumber);
    /// </summary>
    public async Task LeaveShipmentGroup(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return;

        var groupName = GroupName(trackingNumber);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "TrackingHub: connectionId {ConnId} left group {Group}",
            Context.ConnectionId, groupName);
    }

    // ─────────────────────────────────────────────────────────
    //  PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the group name from a TrackingNumber.
    /// Convention: "shipment-{trackingNumber}"
    /// Must match the same convention used in TrackingService.
    /// </summary>
    private static string GroupName(string trackingNumber)
        => $"shipment-{trackingNumber}";

    /// <summary>
    /// Reads the authenticated user's id from the JWT sub claim.
    /// Returns null if the claim is absent or cannot be parsed
    /// (should not happen given [Authorize] — defensive only).
    /// </summary>
    private int? GetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(raw, out var id) ? id : null;
    }
}