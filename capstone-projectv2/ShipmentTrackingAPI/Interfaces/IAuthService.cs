using ShipmentTrackingAPI.DTOs.Auth;

namespace ShipmentTrackingAPI.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
        Task<RegisterResponseDto> RegisterCustomerAsync(RegisterCustomerDto request);
        Task<RegisterResponseDto> RegisterDriverAsync(RegisterDriverDto request);
    }
}