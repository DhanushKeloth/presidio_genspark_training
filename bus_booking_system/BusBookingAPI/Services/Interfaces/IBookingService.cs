using BusBookingAPI.Models.DTOs;

namespace BusBookingAPI.Services.Interfaces;

public interface IBookingService
{
    Task<(bool Success, CreateBookingResponse? Response, string Message, int StatusCode)> CreateBookingAsync(Guid userId, CreateBookingRequest request);
    Task<PagedResult<BookingHistoryItem>> GetUserBookingsAsync(Guid userId, string? status, int page, int pageSize);
    Task<(bool Success, string Message, int StatusCode)> CancelBookingAsync(Guid bookingId, Guid userId, string? reason);
    Task<PagedResult<OperatorBookingItem>> GetOperatorBookingsAsync(Guid operatorId, Guid? busId, DateOnly? journeyDate, string? status, int page, int pageSize);
}
