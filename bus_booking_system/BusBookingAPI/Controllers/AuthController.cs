using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BusBookingAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST /api/v1/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] UserRegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, message, statusCode) = await _authService.RegisterUserAsync(request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = statusCode == 409 ? "CONFLICT" : "VALIDATION_ERROR", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, new { message });
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> LoginUser([FromBody] LoginRequest request)
    {
        var (success, response, message, statusCode) = await _authService.LoginUserAsync(request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = statusCode == 403 ? "FORBIDDEN" : "UNAUTHORIZED", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(response);
    }

    // POST /api/v1/auth/operator/register
    [HttpPost("operator/register")]
    public async Task<IActionResult> RegisterOperator([FromBody] OperatorRegisterRequest request)
    {
        var (success, message, statusCode) = await _authService.RegisterOperatorAsync(request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "CONFLICT", Message = message, TraceId = HttpContext.TraceIdentifier });

        return StatusCode(201, new { message });
    }

    // POST /api/v1/auth/operator/login
    [HttpPost("operator/login")]
    public async Task<IActionResult> LoginOperator([FromBody] LoginRequest request)
    {
        var (success, response, message, statusCode) = await _authService.LoginOperatorAsync(request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = statusCode == 403 ? "FORBIDDEN" : "UNAUTHORIZED", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(response);
    }

    // POST /api/v1/auth/admin/login
    [HttpPost("admin/login")]
    public async Task<IActionResult> LoginAdmin([FromBody] LoginRequest request)
    {
        var (success, response, message, statusCode) = await _authService.LoginAdminAsync(request);
        if (!success)
            return StatusCode(statusCode, new ErrorResponse { Status = statusCode, Error = "UNAUTHORIZED", Message = message, TraceId = HttpContext.TraceIdentifier });

        return Ok(response);
    }
}
