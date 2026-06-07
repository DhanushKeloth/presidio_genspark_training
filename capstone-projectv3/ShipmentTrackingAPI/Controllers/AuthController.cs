using Microsoft.AspNetCore.Mvc;
using ShipmentTrackingAPI.DTOs.Auth;
using ShipmentTrackingAPI.Interfaces;

namespace ShipmentTrackingAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register/customer")]
        public async Task<ActionResult<RegisterResponseDto>> RegisterCustomer(RegisterCustomerDto request)
        {
            var response = await _authService.RegisterCustomerAsync(request);
            // Returns 201 Created
            return StatusCode(201, response);
        }

        [HttpPost("register/driver")]
        public async Task<ActionResult<RegisterResponseDto>> RegisterDriver(RegisterDriverDto request)
        {
            var response = await _authService.RegisterDriverAsync(request);
            // Returns 201 Created
            return StatusCode(201, response);
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);
            // Returns 200 OK
            return Ok(response);
        }
    }
}