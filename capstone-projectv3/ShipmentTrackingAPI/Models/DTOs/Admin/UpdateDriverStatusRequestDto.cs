using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin;

public class UpdateDriverStatusRequestDto
{
    public int DriverId { get; set; }
    public DriverAccountStatus NewStatus { get; set; }
}

