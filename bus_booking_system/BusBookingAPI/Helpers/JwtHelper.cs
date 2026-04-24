using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BusBookingAPI.Helpers;

public class JwtHelper
{
    private readonly IConfiguration _config;

    public JwtHelper(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(Guid userId, string email, string role)
    {
        var secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
        var issuer = _config["Jwt:Issuer"] ?? "BusBookingSystem";
        var audience = _config["Jwt:Audience"] ?? "BusBookingClients";
        var expiryHours = int.Parse(_config["Jwt:ExpiryHours"] ?? "24");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetExpiry()
    {
        var expiryHours = int.Parse(_config["Jwt:ExpiryHours"] ?? "24");
        return DateTime.UtcNow.AddHours(expiryHours);
    }
}
