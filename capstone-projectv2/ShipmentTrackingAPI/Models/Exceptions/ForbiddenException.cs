using ShipmentTrackingAPI.Models.Exceptions;

namespace ShipmentTrackingAPI.Models.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message) { }
    }
}