using ShipmentTrackingAPI.DTOs.Driver;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Services.Interfaces
{
    public interface IDriverService
    {
        Task<DriverProfileDto> GetMyProfileAsync(int userId);
      Task<DriverProfileDto> UpdateOpStatusAsync(int userId, UpdateOpStatusRequestDto request);     
    }
}