using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Driver;

public class UpdateOpStatusRequestDto
{
    public DriverOpStatus NewStatus { get; set; }
}