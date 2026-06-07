using ShipmentTrackingAPI.Models; // Assuming your User entity is in this namespace

namespace ShipmentTrackingAPI.Repositories.RepoInterfaces
{
    public interface IUserRepository : IRepository<User>
    {
        /// <summary>
        /// Retrieves a user by their email address. 
        /// Backed by a citext column in PostgreSQL for case-insensitive matching.
        /// </summary>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>
        /// Highly optimized pre-registration check. Returns true if the email is already in use.
        /// </summary>
        Task<bool> EmailExistsAsync(string email);
    }
}