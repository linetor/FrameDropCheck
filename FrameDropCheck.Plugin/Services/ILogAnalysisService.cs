using System;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service responsible for searching and analyzing FFmpeg transcode logs for frame drops.
/// </summary>
public interface ILogAnalysisService
{
    /// <summary>
    /// Event raised whenever a log message is emitted during analysis.
    /// </summary>
    event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <summary>
    /// Analyzes available logs for the specified media to detect frame drops.
    /// </summary>
    /// <param name="media">The media entity to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON string containing the analysis result (frames_dropped, total_frames, events).</returns>
    Task<string> AnalyzeLogsAsync(Media media, CancellationToken cancellationToken = default);
}
