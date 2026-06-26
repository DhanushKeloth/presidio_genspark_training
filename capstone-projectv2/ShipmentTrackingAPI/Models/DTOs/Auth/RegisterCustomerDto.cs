using System.ComponentModel.DataAnnotations;
using ShipmentTrackingAPI.Validation;

namespace ShipmentTrackingAPI.DTOs.Auth
{
    public class RegisterCustomerDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = null!;


        [Required]
        [MinLength(8)]
        [StrongPassword] //from the Validation 
        public string Password { get; set; } = null!;

        [Required]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;
        // These map to your CustomerProfile model
        
        [ValidPhoneNumber]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }
        [ValidPhoneNumber]
        [StringLength(20)]
        public string? AlternatePhoneNumber { get; set; }
    }
}