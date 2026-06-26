using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Repositories
{
    /// <summary>
    /// Handles database operations for the DriverProfile entity.
    /// </summary>
    public class DriverRepository : BaseRepository<DriverProfile>, IDriverRepository
    {
        public DriverRepository(AppDbContext ctx) : base(ctx) { }

        /// <summary>
        /// Retrieves a driver profile by their User ID, including the linked User data.
        /// </summary>
        public async Task<DriverProfile?> GetByUserIdAsync(int userId)
        {
            return await _ctx.DriverProfiles
                .Include(dp => dp.User)
                .FirstOrDefaultAsync(dp => dp.UserId == userId);
        }

        /// <summary>
        /// Checks if a license number is already registered.
        /// </summary>
        public async Task<bool> LicenseNumberExistsAsync(string licenseNumber)
        {
            return await _ctx.DriverProfiles
                .AnyAsync(dp => dp.LicenseNumber == licenseNumber);
        }

        /// <summary>
        /// Targeted, high-performance update of GPS coordinates bypassing the EF ChangeTracker.
        /// </summary>
        public async Task UpdateGpsAsync(int userId, double lat, double lng)
        {
            await _ctx.DriverProfiles
                .Where(dp => dp.UserId == userId)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(dp => dp.CurrentLat, lat)
                    .SetProperty(dp => dp.CurrentLng, lng));
        }
    }
}