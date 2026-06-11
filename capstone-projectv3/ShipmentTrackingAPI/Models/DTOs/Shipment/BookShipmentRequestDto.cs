using System.ComponentModel.DataAnnotations;

namespace ShipmentTrackingAPI.DTOs.Shipment
{
    public class BookShipmentRequestDto
    {
        [Required]
        public string PickupAddress { get; set; } = null!;
        public double? PickupLat { get; set; }
        public double? PickupLng { get; set; }
        
        [Required]
        public string DropoffAddress { get; set; } = null!;
        public double? DropoffLat { get; set; }
        public double? DropoffLng { get; set; }
        
        [Required]
        public string RecipientName { get; set; } = null!;
        
        [Required]
        [Phone]
        public string RecipientPhone { get; set; } = null!;
        
        [Required]
        [MinLength(1, ErrorMessage = "A shipment must contain at least one item.")]
        public List<ShipmentItemRequestDto> Items { get; set; } = new();
    }

    public class ShipmentItemRequestDto
    {
        [Required]
        public string Description { get; set; } = null!;

        [Range(0.1, 10000, ErrorMessage = "Weight must be greater than zero.")]
        public decimal Weight { get; set; }

        [Range(1, 1000, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        // --- THE NEW DIMENSION FIELDS ---
        // We use decimal? (nullable) so "Documents" don't require dimensions.
        // But IF they are provided, the Range attribute ensures they are > 0.
        
        [Range(0.1, 10000, ErrorMessage = "Height must be greater than zero if provided.")]
        public decimal? Height { get; set; }

        [Range(0.1, 10000, ErrorMessage = "Width must be greater than zero if provided.")]
        public decimal? Width { get; set; }

        [Range(0.1, 10000, ErrorMessage = "Length must be greater than zero if provided.")]
        public decimal? Length { get; set; }
    }
}