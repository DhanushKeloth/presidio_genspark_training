namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when an operator with the requested ID does not exist in the database.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class OperatorNotFoundException : Exception
{
    public OperatorNotFoundException()
        : base("The requested operator was not found.") { }

    public OperatorNotFoundException(Guid operatorId)
        : base($"Operator with ID '{operatorId}' was not found.") { }

    public OperatorNotFoundException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when an invalid action string is provided for operator status updates (e.g., not approve/reject/disable).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class InvalidOperatorActionException : Exception
{
    public InvalidOperatorActionException()
        : base("Invalid operator action. Allowed values are: approve, reject, disable.") { }

    public InvalidOperatorActionException(string action)
        : base($"'{action}' is not a valid operator action. Allowed values are: approve, reject, disable.") { }
}
