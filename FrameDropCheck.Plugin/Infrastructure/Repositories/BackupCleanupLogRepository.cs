using System.Data;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="BackupCleanupLog"/>.
/// </summary>
public class BackupCleanupLogRepository : IBackupCleanupLogRepository
{
    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupCleanupLogRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public BackupCleanupLogRepository(FrameDropCheckDbContext dbContext)
    {
        _connection = dbContext.Connection;
    }

    /// <inheritdoc/>
    public async Task<BackupCleanupLog> AddAsync(BackupCleanupLog entity)
    {
        const string sql = @"
            INSERT INTO BackupCleanupLog (MediaId, BackupPath, CleanupTime)
            VALUES (@MediaId, @BackupPath, @CleanupTime);
            SELECT * FROM BackupCleanupLog WHERE CleanupId = last_insert_rowid();";

    return await _connection.QuerySingleAsync<BackupCleanupLog>(sql, entity).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM BackupCleanupLog WHERE CleanupId = @Id;";
    await _connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackupCleanupLog>> GetAllAsync()
    {
        const string sql = @"
            SELECT l.*, m.*
            FROM BackupCleanupLog l
            LEFT JOIN Media m ON l.MediaId = m.MediaId;";

        var logDict = new Dictionary<int, BackupCleanupLog>();

        await _connection.QueryAsync<BackupCleanupLog, Media, BackupCleanupLog>(
            sql,
            (log, media) =>
            {
                if (!logDict.TryGetValue(log.CleanupId, out var existingLog))
                {
                    existingLog = log;
                    logDict.Add(log.CleanupId, existingLog);
                }

                existingLog.Media = media;

                return existingLog;
            },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return logDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackupCleanupLog>> GetByBackupPathAsync(string backupPath)
    {
        const string sql = @"
            SELECT l.*, m.*
            FROM BackupCleanupLog l
            LEFT JOIN Media m ON l.MediaId = m.MediaId
            WHERE l.BackupPath LIKE @BackupPath;";

        var logDict = new Dictionary<int, BackupCleanupLog>();

        await _connection.QueryAsync<BackupCleanupLog, Media, BackupCleanupLog>(
            sql,
            (log, media) =>
            {
                if (!logDict.TryGetValue(log.CleanupId, out var existingLog))
                {
                    existingLog = log;
                    logDict.Add(log.CleanupId, existingLog);
                }

                existingLog.Media = media;

                return existingLog;
            },

            new { BackupPath = $"%{backupPath}%" },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return logDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackupCleanupLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT l.*, m.*
            FROM BackupCleanupLog l
            LEFT JOIN Media m ON l.MediaId = m.MediaId
            WHERE l.CleanupTime BETWEEN @StartDate AND @EndDate;";

        var logDict = new Dictionary<int, BackupCleanupLog>();
        await _connection.QueryAsync<BackupCleanupLog, Media, BackupCleanupLog>(
            sql,
            (log, media) =>
            {
                if (!logDict.TryGetValue(log.CleanupId, out var existingLog))
                {
                    existingLog = log;
                    logDict.Add(log.CleanupId, existingLog);
                }
                existingLog.Media = media;
                return existingLog;
            },

            new { StartDate = startDate, EndDate = endDate },

            splitOn: "MediaId").ConfigureAwait(false);

        return logDict.Values;
    }

    /// <inheritdoc/>
    public async Task<BackupCleanupLog?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT l.*, m.*
            FROM BackupCleanupLog l
            LEFT JOIN Media m ON l.MediaId = m.MediaId
            WHERE l.CleanupId = @Id;";

        var logs = await _connection.QueryAsync<BackupCleanupLog, Media, BackupCleanupLog>(
            sql,
            (log, media) =>
            {
                log.Media = media;
                return log;
            },

            new { Id = id },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return logs.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackupCleanupLog>> GetByMediaIdAsync(string mediaId)
    {
        const string sql = @"
            SELECT l.*, m.*
            FROM BackupCleanupLog l
            LEFT JOIN Media m ON l.MediaId = m.MediaId
            WHERE l.MediaId = @MediaId;";

        var logDict = new Dictionary<int, BackupCleanupLog>();

        await _connection.QueryAsync<BackupCleanupLog, Media, BackupCleanupLog>(
            sql,
            (log, media) =>
            {
                if (!logDict.TryGetValue(log.CleanupId, out var existingLog))
                {
                    existingLog = log;
                    logDict.Add(log.CleanupId, existingLog);
                }
                existingLog.Media = media;
                return existingLog;
            },

            new { MediaId = mediaId },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return logDict.Values;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(BackupCleanupLog entity)
    {
        const string sql = @"
            UPDATE BackupCleanupLog
            SET BackupPath = @BackupPath,
                CleanupTime = @CleanupTime
            WHERE CleanupId = @CleanupId;";

    await _connection.ExecuteAsync(sql, entity).ConfigureAwait(false);
    }
}
