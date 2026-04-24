using BusBookingAPI.Models.DTOs;

namespace BusBookingAPI.Services.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string Message, int StatusCode)> RegisterUserAsync(UserRegisterRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginUserAsync(LoginRequest request);
    Task<(bool Success, string Message, int StatusCode)> RegisterOperatorAsync(OperatorRegisterRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginOperatorAsync(LoginRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginAdminAsync(LoginRequest request);
}
