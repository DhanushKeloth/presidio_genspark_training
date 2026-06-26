using ShipmentTrackingAPI.Models.Exceptions;

namespace ShipmentTrackingAPI.Models.Exceptions
{
    public class NotFoundException : AppException
    {
        
        public NotFoundException(string message) : base(message) { }
    }
}