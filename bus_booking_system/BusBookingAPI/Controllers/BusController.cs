using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/buses")]
public class BusController : ControllerBase
{
    private readonly IBusService _busService;

    public BusController(IBusService busService)
    {
        _busService = busService;
    }

    // GET /api/v1/buses/search
    [HttpGet("search")]
    public async Task<IActionResult> SearchBuses([FromQuery] BusSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Destination))
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "source and destination are required", TraceId = HttpContext.TraceIdentifier });

        if (request.JourneyDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "journey_date must be today or future", TraceId = HttpContext.TraceIdentifier });

        request.PageSize = Math.Min(request.PageSize, 50);
        var result = await _busService.SearchBusesAsync(request);
        return Ok(result);
    }

    // GET /api/v1/buses/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBusDetails(Guid id, [FromQuery] DateOnly journey_date)
    {
        var result = await _busService.GetBusDetailsAsync(id, journey_date);
        if (result == null)
            return NotFound(new ErrorResponse { Status = 404, Error = "NOT_FOUND", Message = "Bus not found", TraceId = HttpContext.TraceIdentifier });

        return Ok(result);
    }

    // GET /api/v1/buses/routes/autocomplete
    [HttpGet("routes/autocomplete")]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _busService.GetAllRoutesAsync();
        return Ok(routes);
    }

    // POST /api/v1/buses  (Operator only)
    [HttpPost]
    [Authorize(Roles = "Operator")]
    public async Task<IActionResult> AddBus([FromBody] AddBusRequest request)
    {
        var operatorId = GetCurrentUserId();
        var (success, busId, message) = await _busService.AddBusAsync(operatorId, request);
        if (!success)
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, new { bus_id = busId, message });
    }

    // PUT /api/v1/buses/{id}  (Operator only)
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Operator")]
    public async Task<IActionResult> UpdateBus(Guid id, [FromBody] UpdateBusRequest request)
    {
        var operatorId = GetCurrentUserId();
        var (success, message) = await _busService.UpdateBusAsync(id, operatorId, request);
        if (!success)
            return NotFound(new ErrorResponse { Status = 404, Error = "NOT_FOUND", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(new { message });
    }

    // PATCH /api/v1/buses/{id}/status  (Operator or Admin)
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Operator,Admin")]
    public async Task<IActionResult> UpdateBusStatus(Guid id, [FromBody] UpdateBusStatusRequest request)
    {
        var validStatuses = new[] { "active", "maintenance", "removed" };
        if (!validStatuses.Contains(request.Status))
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "status must be: active | maintenance | removed", TraceId = HttpContext.TraceIdentifier });

        var actorId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var (success, message) = await _busService.UpdateBusStatusAsync(id, actorId, role, request.Status);
        if (!success)
            return NotFound(new ErrorResponse { Status = 404, Error = "NOT_FOUND", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(new { message });
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    private string GetCurrentUserRole()
        => User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
}
