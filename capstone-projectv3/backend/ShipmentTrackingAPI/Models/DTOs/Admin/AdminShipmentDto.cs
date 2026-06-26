using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin
{
    public class AdminShipmentDto
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; } = null!;
        public ShipmentStatus Status { get; set; }
        
        public string CustomerName { get; set; } = null!;
        public string? DriverName { get; set; } // Nullable if still pending
        
        public decimal TotalCost {get;set;}
        public string PickupArea { get; set; } = null!;
        public string DropoffArea { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; }
    }
}