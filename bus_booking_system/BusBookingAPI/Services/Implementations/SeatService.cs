using BusBookingAPI.Data;
using BusBookingAPI.Models.DTOs;
using BusBookingAPI.Models.Entities;
using BusBookingAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BusBookingAPI.Services.Implementations;

public class SeatService : ISeatService
{
    private readonly BusBookingDbContext _db;

    public SeatService(BusBookingDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, SeatLockResponse? Response, string Message)> LockSeatsAsync(Guid userId, SeatLockRequest request)
    {
        using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var lockExpiry = DateTime.UtcNow.AddMinutes(10);

            foreach (var seatId in request.SeatIds)
            {
                // Check for existing active lock using raw SQL FOR UPDATE SKIP LOCKED
                var existingLock = await _db.SeatLocks
                    .FromSqlRaw(
                        @"SELECT * FROM seat_locks
                          WHERE seat_id = {0} AND journey_date = {1} AND lock_expiry > NOW()
                          FOR UPDATE SKIP LOCKED",
                        seatId, request.JourneyDate)
                    .FirstOrDefaultAsync();

                if (existingLock != null)
                {
                    await tx.RollbackAsync();
                    return (false, null, $"Seat is already locked or booked");
                }

                // Check if seat is already booked
                var isBooked = await _db.BookingDetails
                    .AnyAsync(bd =>
                        bd.SeatId == seatId &&
                        bd.Booking!.JourneyDate == request.JourneyDate &&
                        bd.Booking.Status == "confirmed");

                if (isBooked)
                {
                    await tx.RollbackAsync();
                    return (false, null, $"Seat is already booked");
                }

                var seatLock = new SeatLock
                {
                    SeatId = seatId,
                    UserId = userId,
                    BusId = request.BusId,
                    JourneyDate = request.JourneyDate,
                    LockExpiry = lockExpiry
                };
                _db.SeatLocks.Add(seatLock);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, new SeatLockResponse
            {
                LockExpiry = lockExpiry,
                LockedSeats = request.SeatIds
            }, "Seats locked successfully");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Success, string Message)> UnlockSeatsAsync(Guid userId, SeatUnlockRequest request)
    {
        var locks = await _db.SeatLocks
            .Where(sl =>
                request.SeatIds.Contains(sl.SeatId) &&
                sl.BusId == request.BusId &&
                sl.JourneyDate == request.JourneyDate)
            .ToListAsync();

        foreach (var lk in locks)
        {
            if (lk.UserId != userId)
                return (false, "Forbidden: seat lock belongs to another user");
        }

        _db.SeatLocks.RemoveRange(locks);
        await _db.SaveChangesAsync();
        return (true, "Seats released");
    }
}
