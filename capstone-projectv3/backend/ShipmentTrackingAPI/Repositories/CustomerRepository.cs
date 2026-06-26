using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Repositories
{
    /// <summary>
    /// Handles database operations for CustomerProfile and SavedAddress.
    /// </summary>
    public class CustomerRepository : BaseRepository<CustomerProfile>, ICustomerRepository
    {
        public CustomerRepository(AppDbContext ctx) : base(ctx) { }

        /// <summary>
        /// Retrieves a customer profile by their User ID, including the linked User data.
        /// </summary>
        public async Task<CustomerProfile?> GetByUserIdAsync(int userId)
        {
            return await _ctx.CustomerProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.UserId == userId);
        }

        /// <summary>
        /// Returns all saved addresses for a customer, ordered by default status then creation date.
        /// </summary>
        public async Task<List<SavedAddress>> GetSavedAddressesAsync(int customerId)
        {
            return await _ctx.SavedAddresses
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Ownership-enforced lookup. Ensures a customer can only retrieve their own address.
        /// </summary>
        public async Task<SavedAddress?> GetSavedAddressByIdAsync(int addressId, int customerId)
        {
            // FIX: Ensure a.Id is checked against addressId, not customerId
            return await _ctx.SavedAddresses
                .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId);
        }

        /// <summary>
        /// Bulk update to remove the 'default' flag from all of a customer's addresses.
        /// </summary>
        public async Task ClearDefaultAddressAsync(int customerId)
        {
            await _ctx.SavedAddresses
                .Where(a => a.CustomerId == customerId && a.IsDefault)
                .ExecuteUpdateAsync(setter => setter.SetProperty(a => a.IsDefault, false));
        }

        /// <summary>
        /// Adds a new SavedAddress to the change tracker.
        /// </summary>
        public async Task AddSavedAddressAsync(SavedAddress address)
        {
            await _ctx.SavedAddresses.AddAsync(address);
        }

        /// <summary>
        /// Hard-deletes a SavedAddress row from the DbContext.
        /// </summary>
        public Task DeleteSavedAddressAsync(SavedAddress address)
        {
            _ctx.SavedAddresses.Remove(address);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks a SavedAddress as modified in the EF change tracker.
        /// </summary>
        public Task UpdateAddressAsync(SavedAddress address)
        {
            _ctx.SavedAddresses.Update(address);
            return Task.CompletedTask;
        }
    }
}