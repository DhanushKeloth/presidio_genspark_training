using BusBookingAPI.Data;
using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Models.Entities;
using BusBookingAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using RouteEntity = BusBookingAPI.Models.Entities.Route;
namespace BusBookingAPI.Services.Implementations;

public class AdminService : IAdminService
{
    private readonly BusBookingDbContext _db;

    public AdminService(BusBookingDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<OperatorListItem>> GetOperatorsAsync(string? status, int page, int pageSize)
    {
        var query = _db.Operators.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var total = await query.CountAsync();
        var ops = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OperatorListItem
            {
                OperatorId = o.Id,
                CompanyName = o.CompanyName,
                Email = o.Email,
                Phone = o.Phone,
                Status = o.Status,
                CreatedAt = o.CreatedAt
            }).ToListAsync();

        return new PagedResult<OperatorListItem> { Total = total, Page = page, PageSize = pageSize, Results = ops };
    }

    public async Task<OperatorDetailResponse?> GetOperatorByIdAsync(Guid operatorId)
    {
        var op = await _db.Operators
            .Include(o => o.Buses)
            .FirstOrDefaultAsync(o => o.Id == operatorId);

        if (op == null) return null;

        return new OperatorDetailResponse
        {
            OperatorId = op.Id,
            CompanyName = op.CompanyName,
            Email = op.Email,
            Phone = op.Phone,
            Status = op.Status,
            CreatedAt = op.CreatedAt,
            GstNumber = op.GstNumber,
            Address = op.Address,
            RejectionReason = op.RejectionReason,
            Buses = op.Buses.Select(b => new BusListItem
            {
                BusId = b.Id,
                RegistrationNumber = b.RegistrationNumber,
                BusType = b.BusType,
                Status = b.Status
            }).ToList()
        };
    }

    public async Task<(bool Success, string Message, int StatusCode)> UpdateOperatorStatusAsync(Guid operatorId, Guid adminId, UpdateOperatorStatusRequest request)
    {
        var op = await _db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId);
        if (op == null) return (false, "Operator not found", 404);

        switch (request.Action.ToLower())
        {
            case "approve":
                op.Status = "approved";
                op.ApprovedBy = adminId;
                op.ApprovedAt = DateTime.UtcNow;
                break;
            case "reject":
                op.Status = "rejected";
                op.RejectionReason = request.Reason;
                break;
            case "disable":
                op.Status = "disabled";
                // Auto-cancel future bookings for this operator's buses
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var affectedBookings = await _db.Bookings
                    .Include(b => b.Payment)
                    .Where(b => b.Bus!.OperatorId == operatorId && b.Status == "confirmed" && b.JourneyDate >= today)
                    .ToListAsync();

                foreach (var booking in affectedBookings)
                {
                    booking.Status = "cancelled";
                    booking.CancellationReason = "Operator account disabled";
                    booking.CancelledAt = DateTime.UtcNow;
                    booking.UpdatedAt = DateTime.UtcNow;
                    if (booking.Payment != null)
                    {
                        booking.Payment.PaymentStatus = "refunded";
                        booking.Payment.RefundAmount = booking.TotalAmount;
                        booking.Payment.RefundInitiatedAt = DateTime.UtcNow;
                    }
                }
                break;
            default:
                return (false, "Invalid action. Use: approve | reject | disable", 400);
        }

        op.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, $"Operator {request.Action}d successfully", 200);
    }

    public async Task<List<RouteDto>> GetRoutesAsync()
    {
        return await _db.Routes
            .Select(r => new RouteDto
            {
                RouteId = r.Id,
                SourceCity = r.SourceCity,
                DestinationCity = r.DestinationCity,
                DistanceKm = r.DistanceKm,
                EstimatedHours = r.EstimatedHours,
                IsActive = r.IsActive
            }).ToListAsync();
    }

    public async Task<(bool Success, Guid? RouteId, string Message, int StatusCode)> CreateRouteAsync(Guid adminId, CreateRouteRequest request)
    {
        var exists = await _db.Routes.AnyAsync(r =>
            r.SourceCity.ToLower() == request.SourceCity.ToLower() &&
            r.DestinationCity.ToLower() == request.DestinationCity.ToLower());

        if (exists)
            return (false, null, "Route with same source-destination already exists", 409);

        var route = new RouteEntity
        {
            SourceCity = request.SourceCity,
            DestinationCity = request.DestinationCity,
            DistanceKm = request.DistanceKm,
            EstimatedHours = request.EstimatedHours,
            CreatedBy = adminId,
            IsActive = true
        };

        _db.Routes.Add(route);
        await _db.SaveChangesAsync();
        return (true, route.Id, "Route created", 201);
    }

    public async Task<(bool Success, string Message, int StatusCode)> UpdateRouteAsync(Guid routeId, UpdateRouteRequest request)
    {
        var route = await _db.Routes.FindAsync(routeId);
        if (route == null) return (false, "Route not found", 404);

        if (request.SourceCity != null) route.SourceCity = request.SourceCity;
        if (request.DestinationCity != null) route.DestinationCity = request.DestinationCity;
        if (request.DistanceKm.HasValue) route.DistanceKm = request.DistanceKm;
        if (request.EstimatedHours.HasValue) route.EstimatedHours = request.EstimatedHours;

        route.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, "Route updated", 200);
    }

    public async Task<(bool Success, string Message, int StatusCode)> ToggleRouteAsync(Guid routeId)
    {
        var route = await _db.Routes.FindAsync(routeId);
        if (route == null) return (false, "Route not found", 404);

        route.IsActive = !route.IsActive;
        route.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, $"Route {(route.IsActive ? "activated" : "deactivated")}", 200);
    }

    public async Task<AdminDashboardResponse> GetDashboardAsync(DateTime from, DateTime to)
    {
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        var bookingsQuery = _db.Bookings
            .Where(b => b.JourneyDate >= fromDate && b.JourneyDate <= toDate);

        var allBookings = await bookingsQuery.CountAsync();
        var cancelled = await bookingsQuery.CountAsync(b => b.Status == "cancelled");
        var revenue = await bookingsQuery
            .Where(b => b.Status == "confirmed" || b.Status == "completed")
            .SumAsync(b => (decimal?)b.TotalAmount) ?? 0;

        var cancellationRate = allBookings > 0 ? Math.Round((double)cancelled / allBookings * 100, 2) : 0;

        var revenueByOperator = await _db.Bookings
            .Where(b => b.JourneyDate >= fromDate && b.JourneyDate <= toDate && (b.Status == "confirmed" || b.Status == "completed"))
            .GroupBy(b => new { b.Bus!.OperatorId, b.Bus.Operator!.CompanyName })
            .Select(g => new RevenueByOperator
            {
                OperatorId = g.Key.OperatorId,
                Name = g.Key.CompanyName,
                Revenue = g.Sum(b => b.TotalAmount)
            }).ToListAsync();

        var revenueByRoute = await _db.Bookings
            .Where(b => b.JourneyDate >= fromDate && b.JourneyDate <= toDate && (b.Status == "confirmed" || b.Status == "completed"))
            .GroupBy(b => new { b.Bus!.RouteId, b.Bus.Route!.SourceCity, b.Bus.Route.DestinationCity })
            .Select(g => new RevenueByRoute
            {
                RouteId = g.Key.RouteId,
                Source = g.Key.SourceCity,
                Destination = g.Key.DestinationCity,
                Revenue = g.Sum(b => b.TotalAmount)
            }).ToListAsync();

        var bookingsByDate = await _db.Bookings
            .Where(b => b.JourneyDate >= fromDate && b.JourneyDate <= toDate)
            .GroupBy(b => b.JourneyDate)
            .Select(g => new BookingsByDate
            {
                Date = g.Key,
                Count = g.Count(),
                Revenue = g.Where(b => b.Status == "confirmed" || b.Status == "completed").Sum(b => b.TotalAmount)
            }).OrderBy(x => x.Date).ToListAsync();

        return new AdminDashboardResponse
        {
            TotalBookings = allBookings,
            TotalRevenue = revenue,
            CancellationRate = cancellationRate,
            RevenueByOperator = revenueByOperator,
            RevenueByRoute = revenueByRoute,
            BookingsByDate = bookingsByDate
        };
    }
}
