namespace ShipmentTrackingAPI.DTOs.Shipment
{
    
public class PendingJobDto
    {
        public int Id{get;set;}
        public string TrackingNumber { get; set; } = null!;
        public string PickupArea { get; set; } = null!;
        public string DropoffArea { get; set; } = null!;
        public decimal TotalWeightKg { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}