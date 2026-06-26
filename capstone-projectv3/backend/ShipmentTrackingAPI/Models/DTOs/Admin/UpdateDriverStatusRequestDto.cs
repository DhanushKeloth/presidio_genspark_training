using System.ComponentModel.DataAnnotations;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin;

public class UpdateDriverStatusRequestDto
{
    [Required(ErrorMessage = "DriverId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "DriverId must be a positive integer.")]
    public int DriverId { get; set; }

    [Required(ErrorMessage = "NewStatus is required.")]
    [EnumDataType(typeof(DriverAccountStatus), ErrorMessage = "Invalid driver account status value.")]
    public DriverAccountStatus NewStatus { get; set; }
}
