namespace FrameDropCheck.Plugin.Infrastructure.Data.Transactions;

/// <summary>
/// Provides extension methods for transaction related operations.
/// </summary>
public static class TransactionExtensions
{
    /// <summary>
    /// Executes an action within a transaction.
    /// </summary>
    /// <param name="unitOfWork">The unit of work instance.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ExecuteInTransactionAsync(
        this IUnitOfWork unitOfWork,
        Func<Task> action)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            await action().ConfigureAwait(false);
            await unitOfWork.CommitAsync().ConfigureAwait(false);
        }
        catch
        {
            await unitOfWork.RollbackAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Executes an action within a transaction and returns a result.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="unitOfWork">The unit of work instance.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task that represents the asynchronous operation and returns the action result.</returns>
    public static async Task<T> ExecuteInTransactionAsync<T>(
        this IUnitOfWork unitOfWork,
        Func<Task<T>> action)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync().ConfigureAwait(false);
            var result = await action().ConfigureAwait(false);
            await unitOfWork.CommitAsync().ConfigureAwait(false);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync().ConfigureAwait(false);
            throw;
        }
    }
}
