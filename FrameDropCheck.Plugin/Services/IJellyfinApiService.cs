using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Abstraction over Jellyfin internal APIs that the service may use to retrieve
/// metadata and session/playback information.
/// Implement this interface using the platform services available in the
/// Jellyfin plugin host when integrating the plugin.
/// </summary>
public interface IJellyfinApiService
{
    /// <summary>
    /// Try to get media metadata from Jellyfin by id. Returns null when not found.
    /// </summary>
    Task<Media?> GetMediaAsync(string mediaId);

    /// <summary>
    /// Retrieve playback/session diagnostics for a particular media id.
    /// The concrete return type is intentionally left as string (JSON) for flexibility.
    /// </summary>
    Task<string> GetPlaybackDiagnosticsAsync(string mediaId);

    /// <summary>
    /// Retrieve all video media IDs from the Jellyfin library.
    /// </summary>
    Task<IEnumerable<string>> GetAllVideoMediaIdsAsync();

    /// <summary>
    /// Try to resolve media directly from Jellyfin internal services, bypassing local DB.
    /// </summary>
    Task<Media?> ResolveMediaFromJellyfinAsync(string mediaId);
}
