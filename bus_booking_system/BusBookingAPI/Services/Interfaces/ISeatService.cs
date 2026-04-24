using BusBookingAPI.Models.DTOs;

namespace BusBookingAPI.Services.Interfaces;

public interface ISeatService
{
    Task<(bool Success, SeatLockResponse? Response, string Message)> LockSeatsAsync(Guid userId, SeatLockRequest request);
    Task<(bool Success, string Message)> UnlockSeatsAsync(Guid userId, SeatUnlockRequest request);
}
