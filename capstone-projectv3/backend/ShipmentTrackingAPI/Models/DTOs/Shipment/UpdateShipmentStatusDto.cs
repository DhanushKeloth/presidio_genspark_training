using System.ComponentModel.DataAnnotations;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Shipment;

public class UpdateShipmentStatusDto
{
    [Required(ErrorMessage = "The new status is required.")]
    public ShipmentStatus NewStatus { get; set; }
}