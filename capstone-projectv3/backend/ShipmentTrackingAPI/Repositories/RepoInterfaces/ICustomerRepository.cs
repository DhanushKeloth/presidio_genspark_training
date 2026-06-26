using ShipmentTrackingAPI.Models;

namespace ShipmentTrackingAPI.Repositories.RepoInterfaces
{
    public interface ICustomerRepository : IRepository<CustomerProfile>
    {
        // ==========================================
        // PROFILE MANAGEMENT
        // ==========================================

        /// <summary>
        /// 1:1 lookup for CustomerProfile. Created during registration.
        /// </summary>
        Task<CustomerProfile?> GetByUserIdAsync(int userId);

        // ==========================================
        // ADDRESS BOOK MANAGEMENT
        // ==========================================

        /// <summary>
        /// Retrieves all saved addresses for the customer. 
        /// Ordered by is_default DESC, created_at DESC.
        /// </summary>
        Task<List<SavedAddress>> GetSavedAddressesAsync(int customerId);

        /// <summary>
        /// Retrieves a specific address. 
        /// Ownership is enforced at the query level by including customerId in the WHERE clause.
        /// </summary>
        Task<SavedAddress?> GetSavedAddressByIdAsync(int addressId, int customerId);

        /// <summary>
        /// Bulk update operation. Sets is_default = false on all rows for the customer. 
        /// Called immediately before setting a new default address to enforce the "One Default" rule.
        /// </summary>
        Task ClearDefaultAddressAsync(int customerId);

        /// <summary>
        /// Inserts one SavedAddress row.
        /// </summary>
        Task AddSavedAddressAsync(SavedAddress addr);

        /// <summary>
        /// Hard delete. Saved addresses carry no audit requirement, so they can be physically removed from the table.
        /// </summary>
        Task DeleteSavedAddressAsync(SavedAddress addr);

        /// <summary>
        /// Updates an existing SavedAddress row.
        /// </summary>
        Task UpdateAddressAsync(SavedAddress address);
    }
}