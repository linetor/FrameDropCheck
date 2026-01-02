using System;

namespace FrameDropCheck.Plugin.Api.Models;

/// <summary>
/// Request model for reporting client-side playback statistics.
/// </summary>
public class ReportStatsRequest
{
    /// <summary>
    /// Gets or sets the Jellyfin MediaId.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of dropped frames.
    /// </summary>
    public int DroppedFrames { get; set; }

    /// <summary>
    /// Gets or sets the playback duration in seconds.
    /// </summary>
    public double PlaybackDuration { get; set; }

    /// <summary>
    /// Gets or sets the client name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin session identifier.
    /// </summary>
    public string? SessionId { get; set; }
}
