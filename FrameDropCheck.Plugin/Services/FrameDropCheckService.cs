using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data.Transactions;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using FrameDropCheck.Plugin.Infrastructure.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Default implementation of <see cref="IFrameDropCheckService"/>.
/// This implementation uses repositories to persist results and may call an
/// optional <see cref="IJellyfinApiService"/> when available to obtain
/// up-to-date metadata and playback diagnostics.
/// </summary>
public class FrameDropCheckService : IFrameDropCheckService
{
    private readonly IMediaRepository _mediaRepo;
    private readonly IFrameDropCheckRepository _checkRepo;
    private readonly IFrameDropDetailRepository _detailRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAppLogger _logger;
    private readonly IJellyfinApiService? _jellyfinApiService;
    private readonly ILogAnalysisService _logAnalysisService;
    private readonly Configuration.PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDropCheckService"/> class.
    /// </summary>
    /// <param name="mediaRepo">The media repository.</param>
    /// <param name="checkRepo">The frame drop check repository.</param>
    /// <param name="detailRepo">The frame drop detail repository.</param>
    /// <param name="uow">The unit of work.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="logAnalysisService">The log analysis service.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="jellyfinApiService">Optional jellyfin API service.</param>
    public FrameDropCheckService(
        IMediaRepository mediaRepo,
        IFrameDropCheckRepository checkRepo,
        IFrameDropDetailRepository detailRepo,
        IUnitOfWork uow,
        IAppLogger logger,
        ILogAnalysisService logAnalysisService,
        Configuration.PluginConfiguration config,
        IJellyfinApiService? jellyfinApiService = null)
    {
        _mediaRepo = mediaRepo ?? throw new ArgumentNullException(nameof(mediaRepo));
        _checkRepo = checkRepo ?? throw new ArgumentNullException(nameof(checkRepo));
        _detailRepo = detailRepo ?? throw new ArgumentNullException(nameof(detailRepo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logAnalysisService = logAnalysisService ?? throw new ArgumentNullException(nameof(logAnalysisService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _jellyfinApiService = jellyfinApiService;

        // Propagate events from log analysis service
        _logAnalysisService.LogMessageEmitted += (s, e) => LogMessageEmitted?.Invoke(this, e);
    }

    /// <summary>
    /// Event raised whenever a log message is emitted during a check.
    /// </summary>
    public event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <summary>
    /// Event raised when a check starts.
    /// </summary>
    public event EventHandler<CheckStatusEventArgs>? CheckStarted;

    /// <summary>
    /// Event raised when a check completes (success or failure).
    /// </summary>
    public event EventHandler<CheckStatusEventArgs>? CheckCompleted;

    /// <summary>
    /// Runs a frame drop check for the given media id and persists results to the database.
    /// Returns the persisted <see cref="FrameDropCheck.Plugin.Domain.Models.FrameDropCheck"/> record.
    /// </summary>
    /// <param name="mediaId">The Jellyfin media identifier.</param>
    /// <param name="cancellationToken">Token to cancel the check operation.</param>
    /// <returns>The completed check record.</returns>
    public async Task<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck> CheckMediaAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            throw new ArgumentNullException(nameof(mediaId));
        }

        // Normalize mediaId to standard hyphenated GUID format if possible
        if (Guid.TryParse(mediaId, out var guid))
        {
            mediaId = guid.ToString();
        }

        FrameDropCheck.Plugin.Domain.Models.FrameDropCheck? check = null;

        try
        {
            var media = await _mediaRepo.GetByIdAsync(mediaId).ConfigureAwait(false);

            if (media == null)
            {
                if (_jellyfinApiService != null)
                {
                    media = await _jellyfinApiService.GetMediaAsync(mediaId).ConfigureAwait(false);
                    if (media != null)
                    {
                        // Double check if ID changed (e.g. from hyphen-less to hyphenated)
                        var existing = await _mediaRepo.GetByIdAsync(media.MediaId).ConfigureAwait(false);
                        if (existing == null)
                        {
                            _logger.LogInformation($"Discovered media {media.Name} with ID {media.MediaId}. Persisting...");
                            await _mediaRepo.AddAsync(media).ConfigureAwait(false);
                        }
                        else
                        {
                            media = existing;
                        }
                    }
                }
            }

            if (media == null)
            {
                throw new InvalidOperationException($"Media not found: {mediaId}");
            }

            // Always use the MediaId from the found object (canonical version)
            mediaId = media.MediaId;

            check = new FrameDropCheck.Plugin.Domain.Models.FrameDropCheck
            {
                MediaId = mediaId,
                CheckStartTime = DateTime.UtcNow,
                Status = "running"
            };

            await _uow.BeginTransactionAsync().ConfigureAwait(false);
            check = await _checkRepo.AddAsync(check).ConfigureAwait(false);

            EmitLog(mediaId, "Info", $"Check started for media: {mediaId}");
            OnCheckStarted(check);

            cancellationToken.ThrowIfCancellationRequested();

            var logResult = await _logAnalysisService.AnalyzeLogsAsync(media, cancellationToken).ConfigureAwait(false);
            var playbackResult = _jellyfinApiService != null
                ? await _jellyfinApiService.GetPlaybackDiagnosticsAsync(mediaId).ConfigureAwait(false)
                : string.Empty;

            cancellationToken.ThrowIfCancellationRequested();

            var (hasDrop, totalDrops, totalFrames, details) = AggregateResults(logResult, playbackResult);

            check.HasFrameDrop = hasDrop;
            check.FrameDropCount = totalDrops;
            check.TotalFrameCount = totalFrames;
            check.LogAnalysisResult = logResult;
            check.PlaybackAnalysisResult = playbackResult;
            check.CheckEndTime = DateTime.UtcNow;
            check.Status = "completed";

            await _checkRepo.UpdateAsync(check).ConfigureAwait(false);

            foreach (var d in details)
            {
                d.CheckId = check.CheckId;
                await _detailRepo.AddAsync(d).ConfigureAwait(false);
            }

            // Update Media health status based on analysis results
            media.LastScanned = DateTime.UtcNow;
            double dropRate = totalFrames > 0 ? (double)totalDrops / totalFrames * 100.0 : 0;
            media.AverageDropRate = media.AverageDropRate == null ? dropRate : (media.AverageDropRate + dropRate) / 2.0;

            if (media.AverageDropRate < _config.DropThreshold)
            {
                media.OptimizationStatus = "Healthy";
            }
            else
            {
                media.OptimizationStatus = "Action Required";
            }

            await _mediaRepo.UpdateAsync(media).ConfigureAwait(false);

            await _uow.CommitAsync().ConfigureAwait(false);

            EmitLog(mediaId, "Info", $"Check completed successfully. Frames dropped: {totalDrops}");
            OnCheckCompleted(check);

            return check;
        }
        catch (OperationCanceledException)
        {
            EmitLog(mediaId, "Warning", "Check was cancelled by user");
            if (check != null)
            {
                check.Status = "cancelled";
                check.CheckEndTime = DateTime.UtcNow;
                try
                {
                    await _checkRepo.UpdateAsync(check).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            try
            {
                await _uow.RollbackAsync().ConfigureAwait(false);
            }
            catch
            {
                // swallow rollback errors
            }

            _logger.LogError($"Error while checking media {mediaId}: {ex.Message}", ex);

            if (check != null)
            {
                try
                {
                    check.Status = "failed";
                    check.CheckEndTime = DateTime.UtcNow;
                    await _checkRepo.UpdateAsync(check).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort
                }
            }

            throw;
        }
    }

    private (bool HasFrameDrop, int FrameDropCount, int TotalFrameCount, List<FrameDropDetail> Details) AggregateResults(string logResultJson, string playbackResultJson)
    {
        var details = new List<FrameDropDetail>();
        int totalDrops = 0;
        int totalFrames = 0;
        bool hasDrop = false;

        try
        {
            if (!string.IsNullOrWhiteSpace(logResultJson))
            {
                using var doc = JsonDocument.Parse(logResultJson);
                if (doc.RootElement.TryGetProperty("frames_dropped", out var f))
                {
                    var n = f.GetInt32();
                    totalDrops += n;
                    if (n > 0)
                    {
                        hasDrop = true;
                        details.Add(new FrameDropDetail
                        {
                            Timestamp = DateTime.UtcNow,
                            DropType = "log",
                            TimeOffset = 0,
                            Description = $"Detected {n} dropped frames from logs"
                        });
                    }
                }

                if (doc.RootElement.TryGetProperty("total_frames", out var tf))
                {
                    totalFrames = Math.Max(totalFrames, tf.GetInt32());
                }
            }

            if (!string.IsNullOrWhiteSpace(playbackResultJson))
            {
                using var doc = JsonDocument.Parse(playbackResultJson);
                if (doc.RootElement.TryGetProperty("dropped_count", out var p))
                {
                    var n = p.GetInt32();
                    totalDrops += n;
                    if (n > 0)
                    {
                        hasDrop = true;
                        details.Add(new FrameDropDetail
                        {
                            Timestamp = DateTime.UtcNow,
                            DropType = "playback",
                            TimeOffset = 0,
                            Description = $"Detected {n} dropped frames from playback diagnostics"
                        });
                    }
                }
            }
        }
        catch
        {
            // parsing errors should not fail the whole flow
            _logger.LogWarning("Failed to parse analysis result JSON, treating as no drops.");
        }

        return (hasDrop, totalDrops, totalFrames, details);
    }

    private void EmitLog(string mediaId, string level, string message)
    {
        var args = new LogMessageEventArgs
        {
            MediaId = mediaId,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
        LogMessageEmitted?.Invoke(this, args);
    }

    private void OnCheckStarted(FrameDropCheck.Plugin.Domain.Models.FrameDropCheck check)
    {
        var args = new CheckStatusEventArgs
        {
            CheckId = check.CheckId,
            MediaId = check.MediaId,
            Status = check.Status,
            Timestamp = DateTime.UtcNow,
            DroppedFrames = check.FrameDropCount,
            TotalFrames = check.TotalFrameCount
        };
        CheckStarted?.Invoke(this, args);
    }

    private void OnCheckCompleted(FrameDropCheck.Plugin.Domain.Models.FrameDropCheck check)
    {
        var args = new CheckStatusEventArgs
        {
            CheckId = check.CheckId,
            MediaId = check.MediaId,
            Status = check.Status,
            Timestamp = DateTime.UtcNow,
            DroppedFrames = check.FrameDropCount,
            TotalFrames = check.TotalFrameCount
        };
        CheckCompleted?.Invoke(this, args);
    }
}
