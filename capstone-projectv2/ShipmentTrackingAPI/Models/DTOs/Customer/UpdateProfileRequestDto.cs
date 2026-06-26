namespace ShipmentTrackingAPI.DTOs.Customer
{
    public class UpdateProfileRequestDto
    {
        public string? PhoneNumber { get; set; }
        public string? AlternatePhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}