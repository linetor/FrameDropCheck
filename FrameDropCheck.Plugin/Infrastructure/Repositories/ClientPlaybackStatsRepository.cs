using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="ClientPlaybackStats"/>.
/// </summary>
public class ClientPlaybackStatsRepository : IClientPlaybackStatsRepository
{
    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientPlaybackStatsRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public ClientPlaybackStatsRepository(FrameDropCheckDbContext dbContext)
    {
        _connection = dbContext?.Connection ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<ClientPlaybackStats> AddAsync(ClientPlaybackStats stats)
    {
        const string sql = @"
            INSERT INTO ClientPlaybackStats (
                Id, MediaId, UserId, ClientName, Timestamp,
                FramesDropped, PlaybackDuration, ValidationResult, JellyfinSessionId)
            VALUES (
                @Id, @MediaId, @UserId, @ClientName, @Timestamp,
                @FramesDropped, @PlaybackDuration, @ValidationResult, @JellyfinSessionId);";

        await _connection.ExecuteAsync(sql, stats).ConfigureAwait(false);
        return stats;
    }

    /// <inheritdoc/>
    public async Task<ClientPlaybackStats> UpsertAsync(ClientPlaybackStats stats)
    {
        if (string.IsNullOrEmpty(stats.JellyfinSessionId))
        {
            return await AddAsync(stats).ConfigureAwait(false);
        }

        const string checkSql = "SELECT Id FROM ClientPlaybackStats WHERE JellyfinSessionId = @JellyfinSessionId LIMIT 1;";
        var existingId = await _connection.ExecuteScalarAsync<string>(checkSql, new { stats.JellyfinSessionId }).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(existingId))
        {
            // UPDATE
            const string updateSql = @"
                UPDATE ClientPlaybackStats
                SET FramesDropped = @FramesDropped,
                    PlaybackDuration = @PlaybackDuration,
                    Timestamp = @Timestamp,
                    ValidationResult = @ValidationResult
                WHERE JellyfinSessionId = @JellyfinSessionId;";

            await _connection.ExecuteAsync(updateSql, stats).ConfigureAwait(false);
            stats.Id = existingId;
        }
        else
        {
            // INSERT
            await AddAsync(stats).ConfigureAwait(false);
        }

        return stats;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ClientPlaybackStats>> GetByMediaIdAsync(string mediaId)
    {
        // Normalize MediaId (remove hyphens for comparison)
        var normalizedMediaId = mediaId.Replace("-", string.Empty, StringComparison.Ordinal);

        // Query using LIKE to match both formats (with or without hyphens)
        const string sql = @"
            SELECT * FROM ClientPlaybackStats
            WHERE REPLACE(MediaId, '-', '') = @NormalizedMediaId
            ORDER BY Timestamp DESC;";

        return await _connection.QueryAsync<ClientPlaybackStats>(sql, new { NormalizedMediaId = normalizedMediaId }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ClientPlaybackStats>> GetByUserIdAsync(string userId)
    {
        const string sql = "SELECT * FROM ClientPlaybackStats WHERE UserId = @UserId ORDER BY Timestamp DESC;";
        return await _connection.QueryAsync<ClientPlaybackStats>(sql, new { UserId = userId }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ClientPlaybackStats>> GetAllAsync()
    {
        const string sql = "SELECT * FROM ClientPlaybackStats;";
        return await _connection.QueryAsync<ClientPlaybackStats>(sql).ConfigureAwait(false);
    }
}
