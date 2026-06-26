using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth0.Controllers;

[ApiController]
[Route("api/[controller]")] // This makes the route: api/data
public class DataController : ControllerBase
{
    // 1. PUBLIC ENDPOINT (Anyone can call this)
    // URL: GET https://localhost:xxxx/api/data/public
    [HttpGet("public")]
    public IActionResult GetPublicData()
    {
        return Ok(new { message = "Hello from a public endpoint! No login required." });
    }

    // 2. SECURE ENDPOINT (Requires Auth0 Token)
    // URL: GET https://localhost:xxxx/api/data/private
    [Authorize] // <-- This attribute blocks unauthorized requests
    [HttpGet("private")]
    public IActionResult GetPrivateData()
    {
        return Ok(new { message = "Hello from a SECURE endpoint! Your Auth0 token is valid." });
    }
}