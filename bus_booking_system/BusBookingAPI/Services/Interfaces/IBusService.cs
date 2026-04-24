using BusBookingAPI.Models.DTOs;

namespace BusBookingAPI.Services.Interfaces;

public interface IBusService
{
    Task<PagedResult<BusSearchResult>> SearchBusesAsync(BusSearchRequest request);
    Task<BusDetailResponse?> GetBusDetailsAsync(Guid busId, DateOnly journeyDate);
    Task<(bool Success, Guid? BusId, string Message)> AddBusAsync(Guid operatorId, AddBusRequest request);
    Task<(bool Success, string Message)> UpdateBusAsync(Guid busId, Guid operatorId, UpdateBusRequest request);
    Task<(bool Success, string Message)> UpdateBusStatusAsync(Guid busId, Guid operatorId, string role, string status);
    Task<List<RouteDto>> GetAllRoutesAsync();
}
