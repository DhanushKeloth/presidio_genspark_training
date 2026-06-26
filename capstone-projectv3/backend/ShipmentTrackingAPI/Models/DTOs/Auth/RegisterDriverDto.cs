using System.ComponentModel.DataAnnotations;
using ShipmentTrackingAPI.Validation;

namespace ShipmentTrackingAPI.DTOs.Auth
{
    public class RegisterDriverDto
    {
        // Fields for the User table
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StrongPassword] 
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Please confirm your password.")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = null!;
        // Fields for the DriverProfile table
       
       [Required(ErrorMessage = "Phone number is required for dispatch.")]
        [ValidPhoneNumber]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required(ErrorMessage = "Vehicle type is required.")]
        [StringLength(50)]
        public string VehicleType { get; set; } = null!;

        [Required(ErrorMessage = "Vehicle registration number is required.")]
        [StringLength(20)]
        // Example format: XX-00-XX-0000. You can adjust the Regex to match your country's exact plate format!
        [RegularExpression(@"^[A-Z]{2}-\d{2}-[A-Z]{2}-\d{4}$", ErrorMessage = "Vehicle number must match format XX-00-XX-0000 (e.g., TS-09-AB-1234)")]
        public string VehicleNumber { get; set; } = null!;

        [Required(ErrorMessage = "Driver's license number is required.")]
        [StringLength(30)]
        // Simple alphanumeric check, adjust if your licenses have specific dash patterns
        [RegularExpression(@"^[A-Z0-9-]+$", ErrorMessage = "License number contains invalid characters.")]
        public string LicenseNumber { get; set; } = null!;}
}