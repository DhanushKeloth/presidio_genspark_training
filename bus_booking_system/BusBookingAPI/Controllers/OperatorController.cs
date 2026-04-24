using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/operator")]
[Authorize(Roles = "Operator")]
public class OperatorController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public OperatorController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // GET /api/v1/operator/bookings
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromQuery] Guid? bus_id,
        [FromQuery] DateOnly? journey_date,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        var operatorId = GetCurrentUserId();
        var result = await _bookingService.GetOperatorBookingsAsync(
            operatorId, bus_id, journey_date, status, page, Math.Min(page_size, 100));
        return Ok(result);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }
}
