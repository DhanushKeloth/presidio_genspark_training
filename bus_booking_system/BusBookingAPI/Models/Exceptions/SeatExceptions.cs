namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when a user attempts to lock a seat that is already locked by another session.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class SeatAlreadyLockedException : Exception
{
    public SeatAlreadyLockedException()
        : base("One or more selected seats are already locked by another user.") { }

    public SeatAlreadyLockedException(Guid seatId)
        : base($"Seat '{seatId}' is currently locked by another user.") { }
}

/// <summary>
/// Thrown when a user tries to unlock a seat lock that belongs to a different user.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class SeatLockOwnershipException : Exception
{
    public SeatLockOwnershipException()
        : base("You are not authorized to release this seat lock.") { }

    public SeatLockOwnershipException(string message)
        : base(message) { }
}
