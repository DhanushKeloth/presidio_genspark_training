using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipmentTrackingAPI.DTOs.Customer;
using ShipmentTrackingAPI.Interfaces;
using ShipmentTrackingAPI.Models.Exceptions;
using ShipmentTrackingAPI.Services.Interfaces;
using System.Security.Claims;

namespace ShipmentTrackingAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Customer")] // 🔒 Locks this entire controller to Customers only
public class CustomerController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomerController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Helper method to extract the User ID from the JWT Token.
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
    public async Task<ActionResult<CustomerProfileDto>> GetMyProfile()
    {
        var profile = await _customerService.GetProfileAsync(GetUserId());
        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<CustomerProfileDto>> UpdateMyProfile([FromBody] UpdateProfileRequestDto request)
    {
        var updatedProfile = await _customerService.UpdateProfileAsync(GetUserId(), request);
        return Ok(updatedProfile);
    }

    // ═══════════════════════════════════════════════════════════
    //  ADDRESS BOOK ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("addresses")]
    public async Task<ActionResult<List<SavedAddressDto>>> GetSavedAddresses()
    {
        var addresses = await _customerService.GetSavedAddressesAsync(GetUserId());
        return Ok(addresses);
    }

    [HttpPost("addresses")]
    public async Task<ActionResult<SavedAddressDto>> AddSavedAddress([FromBody] SavedAddressRequestDto request)
    {
        var newAddress = await _customerService.AddSavedAddressAsync(GetUserId(), request);
        
        // Returns HTTP 201 Created
        return CreatedAtAction(nameof(GetSavedAddresses), new { id = newAddress.Id }, newAddress);
    }

    [HttpPut("addresses/{id}")]
    public async Task<ActionResult<SavedAddressDto>> UpdateSavedAddress(int id, [FromBody] SavedAddressRequestDto request)
    {
        var updatedAddress = await _customerService.UpdateSavedAddressAsync(GetUserId(), id, request);
        return Ok(updatedAddress);
    }

    [HttpDelete("addresses/{id}")]
    public async Task<IActionResult> DeleteSavedAddress(int id)
    {
        await _customerService.DeleteSavedAddressAsync(GetUserId(), id);
        
        // Returns HTTP 204 No Content
        return NoContent(); 
    }
}