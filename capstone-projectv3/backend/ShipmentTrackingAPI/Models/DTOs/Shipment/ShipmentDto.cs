using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Shipment
{
    // The standard return type for mutation actions (Book, Cancel, Assign, Update)
    public class ShipmentDto
    {
        public int Id { get; set; }
        public string TrackingNumber { get; set; } = null!;
        public ShipmentStatus Status { get; set; }
        public decimal TotalCost {get;set;}
        public DateTime CreatedAt { get; set; }
    }

    // Used for filtering the Customer Shipments list
    public class ShipmentQueryParams
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 20;
        public ShipmentStatus? Status { get; set; }
    }

    // Generic pagination wrapper
 
}