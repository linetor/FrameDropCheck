using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Jellyfin.Data.Enums; // Required for MediaType enum

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// A native Jellyfin API service implementation using direct references to Jellyfin Core.
/// </summary>
public class InternalJellyfinApiService : IJellyfinApiService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ISessionManager _sessionManager;
    private readonly IMediaRepository _mediaRepo;
    private readonly IAppLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalJellyfinApiService"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="sessionManager">The Jellyfin session manager.</param>
    /// <param name="mediaRepo">The local media repository.</param>
    /// <param name="logger">The application logger.</param>
    public InternalJellyfinApiService(
        ILibraryManager libraryManager,
        ISessionManager sessionManager,
        IMediaRepository mediaRepo,
        IAppLogger logger)
    {
        _libraryManager = libraryManager;
        _sessionManager = sessionManager;
        _mediaRepo = mediaRepo;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Media?> GetMediaAsync(string mediaId)
    {
        // 1) Try local DB first
        try
        {
            var local = await _mediaRepo.GetByIdAsync(mediaId).ConfigureAwait(false);
            if (local != null)
            {
                return local;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to query local Media repository: {ex.Message}");
        }

        // 2) Resolve from Jellyfin Library
        return await ResolveMediaFromJellyfinAsync(mediaId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<Media?> ResolveMediaFromJellyfinAsync(string mediaId)
    {
        if (!Guid.TryParse(mediaId, out var guid))
        {
            return Task.FromResult<Media?>(null);
        }

        var item = _libraryManager.GetItemById(guid);
        if (item == null || item is not MediaBrowser.Controller.Entities.Video)
        {
            return Task.FromResult<Media?>(null);
        }

        return Task.FromResult(MapToDomainMedia(item));
    }

    /// <inheritdoc/>
    public Task<string> GetPlaybackDiagnosticsAsync(string mediaId)
    {
        long droppedFrames = 0;

        try
        {
            foreach (var session in _sessionManager.Sessions)
            {
                if (session.NowPlayingItem != null)
                {
                    bool match = string.Equals(session.NowPlayingItem.Id.ToString(), mediaId, StringComparison.OrdinalIgnoreCase)
                                 || (Guid.TryParse(mediaId, out var g) && session.NowPlayingItem.Id == g);

                    // Note: PlayerStateInfo.DroppedFrames property might not exist in all Jellyfin versions or might be removed.
                    // If it's missing, we rely on client-side reporting via PlaybackMonitor.
                    // For now, we return 0 to avoid compilation errors if property is missing.
                    /*
                    if (match && session.PlayState != null && session.PlayState.DroppedFrames.HasValue)
                    {
                        droppedFrames = session.PlayState.DroppedFrames.Value;
                        break;
                    }
                    */
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error retrieving playback diagnostics: {ex.Message}");
        }

        return Task.FromResult($"{{\"dropped_count\":{droppedFrames}}}");
    }

    /// <inheritdoc/>
    public Task<IEnumerable<string>> GetAllVideoMediaIdsAsync()
    {
        _logger.LogInformation("[InternalJellyfinApiService] GetAllVideoMediaIdsAsync called - Starting video retrieval...");
        var result = new List<string>();
        try
        {
            _logger.LogInformation("[InternalJellyfinApiService] Creating InternalItemsQuery for videos...");
            var query = new InternalItemsQuery
            {
                MediaTypes = new[] { MediaType.Video }, // Use Jellyfin.Data.Enums.MediaType
                Recursive = true,
                IsVirtualItem = false
            };

            _logger.LogInformation("[InternalJellyfinApiService] Calling _libraryManager.QueryItems()...");
            var queryResult = _libraryManager.QueryItems(query);
            _logger.LogInformation($"[InternalJellyfinApiService] _libraryManager returned {queryResult?.Items?.Count ?? 0} items");

            if (queryResult?.Items != null)
            {
                result.AddRange(queryResult.Items.Select(i => i.Id.ToString()));
                _logger.LogInformation($"[InternalJellyfinApiService] Successfully retrieved {result.Count} video items from Jellyfin Library.");
            }
            else
            {
                _logger.LogWarning("[InternalJellyfinApiService] _libraryManager.QueryItems returned null or no items!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[InternalJellyfinApiService] Failed to get all video media IDs: {ex.Message}", ex);
        }

        _logger.LogInformation($"[InternalJellyfinApiService] Returning {result.Count} video IDs");
        return Task.FromResult<IEnumerable<string>>(result);
    }

    private Media? MapToDomainMedia(BaseItem item)
    {
        if (item == null)
        {
            return null;
        }

        long? bitrate = null;
        if (item is MediaBrowser.Controller.Entities.Video video)
        {
            bitrate = video.TotalBitrate;
        }

        return new Media
        {
            MediaId = item.Id.ToString(),
            Path = item.Path,
            Name = item.Name,
            Duration = item.RunTimeTicks.HasValue ? (int)TimeSpan.FromTicks(item.RunTimeTicks.Value).TotalSeconds : 0,
            Bitrate = bitrate,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
