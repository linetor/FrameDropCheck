using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for managing client playback statistics.
/// </summary>
public interface IClientPlaybackStatsRepository
{
    /// <summary>
    /// Adds a new playback statistic record.
    /// </summary>
    /// <param name="stats">The stats to add.</param>
    /// <returns>The added stats.</returns>
    Task<ClientPlaybackStats> AddAsync(ClientPlaybackStats stats);

    /// <summary>
    /// Adds or updates a playback statistic record.
    /// Deduplication is based on JellyfinSessionId if present.
    /// </summary>
    /// <param name="stats">The stats to upsert.</param>
    /// <returns>The upserted stats.</returns>
    Task<ClientPlaybackStats> UpsertAsync(ClientPlaybackStats stats);

    /// <summary>
    /// Gets statistics for a specific media item.
    /// </summary>
    /// <param name="mediaId">The media identifier.</param>
    /// <returns>List of stats.</returns>
    Task<IEnumerable<ClientPlaybackStats>> GetByMediaIdAsync(string mediaId);

    /// <summary>
    /// Gets statistics for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>List of stats.</returns>
    Task<IEnumerable<ClientPlaybackStats>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Gets all statistics.
    /// </summary>
    /// <returns>All stats.</returns>
    Task<IEnumerable<ClientPlaybackStats>> GetAllAsync();
}
