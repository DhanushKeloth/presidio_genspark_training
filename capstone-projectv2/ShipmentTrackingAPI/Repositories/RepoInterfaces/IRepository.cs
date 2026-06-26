using System.Linq.Expressions;

namespace ShipmentTrackingAPI.Repositories.RepoInterfaces
{
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Basic PK lookup.
        /// </summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// Adds entity to DbContext. Does not call SaveChanges.
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// Marks entity Modified. Does not call SaveChanges.
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Calls SaveChangesAsync. Explicitly commits the transaction boundary.
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// Finds entities matching a specific condition (e.g., finding default addresses).
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Removes an entity from the DbContext (Hard delete).
        /// </summary>
        Task DeleteAsync(T entity);
    }
}