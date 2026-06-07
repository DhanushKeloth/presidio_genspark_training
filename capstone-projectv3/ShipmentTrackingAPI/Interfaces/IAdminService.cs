using ShipmentTrackingAPI.DTOs.Admin;
using ShipmentTrackingAPI.DTOs.Common;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Interfaces
{
    public interface IAdminService
    {
        Task<PaginatedResponse<AdminDriverDto>> GetAllDriversAsync(DriverAccountStatus? status, int page, int size);
        Task<AdminDriverDetailDto> GetDriverDetailAsync(int driverId);
        Task UpdateDriverAccountStatusAsync(int adminId, int driverId, DriverAccountStatus newStatus);

        Task<PaginatedResponse<AdminShipmentDto>> GetAllShipmentsAsync(ShipmentStatus? status, int? driverId, int page, int size);
        Task OverrideShipmentStatusAsync(int adminId, int shipmentId, ShipmentStatus newStatus, string reason);
        Task<DashboardDto> GetDashboardMetricsAsync();
    }
}