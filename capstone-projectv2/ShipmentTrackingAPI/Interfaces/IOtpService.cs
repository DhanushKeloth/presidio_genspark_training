using ShipmentTrackingAPI.DTOs.Otp; // Assuming OtpWindowDto and VerifyOtpResultDto are here
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Interfaces
{
    public interface IOtpService
    {
        // Generates and pushes the code, returning the window details (like expiry) to the driver
        Task<OtpWindowDto> RequestOtpAsync(int shipmentId, OtpType otpType, int driverId);
        
        // Validates the code and returns a detailed result (success, locked out, attempts remaining)
        Task<VerifyOtpResultDto> VerifyOtpAsync(int shipmentId, OtpType otpType, string code, int driverId);
        
        // Resets the clock and attempt count if expired or locked out
        Task<OtpWindowDto> RegenerateOtpAsync(int shipmentId, OtpType otpType, int driverId);
    }
}