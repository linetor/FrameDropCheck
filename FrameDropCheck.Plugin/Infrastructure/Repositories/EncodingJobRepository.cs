using System.Data;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="EncodingJob"/>.
/// </summary>
public class EncodingJobRepository : IEncodingJobRepository
{
    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="EncodingJobRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public EncodingJobRepository(FrameDropCheckDbContext dbContext)
    {
        _connection = dbContext.Connection;
    }

    /// <inheritdoc/>
    public async Task<EncodingJob> AddAsync(EncodingJob entity)
    {
        const string sql = @"
            INSERT INTO EncodingJob (
                MediaId, OriginalPath, BackupPath, NewFilePath,
                StartTime, EndTime, Status, ErrorMessage)
            VALUES (
                @MediaId, @OriginalPath, @BackupPath, @NewFilePath,
                @StartTime, @EndTime, @Status, @ErrorMessage);
            SELECT * FROM EncodingJob WHERE JobId = last_insert_rowid();";

    return await _connection.QuerySingleAsync<EncodingJob>(sql, entity).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM EncodingJob WHERE JobId = @Id;";
    await _connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EncodingJob>> GetAllAsync()
    {
        const string sql = @"
            SELECT j.*, m.*
            FROM EncodingJob j
            LEFT JOIN Media m ON j.MediaId = m.MediaId;";

        var jobDict = new Dictionary<int, EncodingJob>();

        await _connection.QueryAsync<EncodingJob, Media, EncodingJob>(
            sql,
            (job, media) =>
            {
                if (!jobDict.TryGetValue(job.JobId, out var existingJob))
                {
                    existingJob = job;
                    jobDict.Add(job.JobId, existingJob);
                }

                existingJob.Media = media;

                return existingJob;
            },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return jobDict.Values;
    }

    /// <inheritdoc/>
    public async Task<EncodingJob?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT j.*, m.*
            FROM EncodingJob j
            LEFT JOIN Media m ON j.MediaId = m.MediaId
            WHERE j.JobId = @Id;";

        var jobs = await _connection.QueryAsync<EncodingJob, Media, EncodingJob>(
            sql,
            (job, media) =>
            {
                job.Media = media;
                return job;
            },

            new { Id = id },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return jobs.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EncodingJob>> GetByMediaIdAsync(string mediaId)
    {
        const string sql = @"
            SELECT j.*, m.*
            FROM EncodingJob j
            LEFT JOIN Media m ON j.MediaId = m.MediaId
            WHERE j.MediaId = @MediaId;";

        var jobDict = new Dictionary<int, EncodingJob>();

        await _connection.QueryAsync<EncodingJob, Media, EncodingJob>(
            sql,
            (job, media) =>
            {
                if (!jobDict.TryGetValue(job.JobId, out var existingJob))
                {
                    existingJob = job;
                    jobDict.Add(job.JobId, existingJob);
                }
                existingJob.Media = media;
                return existingJob;
            },

            new { MediaId = mediaId },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return jobDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EncodingJob>> GetByStatusAsync(string status)
    {
        const string sql = @"
            SELECT j.*, m.*
            FROM EncodingJob j
            LEFT JOIN Media m ON j.MediaId = m.MediaId
            WHERE j.Status = @Status;";

        var jobDict = new Dictionary<int, EncodingJob>();
        await _connection.QueryAsync<EncodingJob, Media, EncodingJob>(
            sql,
            (job, media) =>
            {
                if (!jobDict.TryGetValue(job.JobId, out var existingJob))
                {
                    existingJob = job;
                    jobDict.Add(job.JobId, existingJob);
                }
                existingJob.Media = media;
                return existingJob;
            },

            new { Status = status },

            splitOn: "MediaId").ConfigureAwait(false);

    return jobDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EncodingJob>> GetFailedJobsAsync()
    {
        return await GetByStatusAsync("failed").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EncodingJob>> GetInProgressJobsAsync()
    {
        return await GetByStatusAsync("in-progress").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(EncodingJob entity)
    {
        const string sql = @"
            UPDATE EncodingJob
            SET BackupPath = @BackupPath,
                NewFilePath = @NewFilePath,
                StartTime = @StartTime,
                EndTime = @EndTime,
                Status = @Status,
                ErrorMessage = @ErrorMessage
            WHERE JobId = @JobId;";

    await _connection.ExecuteAsync(sql, entity).ConfigureAwait(false);
    }
}
