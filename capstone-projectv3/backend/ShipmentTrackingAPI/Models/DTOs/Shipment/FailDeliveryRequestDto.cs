using System.ComponentModel.DataAnnotations;

namespace ShipmentTrackingAPI.DTOs.Shipment;

public class FailDeliveryRequestDto
{
    [Required(ErrorMessage = "A reason for the failed delivery must be provided.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}