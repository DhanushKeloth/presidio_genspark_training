using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Data; // Assuming your AppDbContext lives here
using ShipmentTrackingAPI.Repositories.RepoInterfaces;

namespace ShipmentTrackingAPI.Repositories
{
    
    public abstract class BaseRepository<T> : IRepository<T> where T : class
    {
        // Protected fields so concrete repositories (e.g., ShipmentRepository) 
        // can access them directly for custom domain-specific queries.
        protected readonly AppDbContext _ctx;
        protected readonly DbSet<T> _dbSet;

        // Injected DbContext. All concrete repos call base(ctx) to set this.
        protected BaseRepository(AppDbContext ctx)
        {
            _ctx = ctx;
            _dbSet = ctx.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            // FindAsync checks the EF Core local ChangeTracker cache before querying the database, making it highly optimized.
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual Task UpdateAsync(T entity)
        {
            // Update is synchronous in EF Core because it only modifies the internal ChangeTracker state.
            // We return Task.CompletedTask to satisfy the async interface contract.
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual async Task SaveAsync()
        {
            // The single transaction boundary commitment.
            await _ctx.SaveChangesAsync();
        }

        // ==========================================
        // ADDITIONAL METHODS FROM IRepository<T>
        // ==========================================

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
        }

        public virtual Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }
    }
}