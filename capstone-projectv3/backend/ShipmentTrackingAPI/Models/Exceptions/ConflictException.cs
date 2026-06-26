using ShipmentTrackingAPI.Models.Exceptions;

namespace ShipmentTrackingAPI.Models.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message) { }
    }
}