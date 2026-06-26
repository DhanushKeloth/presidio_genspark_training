namespace ShipmentTrackingAPI.DTOs.Shipment
{
    
public class ShipmentItemDto
    {
        public string Description { get; set; } = null!;
        public decimal Weight { get; set; }
        public int Quantity { get; set; }
    }
}