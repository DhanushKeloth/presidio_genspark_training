namespace ShipmentTrackingAPI.Models.Exceptions
{
    public class RateLimitException : AppException
    {
        public RateLimitException(string message) : base(message) { }
    }
}