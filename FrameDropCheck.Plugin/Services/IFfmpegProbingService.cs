using System.Threading;
using System.Threading.Tasks;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Defines operations for probing media files using FFmpeg to detect potential frame drops.
/// </summary>
public interface IFfmpegProbingService
{
    /// <summary>
    /// Event raised whenever a log message is emitted during probing.
    /// </summary>
    event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <summary>
    /// Event raised whenever the probing status changes.
    /// </summary>
    event EventHandler<CheckStatusEventArgs>? ProbeStatusChanged;

    /// <summary>
    /// Probes a media file for a short duration to detect frame drops under synthetic load.
    /// </summary>
    /// <param name="mediaId">The Jellyfin media identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProbeMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}
