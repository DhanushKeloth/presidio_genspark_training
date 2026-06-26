using ShipmentTrackingAPI.Models;

namespace ShipmentTrackingAPI.Repositories.RepoInterfaces
{
    public interface IDriverRepository : IRepository<DriverProfile>
    {
        /// <summary>
        /// 1:1 lookup mapping a UserId to their specific DriverProfile.
        /// All driver-scoped operations start here.
        /// </summary>
        Task<DriverProfile?> GetByUserIdAsync(int userId);

        /// <summary>
        /// Pre-registration uniqueness check to ensure no duplicate licenses.
        /// </summary>
        Task<bool> LicenseNumberExistsAsync(string license);

        /// <summary>
        /// Highly optimized, targeted UPDATE for GPS coordinates.
        /// Bypasses full entity tracking to handle high-frequency (5s) calls from the GPS service.
        /// </summary>
        Task UpdateGpsAsync(int userId, double lat, double lng);
    }
}