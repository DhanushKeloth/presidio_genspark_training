using BusBookingAPI.Models.DTOs;

namespace BusBookingAPI.Services.Interfaces;

public interface IAdminService
{
    Task<PagedResult<OperatorListItem>> GetOperatorsAsync(string? status, int page, int pageSize);
    Task<OperatorDetailResponse?> GetOperatorByIdAsync(Guid operatorId);
    Task<(bool Success, string Message, int StatusCode)> UpdateOperatorStatusAsync(Guid operatorId, Guid adminId, UpdateOperatorStatusRequest request);
    Task<List<RouteDto>> GetRoutesAsync();
    Task<(bool Success, Guid? RouteId, string Message, int StatusCode)> CreateRouteAsync(Guid adminId, CreateRouteRequest request);
    Task<(bool Success, string Message, int StatusCode)> UpdateRouteAsync(Guid routeId, UpdateRouteRequest request);
    Task<(bool Success, string Message, int StatusCode)> ToggleRouteAsync(Guid routeId);
    Task<AdminDashboardResponse> GetDashboardAsync(DateTime from, DateTime to);
}
