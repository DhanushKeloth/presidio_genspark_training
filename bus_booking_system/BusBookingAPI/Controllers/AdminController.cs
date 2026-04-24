using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    // ─── Operator Management ───────────────────────────────────────────────────

    // GET /api/v1/admin/operators
    [HttpGet("operators")]
    public async Task<IActionResult> GetOperators(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        var result = await _adminService.GetOperatorsAsync(status, page, Math.Min(page_size, 100));
        return Ok(result);
    }

    // GET /api/v1/admin/operators/{id}
    [HttpGet("operators/{id:guid}")]
    public async Task<IActionResult> GetOperatorById(Guid id)
    {
        var op = await _adminService.GetOperatorByIdAsync(id);
        if (op == null)
            return NotFound(new ErrorResponse { Status = 404, Error = "NOT_FOUND", Message = "Operator not found", TraceId = HttpContext.TraceIdentifier });
        return Ok(op);
    }

    // PATCH /api/v1/admin/operators/{id}
    [HttpPatch("operators/{id:guid}")]
    public async Task<IActionResult> UpdateOperatorStatus(Guid id, [FromBody] UpdateOperatorStatusRequest request)
    {
        var adminId = GetCurrentUserId();
        var (success, message, statusCode) = await _adminService.UpdateOperatorStatusAsync(id, adminId, request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "VALIDATION_ERROR", Message = message, TraceId = HttpContext.TraceIdentifier });
        return Ok(new { message });
    }

    // ─── Route Management ─────────────────────────────────────────────────────

    // GET /api/v1/admin/routes
    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes()
    {
        var routes = await _adminService.GetRoutesAsync();
        return Ok(routes);
    }

    // POST /api/v1/admin/routes
    [HttpPost("routes")]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCity) || string.IsNullOrWhiteSpace(request.DestinationCity))
            return BadRequest(new ErrorResponse { Status = 400, Error = "VALIDATION_ERROR", Message = "source_city and destination_city are required", TraceId = HttpContext.TraceIdentifier });

        var adminId = GetCurrentUserId();
        var (success, routeId, message, statusCode) = await _adminService.CreateRouteAsync(adminId, request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "CONFLICT", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, new { route_id = routeId });
    }

    // PUT /api/v1/admin/routes/{id}
    [HttpPut("routes/{id:guid}")]
    public async Task<IActionResult> UpdateRoute(Guid id, [FromBody] UpdateRouteRequest request)
    {
        var (success, message, statusCode) = await _adminService.UpdateRouteAsync(id, request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "NOT_FOUND", Message = message, TraceId = HttpContext.TraceIdentifier });
        return Ok(new { message });
    }

    // PATCH /api/v1/admin/routes/{id}/toggle
    [HttpPatch("routes/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleRoute(Guid id)
    {
        var (success, message, statusCode) = await _adminService.ToggleRouteAsync(id);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "NOT_FOUND", Message = message, TraceId = HttpContext.TraceIdentifier });
        return Ok(new { message });
    }

    // ─── Dashboard ─────────────────────────────────────────────────────────────

    // GET /api/v1/admin/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;
        var result = await _adminService.GetDashboardAsync(fromDate, toDate);
        return Ok(result);
    }

    // ─── Admin Bookings View ───────────────────────────────────────────────────

    // GET /api/v1/admin/buses  (list all buses for admin)
    [HttpGet("buses")]
    public async Task<IActionResult> GetAllBuses([FromServices] IBusService busService)
    {
        var routes = await busService.GetAllRoutesAsync();
        return Ok(routes); // reuse routes for now; full bus admin list can be extended
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }
}
