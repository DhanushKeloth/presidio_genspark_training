namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when a bus with the requested ID does not exist in the database.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class BusNotFoundException : Exception
{
    public BusNotFoundException()
        : base("The requested bus was not found.") { }

    public BusNotFoundException(Guid busId)
        : base($"Bus with ID '{busId}' was not found.") { }

    public BusNotFoundException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when an operator tries to add a bus with a registration number that is already in use.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class DuplicateRegistrationNumberException : Exception
{
    public DuplicateRegistrationNumberException()
        : base("A bus with this registration number already exists.") { }

    public DuplicateRegistrationNumberException(string registrationNumber)
        : base($"Registration number '{registrationNumber}' is already in use.") { }
}

/// <summary>
/// Thrown when the seat layout count does not match the declared total seats value.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class SeatLayoutMismatchException : Exception
{
    public SeatLayoutMismatchException()
        : base("Total seats count does not match the number of seats in the layout.") { }

    public SeatLayoutMismatchException(int totalSeats, int layoutCount)
        : base($"total_seats ({totalSeats}) must equal seat_layout.layout.length ({layoutCount}).") { }
}

/// <summary>
/// Thrown when an operator tries to operate on a bus they do not own.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class BusOwnershipException : Exception
{
    public BusOwnershipException()
        : base("You do not have permission to modify this bus.") { }

    public BusOwnershipException(string message)
        : base(message) { }
}
