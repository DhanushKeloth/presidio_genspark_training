namespace ShipmentTrackingAPI.DTOs.Customer
{
    public class CustomerProfileDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string? AlternatePhoneNumber { get; set; }
        public string? ProfileImageUrl { get; set; }
    }
}