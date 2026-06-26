using ShipmentTrackingAPI.Interfaces; 
using ShipmentTrackingAPI.Data; // Assuming this is your DbContext namespace
using ShipmentTrackingAPI.DTOs.Otp;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;


namespace ShipmentTrackingAPI.Services
{
    public class OtpService : IOtpService
    {
        private readonly IShipmentRepository _repo;
        private readonly AppDbContext _ctx;
        
        // private readonly INotificationService _notificationService;

        public OtpService(IShipmentRepository repo, AppDbContext ctx)
        {
            _repo = repo;
            _ctx = ctx;
        }

        public async Task<OtpWindowDto> RequestOtpAsync(int shipmentId, OtpType otpType, int driverId)
            => await GenerateAndUpsertOtpAsync(shipmentId, otpType, driverId);

        public async Task<OtpWindowDto> RegenerateOtpAsync(int shipmentId, OtpType otpType, int driverId)
            => await GenerateAndUpsertOtpAsync(shipmentId, otpType, driverId);

        public async Task<VerifyOtpResultDto> VerifyOtpAsync(int shipmentId, OtpType otpType, string code, int driverId)
        {
            var shipment = await _repo.GetByIdWithAddressesAsync(shipmentId) 
                ?? throw new KeyNotFoundException("Shipment not found.");

            if (shipment.DriverId != driverId)
                throw new UnauthorizedAccessException("You are not assigned to this shipment.");

            var window = shipment.ShipmentOtpWindows?.FirstOrDefault(w => w.OtpType == otpType);
            if (window == null)
            {
                throw new InvalidOperationException("No OTP window found. Please request an OTP first.");
            }

            int maxAttempts = 3;
            int remaining = maxAttempts - window.AttemptCount;

            // Guard: Already Verified
            if (window.VerifiedAt.HasValue)
            {
                return new VerifyOtpResultDto { Success = true, RemainingAttempts = remaining, NewStatus = shipment.Status };
            }

            // Guard: Locked Out or Expired
            if (window.AttemptCount >= maxAttempts || DateTime.UtcNow > window.ExpiresAt)
            {
                // Setting remaining to 0 triggers your frontend's IsLockedOut property
                return new VerifyOtpResultDto { Success = false, RemainingAttempts = 0 }; 
            }

            // Validation Failure
            if (window.OtpCode != code)
            {
                window.AttemptCount++;
                await _repo.UpsertOtpWindowAsync(window);
                
                return new VerifyOtpResultDto { Success = false, RemainingAttempts = maxAttempts - window.AttemptCount };
            }

            // SUCCESS FLOW: 1. Update the Window
            window.VerifiedAt = DateTime.UtcNow;
            window.OtpCode = null; // Erase code for security
            await _repo.UpsertOtpWindowAsync(window);

            // SUCCESS FLOW: 2. Advance the Shipment Status
            ShipmentStatus newStatus = otpType == OtpType.Pickup ? ShipmentStatus.PickedUp : ShipmentStatus.Delivered;
            shipment.Status = newStatus;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _repo.AddEventAsync(new ShipmentEvent
            {
                ShipmentId = shipment.Id,
                Status = newStatus,
                Description = $"{otpType} OTP verified successfully. Status advanced to {newStatus}.",
                OccurredAt = DateTime.UtcNow
            });

            await _ctx.SaveChangesAsync();

            return new VerifyOtpResultDto 
            { 
                Success = true, 
                RemainingAttempts = maxAttempts - window.AttemptCount, 
                NewStatus = newStatus 
            };
        }

        #region PRIVATE HELPERS

        private async Task<OtpWindowDto> GenerateAndUpsertOtpAsync(int shipmentId, OtpType otpType, int driverId)
        {
            var shipment = await _repo.GetByIdWithAddressesAsync(shipmentId) 
                ?? throw new KeyNotFoundException("Shipment not found.");

            if (shipment.DriverId != driverId)
                throw new UnauthorizedAccessException("You are not assigned to this shipment.");

            // Generate a secure 4-digit code (matching your DTO validation)
            string code = new Random().Next(1000, 10000).ToString("D4"); 
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(15); 

            var window = new ShipmentOtpWindow
            {
                ShipmentId = shipmentId,
                OtpType = otpType,
                OtpCode = code,
                GeneratedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                AttemptCount = 0,
                VerifiedAt = null
            };

            await _repo.UpsertOtpWindowAsync(window);

            // TODO: Await your SMS/Email service here to actually send 'code' to the customer

            return new OtpWindowDto
            {
                ExpiresAt = expiresAt,
                AttemptCount = 0
            };
        }

        #endregion
    }
}