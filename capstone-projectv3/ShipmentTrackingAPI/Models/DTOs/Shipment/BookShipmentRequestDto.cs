namespace ShipmentTrackingAPI.DTOs.Shipment
{
    public class BookShipmentRequestDto
    {
        public string PickupAddress { get; set; } = null!;
        public double? PickupLat { get; set; }
        public double? PickupLng { get; set; }
        
        public string DropoffAddress { get; set; } = null!;
        public double? DropoffLat { get; set; }
        public double? DropoffLng { get; set; }
        
        public string RecipientName { get; set; } = null!;
        public string RecipientPhone { get; set; } = null!;
        
        public List<ShipmentItemRequestDto> Items { get; set; } = new();
    }

    public class ShipmentItemRequestDto
    {
        public string Description { get; set; } = null!;
        public decimal Weight { get; set; }
        public int Quantity { get; set; }
    }
}