using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using MediaBrowser.Model.Tasks;

namespace FrameDropCheck.Plugin.ScheduledTasks;

/// <summary>
/// A scheduled task to scan the media library and perform frame drop probes during off-peak hours.
/// </summary>
public class MediaOptimizationTask : IScheduledTask
{
    private readonly IMediaRepository _mediaRepo;
    private readonly Services.IFfmpegProbingService _probingService;
    private readonly Services.IEncodingService _encodingService;
    private readonly Services.IFileReplacementService _replacementService;
    private readonly Services.MediaSyncService _syncService;
    private readonly Services.ISchedulingService _schedulingService;
    private readonly Services.IOptimizationAnalyzer _analyzer;
    private readonly IAppLogger _logger;
    private readonly Configuration.PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaOptimizationTask"/> class.
    /// </summary>
    /// <param name="mediaRepo">The media repository.</param>
    /// <param name="probingService">The probing service.</param>
    /// <param name="encodingService">The encoding service.</param>
    /// <param name="replacementService">The file replacement service.</param>
    /// <param name="syncService">The media synchronization service.</param>
    /// <param name="schedulingService">The scheduling service.</param>
    /// <param name="analyzer">The optimization analyzer.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="config">The plugin configuration.</param>
    public MediaOptimizationTask(
        IMediaRepository mediaRepo,
        Services.IFfmpegProbingService probingService,
        Services.IEncodingService encodingService,
        Services.IFileReplacementService replacementService,
        Services.MediaSyncService syncService,
        Services.ISchedulingService schedulingService,
        Services.IOptimizationAnalyzer analyzer,
        IAppLogger logger,
        Configuration.PluginConfiguration config)
    {
        _mediaRepo = mediaRepo;
        _probingService = probingService;
        _encodingService = encodingService;
        _replacementService = replacementService;
        _syncService = syncService;
        _schedulingService = schedulingService;
        _analyzer = analyzer;
        _logger = logger;
        _config = config;
    }

    /// <inheritdoc />
    public string Name => "Media Optimization Check (FrameDrop)";

    /// <inheritdoc />
    public string Key => "FrameDropCheckOptimizationTask";

    /// <inheritdoc />
    public string Description => "Scans for media with potential frame drops and performs automated re-encoding.";

    /// <inheritdoc />
    public string Category => "Maintenance";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MediaOptimizationTask] Scheduled media optimization check started.");

        // 1. Sync Library (Find new files)
        await _syncService.SyncLibraryAsync(cancellationToken).ConfigureAwait(false);

        var allMedia = await _mediaRepo.GetAllAsync().ConfigureAwait(false);
        var mediaToProcess = new List<Media>();

        foreach (var m in allMedia)
        {
            // Check if media needs processing using OptimizationAnalyzer
            // This considers both server-side (probe) AND client-side (playback stats) drop rates
            var recommendation = await _analyzer.AnalyzeMediaAsync(m).ConfigureAwait(false);

            bool needsScan = m.LastScanned == null || (DateTime.UtcNow - m.LastScanned.Value).TotalDays > 7;
            bool needsEncode = recommendation.Action == Domain.Models.OptimizationAction.ReEncode;

            if ((needsScan || needsEncode) && m.OptimizationStatus != "Failed")
            {
                mediaToProcess.Add(m);
            }
        }

        _logger.LogInformation($"[MediaOptimizationTask] Found {allMedia.Count()} media in DB. {mediaToProcess.Count} items need processing.");

        if (mediaToProcess.Count == 0)
        {
            _logger.LogInformation("[MediaOptimizationTask] No media needing processing found.");
            return;
        }

        int current = 0;
        foreach (var m in mediaToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_schedulingService.IsWithinMaintenanceWindow())
            {
                _logger.LogInformation($"[MediaOptimizationTask] Maintenance window ended (Current Time: {DateTime.Now:HH:mm}). Stopping task mid-run.");
                break;
            }

            _logger.LogInformation($"[MediaOptimizationTask] Processing: {m.Name} (ID: {m.MediaId})");

            // 1. Probe if needed
            bool needsScan = m.LastScanned == null || (DateTime.UtcNow - m.LastScanned.Value).TotalDays > 7;
            if (needsScan)
            {
                var reason = m.LastScanned == null ? "Never scanned" : $"Last scanned {m.LastScanned:yyyy-MM-dd} (> 7 days ago)";
                _logger.LogInformation($"[MediaOptimizationTask] Triggering probe for '{m.Name}'. Reason: {reason}");

                await _probingService.ProbeMediaAsync(m.MediaId, cancellationToken).ConfigureAwait(false);

                // Refresh entity after probe
                var updatedMedia = await _mediaRepo.GetByIdAsync(m.MediaId).ConfigureAwait(false);
                if (updatedMedia != null)
                {
                    m.AverageDropRate = updatedMedia.AverageDropRate;
                    m.OptimizationStatus = updatedMedia.OptimizationStatus;
                    _logger.LogInformation($"[MediaOptimizationTask] Probe results for '{m.Name}': DropRate={m.AverageDropRate:F2}%, Status={m.OptimizationStatus}");
                }
            }

            // 2. Check if encoding is needed using OptimizationAnalyzer (considers both server and client stats)
            var recommendation = await _analyzer.AnalyzeMediaAsync(m).ConfigureAwait(false);

            if (recommendation.Action == Domain.Models.OptimizationAction.ReEncode && m.OptimizationStatus != "Optimized")
            {
                _logger.LogInformation($"[MediaOptimizationTask] Encoding recommended for '{m.Name}'. Reason: {recommendation.Reason}. Starting re-encode...");

                try
                {
                    var job = await _encodingService.EncodeMediaAsync(m, cancellationToken).ConfigureAwait(false);
                    if (job.Status == "completed" && !string.IsNullOrEmpty(job.NewFilePath))
                    {
                        var originalSize = m.Size ?? 0;
                        var newFileInfo = new System.IO.FileInfo(job.NewFilePath);
                        var newSize = newFileInfo.Length;

                        // 1. Set OriginalBitrate
                        if (m.OriginalBitrate == null)
                        {
                            if (m.Bitrate != null)
                            {
                                m.OriginalBitrate = m.Bitrate;
                            }
                            else if (m.Duration > 0 && originalSize > 0)
                            {
                                m.OriginalBitrate = (long)(originalSize * 8 / m.Duration);
                            }
                        }

                        // 2. Calculate Compression Ratio
                        if (newSize > 0 && originalSize > 0)
                        {
                            m.CompressionRatio = (double)originalSize / newSize;
                        }

                        // 3. Update Current Size and Bitrate
                        m.Size = newSize;
                        if (m.Duration > 0)
                        {
                            m.Bitrate = (long)(newSize * 8 / m.Duration);
                        }

                        _logger.LogInformation($"[MediaOptimizationTask] Re-encode completed for '{m.Name}'. Ratio: {m.CompressionRatio:F2}x. Replacing old file...");

                        await _replacementService.ReplaceAndRefreshAsync(m, job).ConfigureAwait(false);

                        // Reset stats after optimization
                        m.OptimizationStatus = "Optimized";
                        m.AverageDropRate = 0;
                        m.AverageFrameDrops = 0;

                        await _mediaRepo.UpdateAsync(m).ConfigureAwait(false);
                        _logger.LogInformation($"[MediaOptimizationTask] Successfully optimized and replaced file for '{m.Name}'.");
                    }
                    else
                    {
                        _logger.LogWarning($"[MediaOptimizationTask] Re-encode job finished with status: {job.Status} for '{m.Name}'.");
                        m.OptimizationStatus = "Failed";
                        await _mediaRepo.UpdateAsync(m).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[MediaOptimizationTask] Failed to re-encode '{m.Name}': {ex.Message}", ex);
                    m.OptimizationStatus = "Failed";
                    await _mediaRepo.UpdateAsync(m).ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogInformation($"[MediaOptimizationTask] Skipping re-encode for '{m.Name}': {recommendation.Reason}");
            }

            current++;
            progress.Report((double)current / mediaToProcess.Count * 100);
        }

        _logger.LogInformation($"[MediaOptimizationTask] Scheduled media optimization check completed. Processed {current}/{mediaToProcess.Count} items.");
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Default to running daily at 2:00 AM as per PluginConfiguration
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
            }
        };
    }
}
