using System.Data.Common;
using FrameDropCheck.Plugin.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace FrameDropCheck.Plugin.Infrastructure.Data.Transactions;

/// <summary>
/// Provides a SQLite-backed implementation of <see cref="IUnitOfWork"/>.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly FrameDropCheckDbContext _dbContext;
    private DbTransaction? _transaction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UnitOfWork(FrameDropCheckDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task BeginTransactionAsync()
    {
        var sqliteConn = _dbContext.Connection as SqliteConnection;
        var tx = await sqliteConn!.BeginTransactionAsync().ConfigureAwait(false);
        _transaction = tx;
    }

    /// <inheritdoc/>
    public async Task CommitAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            await RollbackAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
        }
    }

    /// <inheritdoc/>
    public async Task RollbackAsync()
    {
        try
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
        }
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs resource cleanup.
    /// </summary>
    /// <param name="disposing">True to release managed resources; otherwise false.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
            }

            _disposed = true;
        }
    }
}
