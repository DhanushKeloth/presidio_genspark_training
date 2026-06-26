using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipmentTrackingAPI.DTOs.Driver;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Services.Interfaces;
using System.Security.Claims;

namespace ShipmentTrackingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Driver")] // 🔒 Locks this entire controller to Drivers only
public class DriverController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriverController(IDriverService driverService)
    {
        _driverService = driverService;
    }

    /// <summary>
    /// Helper method to safely extract the Driver's User ID from the JWT Token.
    /// </summary>
    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                       ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        
        if (int.TryParse(userIdClaim, out int userId))
            return userId;

        throw new UnauthorizedException("Invalid token: User ID missing.");
    }

    // ═══════════════════════════════════════════════════════════
    //  PROFILE ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("profile")]
    public async Task<ActionResult<DriverProfileDto>> GetMyProfile()
    {
        // Automatically grabs the ID from the token and calls your exact method
        var profile = await _driverService.GetMyProfileAsync(GetUserId());
        return Ok(profile);
    }

    // ═══════════════════════════════════════════════════════════
    //  STATUS ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    [HttpPut("status")]
    public async Task<ActionResult<DriverProfileDto>> UpdateOperatingStatus([FromBody] UpdateOpStatusRequestDto request)
    {
        // Passes the token ID and the enum directly to your service
       var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    
    var updatedProfile = await _driverService.UpdateOpStatusAsync(userId, request);
    
    return Ok(updatedProfile);
    }
}