using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Shipment
{
    public class ShipmentDetailDto
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; } = null!;
        public ShipmentStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal TotalCost {get;set;}
        // Actor info
        public string CustomerName { get; set; } = null!;
        public string RecipientName { get; set; } = null!;
        public string RecipientPhone { get; set; } = null!;
        
        // Nullable because it might still be unassigned
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? DriverPhone { get; set; }
        public string? VehicleNumber { get; set; }

        // Addresses
        public ShipmentAddressDto PickupAddress { get; set; } = null!;
        public ShipmentAddressDto DropoffAddress { get; set; } = null!;

        public List<ShipmentItemDto> Items { get; set; } = new();

        // OTP Metadata (Strictly metadata, NO codes exposed)
        public DateTime? PickupOtpExpiresAt { get; set; }
        public int PickupOtpAttemptCount { get; set; }
        public DateTime? DeliveryOtpExpiresAt { get; set; }
        public int DeliveryOtpAttemptCount { get; set; }

        public List<ShipmentEventDto> Events { get; set; } = new();
    }
    public class ShipmentEventDto
    {
        public ShipmentStatus Status { get; set; }
        public string Description { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}