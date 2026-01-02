using System;
using System.Threading;
using System.Threading.Tasks;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Singleton manager that coordinates background frame drop checks and broadcasts events to all listeners.
/// </summary>
public interface IFrameDropCheckManager
{
    /// <summary>
    /// Event raised whenever a log message is emitted from ANY active check.
    /// </summary>
    event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <summary>
    /// Event raised whenever a status change occurs in ANY active check.
    /// </summary>
    event EventHandler<CheckStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Starts a frame drop check (log analysis) for the specified media in a background task.
    /// Returns immediately.
    /// </summary>
    /// <param name="mediaId">The media ID to check.</param>
    void StartCheck(string mediaId);

    /// <summary>
    /// Starts a synthetic performance probe for the specified media in a background task.
    /// Returns immediately.
    /// </summary>
    /// <param name="mediaId">The media ID to probe.</param>
    void StartProbe(string mediaId);

    /// <summary>
    /// Cancels an ongoing check or probe for the specified media.
    /// </summary>
    /// <param name="mediaId">The media ID to cancel.</param>
    void CancelCheck(string mediaId);

    /// <summary>
    /// Checks if a run is currently active for the given media.
    /// </summary>
    /// <param name="mediaId">The media ID.</param>
    /// <returns>True if running.</returns>
    bool IsRunning(string mediaId);
}
