using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Admin
{
    // Used for the grid/list view of all drivers
    public class AdminDriverDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public DriverAccountStatus AccountStatus { get; set; }
        public string VehicleType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    // Used when the Admin clicks into a specific driver's profile to approve/suspend them
    public class AdminDriverDetailDto : AdminDriverDto
    {
        public string? VehicleNumber { get; set; } = null!;
        public string LicenseNumber { get; set; } = null!;
        
        // Nullable because they might not be approved yet
        public DriverOpStatus? OpStatus { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByName { get; set; } // The name of the Admin who approved them
    }
}