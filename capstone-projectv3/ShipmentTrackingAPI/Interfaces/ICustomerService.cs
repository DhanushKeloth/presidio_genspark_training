using ShipmentTrackingAPI.DTOs.Customer;


namespace ShipmentTrackingAPI.Services.Interfaces
{
    public interface ICustomerService
    {
        // ==========================================
        // PROFILE MANAGEMENT
        // ==========================================
        Task<CustomerProfileDto> GetProfileAsync(int userId);
        
        Task<CustomerProfileDto> UpdateProfileAsync(int userId, UpdateProfileRequestDto req);

        // ==========================================
        // ADDRESS BOOK MANAGEMENT
        // ==========================================
        Task<List<SavedAddressDto>> GetSavedAddressesAsync(int userId);
        
        Task<SavedAddressDto> AddSavedAddressAsync(int userId, SavedAddressRequestDto req);
        
        Task<SavedAddressDto> UpdateSavedAddressAsync(int userId, int addressId, SavedAddressRequestDto req);
        
        Task DeleteSavedAddressAsync(int userId, int addressId);
    }
}