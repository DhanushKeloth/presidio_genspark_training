using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data; // Assuming AppDbContext is here
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        // Pass the AppDbContext down to the BaseRepository
        public UserRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            // PostgreSQL 'citext' handles case-insensitivity natively at the database level,
            // so we don't need to force .ToLower() here in the LINQ query.
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            // AnyAsync is highly optimized. It generates an IF EXISTS(...) SQL query 
            // rather than pulling full records into memory.
            return await _dbSet.AnyAsync(u => u.Email == email);
        }
    }
}