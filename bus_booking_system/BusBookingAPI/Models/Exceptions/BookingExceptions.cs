namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when a booking with the requested ID does not exist or does not belong to the user.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class BookingNotFoundException : Exception
{
    public BookingNotFoundException()
        : base("The requested booking was not found.") { }

    public BookingNotFoundException(Guid bookingId)
        : base($"Booking with ID '{bookingId}' was not found.") { }

    public BookingNotFoundException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when a seat lock is expired or was never created before attempting to book.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class SeatLockExpiredException : Exception
{
    public SeatLockExpiredException()
        : base("Seat lock has expired or was not found. Please re-select your seats.") { }

    public SeatLockExpiredException(Guid seatId)
        : base($"Seat lock for seat '{seatId}' has expired or was not found. Please re-select seats.") { }
}

/// <summary>
/// Thrown when attempting to book a seat that is already confirmed by another user.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class SeatAlreadyBookedException : Exception
{
    public SeatAlreadyBookedException()
        : base("One or more selected seats are already booked.") { }

    public SeatAlreadyBookedException(Guid seatId)
        : base($"Seat '{seatId}' has already been booked.") { }
}

/// <summary>
/// Thrown when trying to cancel a booking that is not in 'confirmed' state.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class BookingNotCancellableException : Exception
{
    public BookingNotCancellableException()
        : base("This booking cannot be cancelled because it is not in a confirmed state.") { }

    public BookingNotCancellableException(string currentStatus)
        : base($"Booking with status '{currentStatus}' cannot be cancelled.") { }
}

/// <summary>
/// Thrown when a user attempts to cancel a booking within the 2-hour restriction window before departure.
/// Maps to HTTP 422 Unprocessable Entity.
/// </summary>
public class CancellationWindowClosedException : Exception
{
    public CancellationWindowClosedException()
        : base("Cancellation is no longer allowed as the departure is within 2 hours.") { }

    public CancellationWindowClosedException(string message)
        : base(message) { }
}
