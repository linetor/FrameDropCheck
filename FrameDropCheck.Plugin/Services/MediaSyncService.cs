using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using FrameDropCheck.Plugin.Infrastructure.Repositories;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service responsible for synchronizing media items from Jellyfin to the local plugin database.
/// </summary>
public class MediaSyncService
{
    private readonly IJellyfinApiService _jellyfinApi;
    private readonly IMediaRepository _mediaRepo;
    private readonly IAppLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSyncService"/> class.
    /// </summary>
    /// <param name="jellyfinApi">The Jellyfin API service.</param>
    /// <param name="mediaRepo">The media repository.</param>
    /// <param name="logger">The application logger.</param>
    public MediaSyncService(
        IJellyfinApiService jellyfinApi,
        IMediaRepository mediaRepo,
        IAppLogger logger)
    {
        _jellyfinApi = jellyfinApi;
        _mediaRepo = mediaRepo;
        _logger = logger;
    }

    /// <summary>
    /// Synchronizes the local database with the Jellyfin library.
    /// Fetches all video IDs and ensures they exist in the local DB.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SyncLibraryAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MediaSyncService] Starting library synchronization...");

        try
        {
            var jellyfinVideoIds = await _jellyfinApi.GetAllVideoMediaIdsAsync();
            var localMedia = await _mediaRepo.GetAllAsync();
            var localMediaIds = localMedia.Select(m => m.MediaId).ToHashSet();

            int addedCount = 0;

            foreach (var videoId in jellyfinVideoIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!localMediaIds.Contains(videoId))
                {
                    // Fetch full details
                    var media = await _jellyfinApi.GetMediaAsync(videoId);
                    if (media != null)
                    {
                        await _mediaRepo.AddAsync(media);
                        addedCount++;
                        _logger.LogInformation($"[MediaSyncService] Discovered and added new media: {media.Name} (ID: {videoId})");
                    }
                    else
                    {
                         _logger.LogWarning($"[MediaSyncService] Found ID {videoId} but failed to fetch details.");
                    }
                }
            }

            _logger.LogInformation($"[MediaSyncService] Synchronization complete. Added {addedCount} new media items.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MediaSyncService] Library synchronization failed: {ex.Message}", ex);
        }
    }
}
