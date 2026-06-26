using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.DTOs.Otp;
using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ShipmentTrackingAPI.Controllers;

/// <summary>
/// Owns all shipment-domain endpoints:
///
///   Customer  — book, list own, get detail, cancel, public track
///   Driver    — view queue, assign, status updates, OTP flow
///
/// The controller is intentionally thin — it validates the request,
/// extracts the caller's identity from JWT claims, delegates to the
/// relevant service, and returns the mapped HTTP response.
/// No business logic lives here.
///
/// OTP security note: the Driver submits OTPs through this controller
/// but never sees the code itself. The code is pushed directly to the
/// Sender/Recipient via SignalR in the service layer.
/// </summary>
[ApiController]
[Route("api/shipments")]
public class ShipmentController : ControllerBase
{
    private readonly IShipmentService _shipmentService;
    private readonly IOtpService _otpService;
    private readonly ILogger<ShipmentController> _logger;

    public ShipmentController(
        IShipmentService shipmentService,
        IOtpService otpService,
        ILogger<ShipmentController> logger)
    {
        _shipmentService = shipmentService;
        _otpService = otpService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    //  CUSTOMER — Booking and history
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/shipments
    /// Books a new shipment. Creates Shipment + ShipmentAddresses + ShipmentItems
    /// in one atomic transaction. Returns the TrackingNumber immediately.
    /// Idempotency guard: same pickup+dropoff within 60s returns 409.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> BookShipment(
        [FromBody] BookShipmentRequestDto request)
    {
        var customerId = GetUserId();

        _logger.LogInformation(
            "Shipment booking initiated by customer {CustomerId}", customerId);

        var result = await _shipmentService.BookShipmentAsync(customerId, request);

        _logger.LogInformation(
            "Shipment booked — TrackingNumber: {Tn} by customer {CustomerId}",
            result.TrackingNumber, customerId);

        return CreatedAtAction(
            nameof(GetShipmentById),
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// GET /api/shipments?page=1&amp;size=20&amp;status=InTransit
    /// Returns the calling customer's own shipments — paginated.
    /// Optional ?status= filter. Max page size 50 (enforced in service).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetMyShipments(
        [FromQuery] ShipmentQueryParams query)
    {
        var customerId = GetUserId();
        var result = await _shipmentService.GetCustomerShipmentsAsync(customerId, query);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/shipments/{id}
    /// Full shipment detail — addresses, items, OTP metadata (no codes), event timeline.
    /// Open to Customer, Driver, and Admin. Service layer enforces ownership.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetShipmentById(int id)
    {
        var requesterId = GetUserId();
        var role = GetUserRole();

        var result = await _shipmentService.GetShipmentByIdAsync(id, requesterId, role);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/shipments/{id}
    /// Customer cancels a shipment. Only permitted when status = Pending.
    /// Returns 409 if a driver has already been assigned.
    /// </summary>
    [HttpPut("{id:int}/cancel")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> CancelShipment(int id)
    {
        var customerId = GetUserId();
        var result = await _shipmentService.CancelShipmentAsync(id, customerId);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC — Tracking (no auth required)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/track/{trackingNumber}
    /// Public endpoint — no JWT required. Returns current status, driver
    /// coordinates if InTransit, and the full event timeline.
    /// OTP codes, recipient phone, and internal IDs are never exposed.
    /// Separate route prefix so it doesn't conflict with /api/shipments routes.
    /// </summary>
    [HttpGet("/api/track/{trackingNumber}")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackShipment(string trackingNumber)
    {
        var result = await _shipmentService.GetPublicTrackingAsync(trackingNumber);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER — Job queue and assignment
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/shipments/queue?page=1&amp;size=20
    /// Returns paginated Pending shipments available to claim.
    /// Area-level addresses only — no customer PII.
    /// Blocked if driver account is not Active (403 returned by service).
    /// Route must be declared before {id:int} to avoid ambiguity.
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetPendingQueue(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var driverId = GetUserId();
        var result = await _shipmentService.GetPendingQueueAsync(driverId, page, size);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/shipments/{id}/assign
    /// Driver self-assigns a Pending shipment. Runs inside a DB transaction
    /// with row-level locking to prevent two drivers claiming the same job.
    /// Returns 409 if the shipment was already assigned by another driver.
    /// </summary>
    [HttpPut("{id:int}/assign")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> AssignShipment(int id)
    {
        var driverId = GetUserId();
        var result = await _shipmentService.AssignDriverAsync(id, driverId);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER — Status transitions (state machine)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// PUT /api/shipments/{id}/status
    /// Body: { "newStatus": "InTransit" }
    /// Used for PickedUp → InTransit and InTransit → Arrived transitions.
    /// OTP-triggered transitions (→ PickedUp, → Delivered) use the OTP
    /// endpoints below — they are not routed through here.
    /// State machine guard rejects invalid transitions with 409.
    /// </summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateShipmentStatusDto request)
    {
        var driverId = GetUserId();
        var result = await _shipmentService.UpdateStatusAsync(id, driverId, request.NewStatus);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/shipments/{id}/fail-delivery
    /// Body: { "reason": "Recipient not available" }
    /// Driver marks a delivery as failed when at Arrived status.
    /// Resets driver op_status to Available. Broadcasts to tracking group.
    /// </summary>
    [HttpPost("{id:int}/fail-delivery")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> FailDelivery(
        int id,
        [FromBody] FailDeliveryRequestDto request)
    {
        var driverId = GetUserId();
        var result = await _shipmentService.FailDeliveryAsync(id, driverId, request.Reason);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER — Pickup OTP flow (Module 3)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/shipments/{id}/request-pickup-otp
    /// Driver requests the Pickup OTP when they arrive at the sender's address.
    /// Shipment must be in Assigned status.
    /// Generated OTP is pushed via SignalR to the Sender's screen only —
    /// the Driver never sees the code in the API response.
    /// </summary>
    [HttpPost("{id:int}/request-pickup-otp")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> RequestPickupOtp(int id)
    {
        var driverId = GetUserId();

        _logger.LogInformation(
            "Pickup OTP requested — shipment {Id} by driver {DriverId}", id, driverId);

        var result = await _otpService.RequestOtpAsync(id, OtpType.Pickup, driverId);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/shipments/{id}/verify-pickup-otp
    /// Body: { "otpCode": "7284" }
    /// Driver submits the 4-digit code verbally given by the Sender.
    /// On success: status → PickedUp, picked_up_at set, OTP cleared.
    /// On failure: remaining attempts returned.
    /// At 3 failures: 429 returned, regeneration required.
    /// </summary>
    [HttpPost("{id:int}/verify-pickup-otp")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> VerifyPickupOtp(
        int id,
        [FromBody] VerifyOtpRequestDto request)
    {
        var driverId = GetUserId();

        var result = await _otpService.VerifyOtpAsync(
            id, OtpType.Pickup, request.OtpCode, driverId);

        // HTTP 200 for both success and wrong-code — the DTO carries Success flag.
        // The client reads result.Success to decide what to show.
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER — Delivery OTP flow (Module 5)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/shipments/{id}/request-delivery-otp
    /// Driver requests the Delivery OTP after triggering Arrived.
    /// Shipment must be in Arrived status.
    /// Generated OTP is pushed via SignalR to the Recipient's screen only.
    /// </summary>
    [HttpPost("{id:int}/request-delivery-otp")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> RequestDeliveryOtp(int id)
    {
        var driverId = GetUserId();

        _logger.LogInformation(
            "Delivery OTP requested — shipment {Id} by driver {DriverId}", id, driverId);

        var result = await _otpService.RequestOtpAsync(id, OtpType.Delivery, driverId);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/shipments/{id}/verify-delivery-otp
    /// Body: { "otpCode": "5931" }
    /// Driver submits the 4-digit code verbally given by the Recipient.
    /// On success: status → Delivered, delivered_at set, OTP cleared,
    /// ShipmentDelivered broadcast to group. Both screens show success state.
    /// </summary>
    [HttpPost("{id:int}/verify-delivery-otp")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> VerifyDeliveryOtp(
        int id,
        [FromBody] VerifyOtpRequestDto request)
    {
        var driverId = GetUserId();

        var result = await _otpService.VerifyOtpAsync(
            id, OtpType.Delivery, request.OtpCode, driverId);

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER — OTP regeneration (both types)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/shipments/{id}/regenerate-otp
    /// Body: { "otpType": "Pickup" } or { "otpType": "Delivery" }
    /// Resets the OTP window with a new code and a fresh 15-minute expiry.
    /// Only callable after the window is locked (3 failed attempts) or expired.
    /// New code is pushed to the Sender (Pickup) or Recipient (Delivery) via SignalR.
    /// </summary>
    [HttpPost("{id:int}/regenerate-otp")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> RegenerateOtp(
        int id,
        [FromBody] RegenerateOtpRequestDto request)
    {
        var driverId = GetUserId();
        var result = await _otpService.RegenerateOtpAsync(id, request.OtpType, driverId);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════
    //  PRIVATE HELPERS — JWT claim extraction
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Extracts the authenticated user's integer id from the JWT sub claim.
    /// Throws if the claim is absent or unparseable — this should never
    /// happen given [Authorize] validates the token first.
    /// </summary>
    private int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(raw, out var id))
            throw new UnauthorizedException(
                "User ID claim is missing or invalid in the JWT.");

        return id;
    }

    /// <summary>
    /// Extracts the UserRole from the JWT role claim.
    /// Used by GetShipmentById to pass the caller's role to the service
    /// for ownership validation without an extra DB lookup.
    /// </summary>
    private UserRole GetUserRole()
    {
        var raw = User.FindFirstValue(ClaimTypes.Role);

        return Enum.TryParse<UserRole>(raw, out var role)
            ? role
            : UserRole.Customer;   // safe default for [Authorize] routes
    }


    [HttpPost("quote")]
    //[Authorize(Roles = "Customer")] // Protect it so only logged-in customers can get quotes
    public async Task<IActionResult> GetQuote([FromBody] BookShipmentRequestDto req)
    {
        var quote = await _shipmentService.GetShipmentQuoteAsync(req);
        return Ok(quote);
    }
}