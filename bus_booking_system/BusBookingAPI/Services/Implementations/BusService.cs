using System.Text.Json;
using BusBookingAPI.Data;
using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Models.Entities;
using BusBookingAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusBookingAPI.Services.Implementations;

public class BusService : IBusService
{
    private readonly BusBookingDbContext _db;

    public BusService(BusBookingDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<BusSearchResult>> SearchBusesAsync(BusSearchRequest request)
    {
        var journeyDate = request.JourneyDate;

        var query = _db.Buses
            .Include(b => b.Operator)
            .Include(b => b.Route)
            .Include(b => b.Seats.Where(s => s.IsActive))
                .ThenInclude(s => s.SeatLocks.Where(sl => sl.JourneyDate == journeyDate && sl.LockExpiry > DateTime.UtcNow))
            .Include(b => b.Seats.Where(s => s.IsActive))
                .ThenInclude(s => s.BookingDetails.Where(bd => bd.Booking!.JourneyDate == journeyDate && bd.Booking.Status == "confirmed"))
            .Where(b =>
                b.Status == "active" &&
                b.Route != null &&
                b.Route.IsActive &&
                EF.Functions.ILike(b.Route.SourceCity, $"%{request.Source}%") &&
                EF.Functions.ILike(b.Route.DestinationCity, $"%{request.Destination}%")
            );

        if (!string.IsNullOrWhiteSpace(request.BusType))
            query = query.Where(b => b.BusType == request.BusType);

        if (request.MinPrice.HasValue)
            query = query.Where(b => b.PricePerSeat >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(b => b.PricePerSeat <= request.MaxPrice.Value);

        var total = await query.CountAsync();

        var buses = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var results = buses.Select(b =>
        {
            var activeSeats = b.Seats.Where(s => s.IsActive).ToList();
            var totalSeats = activeSeats.Count;
            var lockedSeats = activeSeats.Sum(s => s.SeatLocks.Count);
            var bookedSeats = activeSeats.Sum(s => s.BookingDetails.Count);
            var available = totalSeats - lockedSeats - bookedSeats;

            var amenities = new List<string>();
            try { amenities = JsonSerializer.Deserialize<List<string>>(b.Amenities) ?? []; } catch { }

            return new BusSearchResult
            {
                BusId = b.Id,
                OperatorName = b.Operator?.CompanyName ?? "",
                BusType = b.BusType,
                RegistrationNumber = b.RegistrationNumber,
                DepartureTime = b.DepartureTime,
                ArrivalTime = b.ArrivalTime,
                EstimatedHours = b.Route?.EstimatedHours,
                TotalSeats = totalSeats,
                AvailableSeats = Math.Max(0, available),
                PricePerSeat = b.PricePerSeat,
                Amenities = amenities,
                BoardingLocation = b.BoardingLocation,
                DroppingLocation = b.DroppingLocation,
                SourceCity = b.Route?.SourceCity ?? "",
                DestinationCity = b.Route?.DestinationCity ?? ""
            };
        }).ToList();

        return new PagedResult<BusSearchResult>
        {
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
            Results = results
        };
    }

    public async Task<BusDetailResponse?> GetBusDetailsAsync(Guid busId, DateOnly journeyDate)
    {
        var bus = await _db.Buses
            .Include(b => b.Operator)
            .Include(b => b.Route)
            .Include(b => b.Seats.Where(s => s.IsActive))
                .ThenInclude(s => s.SeatLocks.Where(sl => sl.JourneyDate == journeyDate && sl.LockExpiry > DateTime.UtcNow))
            .Include(b => b.Seats.Where(s => s.IsActive))
                .ThenInclude(s => s.BookingDetails.Where(bd => bd.Booking!.JourneyDate == journeyDate && bd.Booking.Status == "confirmed"))
            .FirstOrDefaultAsync(b => b.Id == busId);

        if (bus == null) return null;

        var amenities = new List<string>();
        try { amenities = JsonSerializer.Deserialize<List<string>>(bus.Amenities) ?? []; } catch { }

        var seats = bus.Seats.Where(s => s.IsActive).Select(s =>
        {
            string status = "available";
            if (s.SeatLocks.Any()) status = "locked";
            else if (s.BookingDetails.Any()) status = "booked";

            return new SeatDto
            {
                SeatId = s.Id,
                SeatNumber = s.SeatNumber,
                SeatType = s.SeatType,
                Row = s.RowPosition,
                Col = s.ColPosition,
                Status = status
            };
        }).ToList();

        return new BusDetailResponse
        {
            BusId = bus.Id,
            OperatorName = bus.Operator?.CompanyName ?? "",
            BusType = bus.BusType,
            RegistrationNumber = bus.RegistrationNumber,
            Amenities = amenities,
            DepartureTime = bus.DepartureTime,
            ArrivalTime = bus.ArrivalTime,
            BoardingLocation = bus.BoardingLocation,
            DroppingLocation = bus.DroppingLocation,
            PricePerSeat = bus.PricePerSeat,
            SourceCity = bus.Route?.SourceCity ?? "",
            DestinationCity = bus.Route?.DestinationCity ?? "",
            SeatLayout = new SeatLayoutDto { Seats = seats }
        };
    }

    public async Task<(bool Success, Guid? BusId, string Message)> AddBusAsync(Guid operatorId, AddBusRequest request)
    {
        if (request.SeatLayout.Layout.Count != request.TotalSeats)
            return (false, null, $"total_seats ({request.TotalSeats}) must equal seat_layout.layout.length ({request.SeatLayout.Layout.Count})");

        var regExists = await _db.Buses.AnyAsync(b => b.RegistrationNumber == request.RegistrationNumber);
        if (regExists)
            return (false, null, "Registration number already exists");

        var routeExists = await _db.Routes.AnyAsync(r => r.Id == request.RouteId && r.IsActive);
        if (!routeExists)
            return (false, null, "Route not found or inactive");

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var amenitiesJson = JsonSerializer.Serialize(request.Amenities);
            var seatLayoutJson = JsonSerializer.Serialize(request.SeatLayout);

            var bus = new Bus
            {
                OperatorId = operatorId,
                RouteId = request.RouteId,
                RegistrationNumber = request.RegistrationNumber,
                BusType = request.BusType,
                TotalSeats = request.TotalSeats,
                DepartureTime = request.DepartureTime,
                ArrivalTime = request.ArrivalTime,
                BoardingLocation = request.BoardingLocation,
                DroppingLocation = request.DroppingLocation,
                PricePerSeat = request.PricePerSeat,
                Amenities = amenitiesJson,
                SeatLayout = seatLayoutJson,
                Status = "active"
            };
            _db.Buses.Add(bus);
            await _db.SaveChangesAsync();

            var seats = request.SeatLayout.Layout.Select(si => new Seat
            {
                BusId = bus.Id,
                SeatNumber = si.SeatNumber,
                SeatType = si.Type,
                RowPosition = si.Row,
                ColPosition = si.Col,
                IsActive = true
            }).ToList();

            _db.Seats.AddRange(seats);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, bus.Id, "Bus added successfully");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string Message)> UpdateBusAsync(Guid busId, Guid operatorId, UpdateBusRequest request)
    {
        var bus = await _db.Buses.FirstOrDefaultAsync(b => b.Id == busId && b.OperatorId == operatorId);
        if (bus == null) return (false, "Bus not found or not owned by operator");

        if (request.BusType != null) bus.BusType = request.BusType;
        if (request.RouteId.HasValue) bus.RouteId = request.RouteId.Value;
        if (request.DepartureTime.HasValue) bus.DepartureTime = request.DepartureTime.Value;
        if (request.ArrivalTime.HasValue) bus.ArrivalTime = request.ArrivalTime.Value;
        if (request.BoardingLocation != null) bus.BoardingLocation = request.BoardingLocation;
        if (request.DroppingLocation != null) bus.DroppingLocation = request.DroppingLocation;
        if (request.PricePerSeat.HasValue) bus.PricePerSeat = request.PricePerSeat.Value;
        if (request.Amenities != null) bus.Amenities = JsonSerializer.Serialize(request.Amenities);

        bus.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, "Bus updated successfully");
    }

    public async Task<(bool Success, string Message)> UpdateBusStatusAsync(Guid busId, Guid actorId, string role, string status)
    {
        Bus? bus;
        if (role == "Operator")
            bus = await _db.Buses.FirstOrDefaultAsync(b => b.Id == busId && b.OperatorId == actorId);
        else
            bus = await _db.Buses.FirstOrDefaultAsync(b => b.Id == busId);

        if (bus == null) return (false, "Bus not found");

        bus.Status = status;
        bus.UpdatedAt = DateTime.UtcNow;

        if (status == "removed")
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var futureBookings = await _db.Bookings
                .Where(b => b.BusId == busId && b.Status == "confirmed" && b.JourneyDate >= today)
                .ToListAsync();

            foreach (var booking in futureBookings)
            {
                booking.Status = "cancelled";
                booking.CancellationReason = "Bus removed by operator";
                booking.CancelledAt = DateTime.UtcNow;
                booking.UpdatedAt = DateTime.UtcNow;

                var payment = await _db.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.Id);
                if (payment != null)
                {
                    payment.PaymentStatus = "refunded";
                    payment.RefundAmount = payment.Amount;
                    payment.RefundInitiatedAt = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync();
        return (true, "Bus status updated");
    }

    public async Task<List<RouteDto>> GetAllRoutesAsync()
    {
        return await _db.Routes
            .Where(r => r.IsActive)
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
}
