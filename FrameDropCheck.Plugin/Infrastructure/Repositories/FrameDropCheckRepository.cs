using System.Data;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="FrameDropCheck.Plugin.Domain.Models.FrameDropCheck"/>.
/// </summary>
public class FrameDropCheckRepository : IFrameDropCheckRepository
{
    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDropCheckRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public FrameDropCheckRepository(FrameDropCheckDbContext dbContext)
    {
        _connection = dbContext.Connection;
    }

    /// <inheritdoc/>
    public async Task<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck> AddAsync(FrameDropCheck.Plugin.Domain.Models.FrameDropCheck entity)
    {
        const string sql = @"
            INSERT INTO FrameDropCheck (
                MediaId, CheckStartTime, CheckEndTime, HasFrameDrop,
                FrameDropCount, TotalFrameCount, LogAnalysisResult, PlaybackAnalysisResult, Status)
            VALUES (
                @MediaId, @CheckStartTime, @CheckEndTime, @HasFrameDrop,
                @FrameDropCount, @TotalFrameCount, @LogAnalysisResult, @PlaybackAnalysisResult, @Status);
            SELECT * FROM FrameDropCheck WHERE CheckId = last_insert_rowid();";

        return await _connection.QuerySingleAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(sql, entity).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM FrameDropCheck WHERE CheckId = @Id;";
        await _connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetAllAsync()
    {
        const string sql = @"
            SELECT f.*, m.*
            FROM FrameDropCheck f
            LEFT JOIN Media m ON f.MediaId = m.MediaId;";

        var frameDropDict = new Dictionary<int, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>();

        await _connection.QueryAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, Media, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(
            sql,
            (frameDrop, media) =>
            {
                if (!frameDropDict.TryGetValue(frameDrop.CheckId, out var existingFrameDrop))
                {
                    existingFrameDrop = frameDrop;
                    frameDropDict.Add(frameDrop.CheckId, existingFrameDrop);
                }

                existingFrameDrop.Media = media;

                return existingFrameDrop;
            },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return frameDropDict.Values;
    }

    /// <inheritdoc/>
    public async Task<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT f.*, m.*
            FROM FrameDropCheck f
            LEFT JOIN Media m ON f.MediaId = m.MediaId
            WHERE f.CheckId = @Id;";

        var frameDrops = await _connection.QueryAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, Media, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(
            sql,
            (frameDrop, media) =>
            {
                frameDrop.Media = media;
                return frameDrop;
            },

            new { Id = id },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return frameDrops.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetByMediaIdAsync(string mediaId)
    {
        const string sql = @"
            SELECT f.*, m.*
            FROM FrameDropCheck f
            LEFT JOIN Media m ON f.MediaId = m.MediaId
            WHERE f.MediaId = @MediaId;";

        var frameDropDict = new Dictionary<int, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>();

        await _connection.QueryAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, Media, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(
            sql,
            (frameDrop, media) =>
            {
                if (!frameDropDict.TryGetValue(frameDrop.CheckId, out var existingFrameDrop))
                {
                    existingFrameDrop = frameDrop;
                    frameDropDict.Add(frameDrop.CheckId, existingFrameDrop);
                }

                existingFrameDrop.Media = media;
                return existingFrameDrop;
            },

            new { MediaId = mediaId },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return frameDropDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetByStatusAsync(string status)
    {
        const string sql = @"
            SELECT f.*, m.*
            FROM FrameDropCheck f
            LEFT JOIN Media m ON f.MediaId = m.MediaId
            WHERE f.Status = @Status;";

        var frameDropDict = new Dictionary<int, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>();

        await _connection.QueryAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, Media, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(
            sql,
            (frameDrop, media) =>
            {
                if (!frameDropDict.TryGetValue(frameDrop.CheckId, out var existingFrameDrop))
                {
                    existingFrameDrop = frameDrop;
                    frameDropDict.Add(frameDrop.CheckId, existingFrameDrop);
                }
                existingFrameDrop.Media = media;
                return existingFrameDrop;
            },

            new { Status = status },

            splitOn: "MediaId"
        ).ConfigureAwait(false);

        return frameDropDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetWithFrameDropsAsync()
    {
        const string sql = @"
            SELECT f.*, m.*
            FROM FrameDropCheck f
            LEFT JOIN Media m ON f.MediaId = m.MediaId
            WHERE f.HasFrameDrop = 1;";

        var frameDropDict = new Dictionary<int, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>();
        await _connection.QueryAsync<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, Media, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>(
            sql,
            (frameDrop, media) =>
            {
                if (!frameDropDict.TryGetValue(frameDrop.CheckId, out var existingFrameDrop))
                {
                    existingFrameDrop = frameDrop;
                    frameDropDict.Add(frameDrop.CheckId, existingFrameDrop);
                }
                existingFrameDrop.Media = media;
                return existingFrameDrop;
            },

            splitOn: "MediaId");

        return frameDropDict.Values;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(FrameDropCheck.Plugin.Domain.Models.FrameDropCheck entity)
    {
        const string sql = @"
            UPDATE FrameDropCheck
            SET CheckStartTime = @CheckStartTime,
                CheckEndTime = @CheckEndTime,
                HasFrameDrop = @HasFrameDrop,
                FrameDropCount = @FrameDropCount,
                TotalFrameCount = @TotalFrameCount,
                LogAnalysisResult = @LogAnalysisResult,
                PlaybackAnalysisResult = @PlaybackAnalysisResult,
                Status = @Status
            WHERE CheckId = @CheckId;";

        await _connection.ExecuteAsync(sql, entity).ConfigureAwait(false);
    }
}
