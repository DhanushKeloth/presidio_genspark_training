namespace ShipmentTrackingAPI.DTOs.Shipment
{
    
    public class ShipmentAddressDto
    {
        public string AddressLine1 { get; set; } = null!;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string PostalCode { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}