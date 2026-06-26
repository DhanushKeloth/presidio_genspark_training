using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Otp
{
    public class VerifyOtpResultDto
    {
        public bool Success { get; set; }

        public int RemainingAttempts { get; set; }

        /// <summary>
        /// Populated only if Success is true. Tells the frontend what the new state of the shipment is.
        /// </summary>
        public ShipmentStatus? NewStatus { get; set; }
        public string Message{get;set;}=null!;

        /// <summary>
        /// Helper property for the frontend to easily know if they should disable the input and show the "Regenerate" button.
        /// </summary>
        public bool IsLockedOut => !Success && RemainingAttempts <= 0;
    }
}