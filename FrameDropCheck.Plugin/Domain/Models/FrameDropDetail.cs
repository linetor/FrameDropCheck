namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Represents detailed information about a frame drop.
/// </summary>
public class FrameDropDetail
{
    /// <summary>
    /// Gets or sets the detail identifier.
    /// </summary>
    public int DetailId { get; set; }

    /// <summary>
    /// Gets or sets the FrameDropCheck reference identifier.
    /// </summary>
    public int CheckId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the frame drop occurred.
    /// </summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the drop type (log/playback).
    /// </summary>
    public string DropType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the offset time in seconds.
    /// </summary>
    public int TimeOffset { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the associated FrameDropCheck reference.
    /// </summary>
    public FrameDropCheck? Check { get; set; }
}
