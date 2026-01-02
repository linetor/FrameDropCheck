using System;

namespace FrameDropCheck.Plugin.Api.Models;

/// <summary>
/// View model for media health status.
/// </summary>
public class MediaHealthViewModel
{
    /// <summary>
    /// Gets or sets the media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optimization status.
    /// </summary>
    public string OptimizationStatus { get; set; } = "Pending";

    // Server-side Probe Stats

    /// <summary>
    /// Gets or sets the server-side probe drop rate percentage.
    /// </summary>
    public double? ProbeDropRate { get; set; }

    /// <summary>
    /// Gets or sets the server-side probe speed factor.
    /// </summary>
    public double? ProbeSpeed { get; set; }

    /// <summary>
    /// Gets or sets the last scanned time.
    /// </summary>
    public DateTime? LastScanned { get; set; }

    // Client-side Playback Stats

    /// <summary>
    /// Gets or sets the client-side calculated drop rate percentage.
    /// </summary>
    public double? ClientDropRate { get; set; }

    /// <summary>
    /// Gets or sets the number of client playback sesssions.
    /// </summary>
    public int ClientPlayCount { get; set; }

    // Compression info

    /// <summary>
    /// Gets or sets the compression ratio achieved.
    /// </summary>
    public double? CompressionRatio { get; set; }
}
