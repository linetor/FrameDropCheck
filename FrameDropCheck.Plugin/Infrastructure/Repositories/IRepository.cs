namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Base repository interface.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public interface IRepository<T, TKey>
    where T : class
{
    /// <summary>
    /// Gets all entities.
    /// </summary>
    /// <returns>A task that returns all entities.</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Gets an entity by identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <returns>A task that returns the entity if found; otherwise null.</returns>
    Task<T?> GetByIdAsync(TKey id);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>A task that returns the added entity.</returns>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Updates the specified entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes the entity with the specified identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    /// <returns>A task that represents the asynchronous delete operation.</returns>
    Task DeleteAsync(TKey id);
}
