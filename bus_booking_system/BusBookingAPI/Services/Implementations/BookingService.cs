using BusBookingAPI.Data;
using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Models.Entities;
using BusBookingAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusBookingAPI.Services.Implementations;

public class BookingService : IBookingService
{
    private readonly BusBookingDbContext _db;

    public BookingService(BusBookingDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, CreateBookingResponse? Response, string Message, int StatusCode)> CreateBookingAsync(Guid userId, CreateBookingRequest request)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var seatIds = request.Passengers.Select(p => p.SeatId).ToList();

            // Validate all seat locks exist and belong to this user
            foreach (var seatId in seatIds)
            {
                var lockExists = await _db.SeatLocks.AnyAsync(sl =>
                    sl.SeatId == seatId &&
                    sl.UserId == userId &&
                    sl.BusId == request.BusId &&
                    sl.JourneyDate == request.JourneyDate &&
                    sl.LockExpiry > DateTime.UtcNow);

                if (!lockExists)
                {
                    await tx.RollbackAsync();
                    return (false, null, $"Seat lock expired or not found. Please re-select seats.", 400);
                }

                // Double-check seat isn't booked
                var alreadyBooked = await _db.BookingDetails.AnyAsync(bd =>
                    bd.SeatId == seatId &&
                    bd.Booking!.JourneyDate == request.JourneyDate &&
                    bd.Booking.Status == "confirmed");

                if (alreadyBooked)
                {
                    await tx.RollbackAsync();
                    return (false, null, "Seat already booked", 409);
                }
            }

            // Get price per seat
            var bus = await _db.Buses.FindAsync(request.BusId);
            if (bus == null)
            {
                await tx.RollbackAsync();
                return (false, null, "Bus not found", 404);
            }

            var totalAmount = bus.PricePerSeat * request.Passengers.Count;

            // Insert booking
            var booking = new Booking
            {
                UserId = userId,
                BusId = request.BusId,
                JourneyDate = request.JourneyDate,
                TotalAmount = totalAmount,
                Status = "confirmed"
            };
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            // Insert booking details
            var bookingDetails = request.Passengers.Select(p => new BookingDetail
            {
                BookingId = booking.Id,
                SeatId = p.SeatId,
                PassengerName = p.PassengerName,
                PassengerAge = p.PassengerAge,
                PassengerGender = p.PassengerGender,
                SeatPrice = bus.PricePerSeat
            }).ToList();

            _db.BookingDetails.AddRange(bookingDetails);

            // Insert payment (dummy for Phase 1)
            var payment = new Payment
            {
                BookingId = booking.Id,
                Amount = totalAmount,
                PaymentMethod = request.Payment.Method,
                PaymentStatus = "success",
                PaidAt = DateTime.UtcNow
            };
            _db.Payments.Add(payment);

            // Remove seat locks
            var locks = await _db.SeatLocks
                .Where(sl => seatIds.Contains(sl.SeatId) && sl.UserId == userId && sl.JourneyDate == request.JourneyDate)
                .ToListAsync();
            _db.SeatLocks.RemoveRange(locks);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, new CreateBookingResponse
            {
                BookingId = booking.Id,
                BookingStatus = "confirmed",
                TotalAmount = totalAmount,
                TicketDownloadUrl = $"/api/v1/bookings/{booking.Id}/ticket"
            }, "Booking confirmed", 201);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<PagedResult<BookingHistoryItem>> GetUserBookingsAsync(Guid userId, string? status, int page, int pageSize)
    {
        var query = _db.Bookings
            .Include(b => b.Bus)
                .ThenInclude(b => b!.Route)
            .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = status switch
            {
                "upcoming" => query.Where(b => b.Status == "confirmed" && b.JourneyDate >= today),
                "completed" => query.Where(b => b.Status == "completed" || (b.Status == "confirmed" && b.JourneyDate < today)),
                "cancelled" => query.Where(b => b.Status == "cancelled"),
                _ => query.Where(b => b.Status == status)
            };
        }

        var total = await query.CountAsync();

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var results = bookings.Select(b => new BookingHistoryItem
        {
            BookingId = b.Id,
            SourceCity = b.Bus?.Route?.SourceCity ?? "",
            DestinationCity = b.Bus?.Route?.DestinationCity ?? "",
            JourneyDate = b.JourneyDate,
            SeatNumbers = b.BookingDetails.Select(bd => bd.Seat?.SeatNumber ?? "").ToList(),
            Status = b.Status,
            TotalAmount = b.TotalAmount,
            TicketUrl = b.TicketPdfPath != null ? $"/api/v1/bookings/{b.Id}/ticket" : null
        }).ToList();

        return new PagedResult<BookingHistoryItem> { Total = total, Page = page, PageSize = pageSize, Results = results };
    }

    public async Task<(bool Success, string Message, int StatusCode)> CancelBookingAsync(Guid bookingId, Guid userId, string? reason)
    {
        var booking = await _db.Bookings
            .Include(b => b.Bus)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null || booking.UserId != userId)
            return (false, "Booking not found", 404);

        if (booking.Status != "confirmed")
            return (false, "Booking is not in confirmed status", 400);

        // 2-hour window check
        var departureDateTime = booking.JourneyDate.ToDateTime(booking.Bus!.DepartureTime);
        if (DateTime.UtcNow >= departureDateTime.AddHours(-2))
            return (false, "Cancellation window closed (< 2 hours to departure)", 422);

        booking.Status = "cancelled";
        booking.CancellationReason = reason;
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        // Dummy refund
        if (booking.Payment != null)
        {
            booking.Payment.PaymentStatus = "refunded";
            booking.Payment.RefundAmount = booking.TotalAmount;
            booking.Payment.RefundInitiatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return (true, "Booking cancelled", 200);
    }

    public async Task<PagedResult<OperatorBookingItem>> GetOperatorBookingsAsync(Guid operatorId, Guid? busId, DateOnly? journeyDate, string? status, int page, int pageSize)
    {
        var query = _db.Bookings
            .Include(b => b.Bus)
            .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Seat)
            .Where(b => b.Bus != null && b.Bus.OperatorId == operatorId);

        if (busId.HasValue) query = query.Where(b => b.BusId == busId.Value);
        if (journeyDate.HasValue) query = query.Where(b => b.JourneyDate == journeyDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);

        var total = await query.CountAsync();

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var results = bookings.Select(b => new OperatorBookingItem
        {
            BookingId = b.Id,
            JourneyDate = b.JourneyDate,
            BusRegistration = b.Bus?.RegistrationNumber ?? "",
            Passengers = b.BookingDetails.Select(bd => new PassengerDto
            {
                Name = bd.PassengerName,
                Age = bd.PassengerAge,
                Gender = bd.PassengerGender,
                SeatNumber = bd.Seat?.SeatNumber ?? ""
            }).ToList(),
            TotalAmount = b.TotalAmount,
            Status = b.Status
        }).ToList();

        return new PagedResult<OperatorBookingItem> { Total = total, Page = page, PageSize = pageSize, Results = results };
    }
}
