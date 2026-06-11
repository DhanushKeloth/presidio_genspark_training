using ShipmentTrackingAPI.Hubs.DTOs;

namespace ShipmentTrackingAPI.Hubs;

/// <summary>
/// Strongly-typed client interface for TrackingHub.
///
/// TrackingHub extends Hub<ITrackingClient> instead of the untyped Hub base.
/// This means IHubContext<TrackingHub, ITrackingClient> gives compile-time
/// checking on event names and payload types everywhere it is injected
/// (TrackingService, and previously GpsSimulationService).
///
/// Every method here maps to a SignalR event that Angular listens for.
/// The C# method name IS the event name string — they must match exactly.
///
///   C# method name        Angular listener string
///   ─────────────────     ──────────────────────────────
///   LocationUpdated    →  hubConnection.on('LocationUpdated', ...)
///   StatusUpdated      →  hubConnection.on('StatusUpdated', ...)
///   PickupOtpGenerated →  hubConnection.on('PickupOtpGenerated', ...)
///   DeliveryOtpGenerated → hubConnection.on('DeliveryOtpGenerated', ...)
///   OtpRegenerated     →  hubConnection.on('OtpRegenerated', ...)
///   DriverArrived      →  hubConnection.on('DriverArrived', ...)
///   ShipmentDelivered  →  hubConnection.on('ShipmentDelivered', ...)
///
/// All methods return Task — required by the SignalR typed hub contract.
/// </summary>
public interface ITrackingClient
{
    /// <summary>
    /// GPS coordinates update. Broadcast to all group members every 5 seconds
    /// while shipment status = InTransit.
    /// Sender: GpsSimulationService via TrackingService.
    /// </summary>
    Task LocationUpdated(LocationUpdatedDto dto);

    /// <summary>
    /// Shipment status changed. Broadcast to all group members on every
    /// status transition (Assigned, PickedUp, InTransit, Arrived, etc.).
    /// Sender: ShipmentService via TrackingService.
    /// </summary>
    Task StatusUpdated(StatusUpdatedDto dto);

    /// <summary>
    /// Pickup OTP generated. Sent to the Sender's connectionId ONLY.
    /// Never broadcast to the full group — Driver must not see the code.
    /// Sender: ShipmentService via TrackingService.PushOtpToSenderAsync().
    /// </summary>
    Task PickupOtpGenerated(OtpGeneratedDto dto);

    /// <summary>
    /// Delivery OTP generated. Sent to the Recipient's connectionId ONLY.
    /// Never broadcast to the full group — Driver must not see the code.
    /// Sender: ShipmentService via TrackingService.PushOtpToRecipientAsync().
    /// </summary>
    Task DeliveryOtpGenerated(OtpGeneratedDto dto);

    /// <summary>
    /// OTP was regenerated. Broadcast to all group members — WITHOUT the code.
    /// Tells the Driver "a new OTP was sent to the customer."
    /// The new code is sent separately via PickupOtpGenerated/DeliveryOtpGenerated
    /// to the correct individual connection.
    /// Sender: ShipmentService via TrackingService.BroadcastOtpRegeneratedAsync().
    /// </summary>
    Task OtpRegenerated(OtpRegeneratedDto dto);

    /// <summary>
    /// Driver reached the Recipient's address. Broadcast to all group members.
    /// Angular prepares the Delivery OTP display panel on receipt.
    /// Sender: ShipmentService via TrackingService.BroadcastDriverArrivedAsync().
    /// </summary>
    Task DriverArrived(DriverArrivedDto dto);

    /// <summary>
    /// Delivery OTP verified — shipment is Delivered. Broadcast to all group members.
    /// Angular shows success screen and calls LeaveShipmentGroup on receipt.
    /// Sender: ShipmentService via TrackingService.BroadcastDeliverySuccessAsync().
    /// </summary>
    Task ShipmentDelivered(ShipmentDeliveredDto dto);
}
