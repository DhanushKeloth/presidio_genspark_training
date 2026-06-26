using System.ComponentModel.DataAnnotations;

namespace ShipmentTrackingAPI.DTOs.Otp
{
    public class VerifyOtpRequestDto
    {
        [Required]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "OTP must be exactly 4 characters.")]
        public string OtpCode { get; set; } = null!;
    }
}