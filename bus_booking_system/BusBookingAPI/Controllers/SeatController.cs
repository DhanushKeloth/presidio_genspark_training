using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/seats")]
[Authorize(Roles = "User")]
public class SeatController : ControllerBase
{
    private readonly ISeatService _seatService;

    public SeatController(ISeatService seatService)
    {
        _seatService = seatService;
    }

    // POST /api/v1/seats/lock
    [HttpPost("lock")]
    public async Task<IActionResult> LockSeats([FromBody] SeatLockRequest request)
    {
        if (request.SeatIds == null || request.SeatIds.Count == 0)
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "seat_ids must not be empty", TraceId = HttpContext.TraceIdentifier });

        var userId = GetCurrentUserId();
        var (success, response, message) = await _seatService.LockSeatsAsync(userId, request);

        if (!success)
            return Conflict(new ErrorResponse { Status = 409, Error = "CONFLICT", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, response);
    }

    // DELETE /api/v1/seats/lock
    [HttpDelete("lock")]
    public async Task<IActionResult> UnlockSeats([FromBody] SeatUnlockRequest request)
    {
        var userId = GetCurrentUserId();
        var (success, message) = await _seatService.UnlockSeatsAsync(userId, request);

        if (!success)
            return StatusCode(403, new ErrorResponse { Status = 403, Error = "FORBIDDEN", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(new { message });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }
}
