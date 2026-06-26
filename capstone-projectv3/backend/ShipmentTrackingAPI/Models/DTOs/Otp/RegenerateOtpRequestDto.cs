using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Otp;

public class RegenerateOtpRequestDto
{
    // The driver needs to specify if they are regenerating the Pickup or Delivery OTP
    public OtpType OtpType { get; set; }
}