using System.ComponentModel.DataAnnotations;

namespace ShipmentTrackingAPI.DTOs.Auth
{
    /// <summary>
    /// Represents the user's credentials used for stateless authentication.
    /// </summary>
    public class LoginRequestDto
    {
        [Required,EmailAddress]
        public string Email { get; set; } = null!;
        [Required] 
        public string Password { get; set; } = null!;
    }
}