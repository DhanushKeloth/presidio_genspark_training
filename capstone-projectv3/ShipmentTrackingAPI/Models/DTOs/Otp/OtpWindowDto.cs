namespace ShipmentTrackingAPI.DTOs.Otp
{
    public class OtpWindowDto
    {
        /// <summary>
        /// The exact UTC time the OTP expires. Used by the frontend for the countdown timer.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
        public string OtpType {get;set;}=null!;
        public DateTime GeneratedAt{get;set;}
        public int AttemptCount { get; set; }
    }
}