namespace ShipmentTrackingAPI.Interfaces;

/// <summary>
/// Contract for all SignalR real-time broadcasts in the system.
///
/// Implemented by TrackingService (singleton).
/// Called by: GpsSimulationService, ShipmentService.
/// Never called from TrackingHub — the hub only handles
/// client → server calls (Join/Leave group).
/// </summary>
public interface ITrackingService
{
    // ── Connection registry ──────────────────────────────────────────────────

    /// <summary>
    /// Called by TrackingHub.OnConnectedAsync.
    /// Stores userId → connectionId so targeted OTP sends are possible.
    /// </summary>
    void RegisterConnection(int userId, string connectionId);

    /// <summary>
    /// Called by TrackingHub.OnDisconnectedAsync.
    /// Removes stale connectionId from the registry.
    /// </summary>
    void RemoveConnection(string connectionId);

    // ── Group broadcasts ─────────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts GPS coordinates to all members of the shipment's SignalR group.
    /// Called by GpsSimulationService every tick while shipment is InTransit.
    /// </summary>
    Task BroadcastLocationUpdateAsync(string trackingNumber, double lat, double lng);

    /// <summary>
    /// Broadcasts a status change to all members of the shipment's SignalR group.
    /// Called by ShipmentService on every status transition.
    /// </summary>
    Task BroadcastStatusUpdateAsync(string trackingNumber, string newStatus, string description);

    /// <summary>
    /// Broadcasts driver arrival event to all group members.
    /// Called when driver updates status to Arrived.
    /// </summary>
    Task BroadcastDriverArrivedAsync(string trackingNumber, double lat, double lng);

    /// <summary>
    /// Broadcasts delivery success to all group members.
    /// Called when delivery OTP is verified. Angular clients leave the group after this.
    /// </summary>
    Task BroadcastDeliverySuccessAsync(string trackingNumber);

    /// <summary>
    /// Broadcasts OTP regeneration notification to all group members.
    /// Does NOT include the OTP code — that is sent separately via the Push methods below.
    /// Allows the Driver's screen to show "New OTP sent to customer" without seeing the code.
    /// </summary>
    Task BroadcastOtpRegeneratedAsync(string trackingNumber, string otpType, DateTime expiresAt);

    // ── Targeted OTP pushes (specific user only, never the whole group) ───────

    /// <summary>
    /// Sends the Pickup OTP to the Sender's connection only.
    /// senderUserId is used to look up the connectionId in the registry.
    /// The Driver and other group members do NOT receive this event.
    /// </summary>
    Task PushOtpToSenderAsync(int senderUserId, string trackingNumber, string otpCode, DateTime expiresAt);

    /// <summary>
    /// Sends the Delivery OTP to the Recipient's connection only.
    /// recipientUserId is used to look up the connectionId in the registry.
    /// The Driver and other group members do NOT receive this event.
    /// </summary>
    Task PushOtpToRecipientAsync(int recipientUserId, string trackingNumber, string otpCode, DateTime expiresAt);
}