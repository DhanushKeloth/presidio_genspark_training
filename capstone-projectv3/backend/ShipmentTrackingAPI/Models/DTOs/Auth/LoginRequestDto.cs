using System.ComponentModel.DataAnnotations;

namespace ShipmentTrackingAPI.DTOs.Auth
{
    /// <summary>
    /// Represents the user's credentials used for stateless authentication.
    /// </summary>
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(1, ErrorMessage = "Password cannot be empty.")]
        public string Password { get; set; } = null!;
    }
}