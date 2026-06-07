namespace ShipmentTrackingAPI.DTOs.Admin
{
    public class DashboardDto
    {
        public int TotalPendingShipments { get; set; }
        public int ActiveDeliveries { get; set; }
        public int DeliveriesToday { get; set; }
        public int DriversPendingApproval { get; set; }
        public int TotalActiveDrivers { get; set; }
    }
}