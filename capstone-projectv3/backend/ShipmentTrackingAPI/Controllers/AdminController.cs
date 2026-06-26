using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipmentTrackingAPI.DTOs.Admin;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using System.Security.Claims;

namespace ShipmentTrackingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")] // 🔒 Strict role enforcement: Only Admins can hit these endpoints
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Safely extracts the Admin's User ID from the JWT Token claims.
    /// </summary>
    private int GetAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        if (int.TryParse(userIdClaim, out int userId))
            return userId;

        throw new UnauthorizedException("Invalid token: Admin ID missing.");
    }

    // ═══════════════════════════════════════════════════════════
    //  DRIVER MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    [HttpGet("drivers")]
    public async Task<ActionResult<PaginatedResponse<AdminDriverDto>>> GetDrivers(
        [FromQuery] DriverAccountStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var response = await _adminService.GetAllDriversAsync(status, page, size);
        return Ok(response);
    }

    [HttpGet("drivers/{id}")]
    public async Task<ActionResult<AdminDriverDetailDto>> GetDriverDetail(int id)
    {
        var driver = await _adminService.GetDriverDetailAsync(id);
        return Ok(driver);
    }

    // Notice the {id} is gone from the route string
    [HttpPut("drivers/status")]
    public async Task<IActionResult> UpdateDriverStatus([FromBody] UpdateDriverStatusRequestDto request)
    {
        // We now grab the DriverId directly from the request object
        await _adminService.UpdateDriverAccountStatusAsync(GetAdminId(), request.DriverId, request.NewStatus);

        return Ok(new { Message = $"Driver {request.DriverId} status updated to {request.NewStatus}." });
    }

    // ═══════════════════════════════════════════════════════════
    //  SHIPMENT MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    [HttpGet("shipments")]
    public async Task<ActionResult<PaginatedResponse<AdminShipmentDto>>> GetShipments(
        [FromQuery] ShipmentStatus? status,
        [FromQuery] int? driverId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var response = await _adminService.GetAllShipmentsAsync(status, driverId, page, size);
        return Ok(response);
    }

    [HttpPut("shipments/{id}/override-status")]
    public async Task<IActionResult> OverrideShipmentStatus(int id, [FromBody] OverrideShipmentStatusRequestDto request)
    {
        // Bypasses normal state machine guards and logs the admin's action
        await _adminService.OverrideShipmentStatusAsync(GetAdminId(), id, request.NewStatus, request.Reason);

        return Ok(new { Message = $"Shipment {id} overridden to {request.NewStatus}." });
    }

    // ═══════════════════════════════════════════════════════════
    //  DASHBOARD
    // ═══════════════════════════════════════════════════════════

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboardMetrics()
    {
        var metrics = await _adminService.GetDashboardMetricsAsync();
        return Ok(metrics);
    }
}