namespace ShipmentTrackingAPI.DTOs.Auth
{
    public class RegisterResponseDto
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Message { get; set; } = "Registration successful.";
    }
}