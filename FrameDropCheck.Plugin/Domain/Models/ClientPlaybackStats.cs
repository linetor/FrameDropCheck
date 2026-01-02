using System;

namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents playback statistics reported by a client.
/// </summary>
public class ClientPlaybackStats
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the client device/app.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the report.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of dropped frames reported.
    /// </summary>
    public int FramesDropped { get; set; }

    /// <summary>
    /// Gets or sets the playback duration in seconds.
    /// </summary>
    public double PlaybackDuration { get; set; }

    /// <summary>
    /// Gets or sets the validation result message.
    /// </summary>
    public string ValidationResult { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin session identifier.
    /// Used to deduplicate or update stats for the same session.
    /// </summary>
    public string? JellyfinSessionId { get; set; }
}
