using System.ComponentModel.DataAnnotations;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin;

public class OverrideShipmentStatusRequestDto
{
    [Required(ErrorMessage = "New status is required.")]
    [EnumDataType(typeof(ShipmentStatus), ErrorMessage = "Invalid shipment status value.")]
    public ShipmentStatus NewStatus { get; set; }

    // Required string to explain why the admin bypassed the state machine
    [Required(ErrorMessage = "A reason is required for overriding shipment status.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}