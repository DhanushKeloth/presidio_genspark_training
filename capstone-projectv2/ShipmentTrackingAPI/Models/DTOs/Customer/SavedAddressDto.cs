namespace ShipmentTrackingAPI.DTOs.Customer
{
    public class SavedAddressDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = null!;
        public string AddressLine1 { get; set; } = null!;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string PostalCode { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public bool IsDefault { get; set; }
    }
}