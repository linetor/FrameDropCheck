namespace FrameDropCheck.Plugin.Infrastructure.Data.Transactions;

/// <summary>
/// Provides transaction management capabilities.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Begins a transaction asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commits the current transaction asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitAsync();

    /// <summary>
    /// Rolls back the current transaction asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackAsync();
}
