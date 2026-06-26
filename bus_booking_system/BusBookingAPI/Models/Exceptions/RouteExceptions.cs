namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when a route with the requested ID does not exist or is inactive.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class RouteNotFoundException : Exception
{
    public RouteNotFoundException()
        : base("The requested route was not found or is inactive.") { }

    public RouteNotFoundException(Guid routeId)
        : base($"Route with ID '{routeId}' was not found or is inactive.") { }

    public RouteNotFoundException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when an admin tries to create a route that already exists (same source–destination).
/// Maps to HTTP 409 Conflict.
/// </summary>
public class DuplicateRouteException : Exception
{
    public DuplicateRouteException()
        : base("A route with the same source and destination already exists.") { }

    public DuplicateRouteException(string source, string destination)
        : base($"Route from '{source}' to '{destination}' already exists.") { }
}
