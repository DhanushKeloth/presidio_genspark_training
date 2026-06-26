using ShipmentTrackingAPI.Models.Exceptions;

namespace ShipmentTrackingAPI.Models.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message) { }
    }
}