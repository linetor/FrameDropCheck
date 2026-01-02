namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents the result of a frame drop check.
/// </summary>
public class FrameDropCheck
{
    /// <summary>
    /// Gets or sets the check identifier.
    /// </summary>
    public int CheckId { get; set; }

    /// <summary>
    /// Gets or sets the referenced media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the check start time.
    /// </summary>
    public DateTime? CheckStartTime { get; set; }

    /// <summary>
    /// Gets or sets the check end time.
    /// </summary>
    public DateTime? CheckEndTime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a frame drop occurred.
    /// </summary>
    public bool HasFrameDrop { get; set; }

    /// <summary>
    /// Gets or sets the number of frame drops.
    /// </summary>
    public int FrameDropCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of frames processed.
    /// </summary>
    public int TotalFrameCount { get; set; }

    /// <summary>
    /// Gets or sets the log analysis result.
    /// </summary>
    public string? LogAnalysisResult { get; set; }

    /// <summary>
    /// Gets or sets the playback analysis result.
    /// </summary>
    public string? PlaybackAnalysisResult { get; set; }

    /// <summary>
    /// Gets or sets the check status (pending/in-progress/completed/failed).
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets the related media reference.
    /// </summary>
    public Media? Media { get; set; }
}
