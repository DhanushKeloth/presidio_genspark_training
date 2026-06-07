using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.DTOs.Driver
{
    public class DriverProfileDto
    {
        // Primary Keys
        public int Id { get; set; } // The ID of the DriverProfile row
        public int UserId { get; set; } // The ID of the linked User row

        // ==========================================
        // IDENTITY (From Users Table)
        // ==========================================
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;

        // ==========================================
        // DRIVER DETAILS (From DriverProfiles Table)
        // ==========================================
        public string PhoneNumber { get; set; } = null!;
        public string VehicleType { get; set; } = null!;
        public string VehicleNumber { get; set; } = null!;
        public string LicenseNumber { get; set; } = null!;

        // ==========================================
        // STATUSES
        // ==========================================
        public DriverAccountStatus AccountStatus { get; set; }
        
        // Nullable because a driver pending approval or suspended won't have an operational status
        public DriverOpStatus? OpStatus { get; set; }

        // ==========================================
        // LOCATION & METADATA
        // ==========================================
        public double? CurrentLat { get; set; }
        public double? CurrentLng { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}