using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Shipment
{
    // Used for standard list views (Customer dashboard, Admin tables)
    public class ShipmentSummaryDto
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; } = null!;
        public ShipmentStatus Status { get; set; }
        public string PickupArea { get; set; } = null!; // Truncated for lists
        public string DropoffArea { get; set; } = null!; // Truncated for lists
        public DateTime CreatedAt { get; set; }
        public decimal TotalCost {get;set;}
    }
}