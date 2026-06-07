using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin;


public class OverrideShipmentStatusRequestDto
{
    public ShipmentStatus NewStatus { get; set; }
    
    // Required string to explain why the admin bypassed the state machine
    public string Reason { get; set; } = string.Empty; 
}