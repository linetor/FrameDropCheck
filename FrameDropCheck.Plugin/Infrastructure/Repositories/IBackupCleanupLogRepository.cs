using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the <see cref="BackupCleanupLog"/> entity.
/// </summary>
public interface IBackupCleanupLogRepository : IRepository<BackupCleanupLog, int>
{
    /// <summary>
    /// Gets backup cleanup logs by media identifier.
    /// </summary>
    /// <param name="mediaId">The media identifier.</param>
    /// <returns>A task that returns the matching cleanup logs.</returns>
    Task<IEnumerable<BackupCleanupLog>> GetByMediaIdAsync(string mediaId);

    /// <summary>
    /// Gets backup cleanup logs within a date range.
    /// </summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="endDate">The end date.</param>
    /// <returns>A task that returns the matching cleanup logs.</returns>
    Task<IEnumerable<BackupCleanupLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Gets backup cleanup logs for a specific backup path.
    /// </summary>
    /// <param name="backupPath">The backup path.</param>
    /// <returns>A task that returns the matching cleanup logs.</returns>
    Task<IEnumerable<BackupCleanupLog>> GetByBackupPathAsync(string backupPath);
}
