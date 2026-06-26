using ShipmentTrackingAPI.DTOs.Shipment;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Shipment
{
    public class PublicTrackingDto
    {
        public string TrackingNumber { get; set; } = null!;
        public ShipmentStatus Status { get; set; }
        
        public string PickupAddress { get; set; } = null!; // General area
        public string DropoffAddress { get; set; } = null!; // General area
        
        public double? DriverLat { get; set; }
        public double? DriverLng { get; set; }
        
        public List<ShipmentEventDto> Events { get; set; } = new();
    }
}