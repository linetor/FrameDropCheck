using System;
using System.Threading;
using FrameDropCheck.Plugin.Domain.Models;

// NOTE: There is a top-level namespace named 'FrameDropCheck' in this project which
// conflicts with the model type name 'FrameDropCheck'. Use the fully-qualified
// model type in public signatures to avoid the compiler treating the identifier
// as a namespace.

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Defines operations to run frame drop checks for media items.
/// </summary>
public interface IFrameDropCheckService
{
    /// <summary>
    /// Event raised whenever a log message is emitted during a check.
    /// Subscribers can use this to stream real-time progress to UI clients.
    /// </summary>
    event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <summary>
    /// Event raised when a check starts.
    /// </summary>
    event EventHandler<CheckStatusEventArgs>? CheckStarted;

    /// <summary>
    /// Event raised when a check completes (success or failure).
    /// </summary>
    event EventHandler<CheckStatusEventArgs>? CheckCompleted;

    /// <summary>
    /// Runs a frame drop check for the given media id and persists results to the database.
    /// Returns the persisted <see cref="FrameDropCheck.Plugin.Domain.Models.FrameDropCheck"/> record.
    /// </summary>
    /// <param name="mediaId">The Jellyfin media identifier.</param>
    /// <param name="cancellationToken">Token to cancel the check operation.</param>
    Task<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck> CheckMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments for log messages emitted during check execution.
/// </summary>
public class LogMessageEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log level (Info, Warning, Error).
    /// </summary>
    public string Level { get; set; } = "Info";

    /// <summary>
    /// Gets or sets the log message text.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the log entry.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for check status changes.
/// </summary>
public class CheckStatusEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the check identifier.
    /// </summary>
    public int CheckId { get; set; }

    /// <summary>
    /// Gets or sets the media identifier.
    /// </summary>
    public string MediaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the check status (running/completed/failed).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the status change.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of dropped frames detected so far.
    /// </summary>
    public int DroppedFrames { get; set; }

    /// <summary>
    /// Gets or sets the total number of frames processed so far.
    /// </summary>
    public int TotalFrames { get; set; }

    /// <summary>
    /// Gets or sets the current transcoding speed factor (e.g., 1.5 for 1.5x).
    /// </summary>
    public double? SpeedFactor { get; set; }
}
