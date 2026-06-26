using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.DTOs.Driver;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Services.Interfaces;

namespace ShipmentTrackingAPI.Services
{
    public class DriverService : IDriverService
    {
        private readonly AppDbContext _ctx;

        public DriverService(AppDbContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<DriverProfileDto> GetMyProfileAsync(int userId)
        {
            var profile = await _ctx.DriverProfiles
                .Include(dp => dp.User) // Including user to get FullName
                .FirstOrDefaultAsync(dp => dp.UserId == userId)
                ?? throw new KeyNotFoundException("Driver profile not found.");

            return new DriverProfileDto
            {
                FullName = profile.User?.FullName ?? "Unknown",
                VehicleNumber = profile.VehicleNumber,
                OpStatus = profile.OpStatus,
                CurrentLat = profile.CurrentLat,
                CurrentLng = profile.CurrentLng
                // Map any other properties from your DriverProfileDto
            };
        }

        public async Task<DriverProfileDto> UpdateOpStatusAsync(int userId, DriverOpStatus newStatus)
        {
            var profile = await _ctx.DriverProfiles
                .Include(dp => dp.User)
                .FirstOrDefaultAsync(dp => dp.UserId == userId)
                ?? throw new KeyNotFoundException("Driver profile not found.");

            // Update the status
            profile.OpStatus = newStatus;
            profile.UpdatedAt = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();

            return new DriverProfileDto
            {
                FullName = profile.User?.FullName ?? "Unknown",
                VehicleNumber = profile.VehicleNumber,
                OpStatus = profile.OpStatus,
                CurrentLat = profile.CurrentLat,
                CurrentLng = profile.CurrentLng
            };
        }
    }
}