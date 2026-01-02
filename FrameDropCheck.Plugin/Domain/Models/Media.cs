namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents media file information.
/// </summary>
public class Media
{
    /// <summary>
    /// Gets or sets the Jellyfin media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media file path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media duration in seconds.
    /// </summary>
    public int? Duration { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Gets or sets the last modified time.
    /// </summary>
    public DateTime? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the last scanned time.
    /// </summary>
    public DateTime? LastScanned { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this media has been processed.
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optimization status (Pending, Optimized, Failed, Skipped).
    /// </summary>
    public string OptimizationStatus { get; set; } = "Pending";

    /// <summary>
    /// Gets or sets the average frame drops detected across checks.
    /// </summary>
    public double? AverageFrameDrops { get; set; }

    /// <summary>
    /// Gets or sets the average drop rate percentage (0-100).
    /// </summary>
    public double? AverageDropRate { get; set; }

    /// <summary>
    /// Gets or sets the video codec.
    /// </summary>
    public string? Codec { get; set; }

    /// <summary>
    /// Gets or sets the video resolution (e.g., 1080p).
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// Gets or sets the bitrate in bps.
    /// </summary>
    public long? Bitrate { get; set; }

    /// <summary>
    /// Gets or sets the original bitrate before optimization.
    /// </summary>
    public long? OriginalBitrate { get; set; }

    /// <summary>
    /// Gets or sets the compression ratio achieved (OriginalSize / NewSize).
    /// </summary>
    public double? CompressionRatio { get; set; }

    /// <summary>
    /// Gets or sets the speed factor of the last scan/probe (e.g. 1.5x).
    /// </summary>
    public double? LastScanSpeed { get; set; }
}
