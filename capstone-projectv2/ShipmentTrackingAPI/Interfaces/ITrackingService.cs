namespace ShipmentTrackingAPI.Services.Interfaces
{
    public interface ITrackingService
    {
        void RegisterConnection(int userId, string connectionId);
        void RemoveConnection(string connectionId);
        Task BroadcastLocationUpdateAsync(string trackingNumber, double lat, double lng);
        Task BroadcastStatusUpdateAsync(string trackingNumber, string newStatus, string description);
        Task PushOtpToSenderAsync(string trackingNumber, string otpCode, DateTime expiresAt);
        Task PushOtpToRecipientAsync(string trackingNumber, string otpCode, DateTime expiresAt);
        Task BroadcastDriverArrivedAsync(string trackingNumber, double lat, double lng);
        Task BroadcastDeliverySuccessAsync(string trackingNumber);
    }
}