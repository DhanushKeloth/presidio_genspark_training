using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/bookings")]
[Authorize(Roles = "User")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // POST /api/v1/bookings
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        if (request.Passengers == null || request.Passengers.Count == 0)
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "At least one passenger is required", TraceId = HttpContext.TraceIdentifier });

        var userId = GetCurrentUserId();
        var (success, response, message, statusCode) = await _bookingService.CreateBookingAsync(userId, request);

        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = statusCode == 409 ? "CONFLICT" : "BUSINESS_RULE", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, response);
    }

    // GET /api/v1/bookings
    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int page_size = 10)
    {
        var userId = GetCurrentUserId();
        var result = await _bookingService.GetUserBookingsAsync(userId, status, page, Math.Min(page_size, 50));
        return Ok(result);
    }

    // GET /api/v1/bookings/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBookingById(Guid id)
    {
        var userId = GetCurrentUserId();
        // Returns within the user's booking list — reuse history with exact match
        var result = await _bookingService.GetUserBookingsAsync(userId, null, 1, 100);
        var booking = result.Results.FirstOrDefault(b => b.BookingId == id);

        if (booking == null)
            return NotFound(new ErrorResponse { Status = 404, Error = "NOT_FOUND", Message = "Booking not found", TraceId = HttpContext.TraceIdentifier });

        return Ok(booking);
    }

    // POST /api/v1/bookings/{id}/cancel
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelBookingRequest? request)
    {
        var userId = GetCurrentUserId();
        var (success, message, statusCode) = await _bookingService.CancelBookingAsync(id, userId, request?.CancellationReason);

        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = statusCode == 422 ? "BUSINESS_RULE" : "NOT_FOUND", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(new { message, refund_status = "initiated" });
    }

    // GET /api/v1/bookings/{id}/ticket
    [HttpGet("{id:guid}/ticket")]
    public IActionResult GetTicket(Guid id)
    {
        // Phase 1: ticket PDF path placeholder
        return Ok(new { message = "Ticket PDF generation is available in Phase 2", booking_id = id });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }
}
