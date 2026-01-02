using System;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Monitors playback events and records statistics.
/// </summary>
public class PlaybackMonitor : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppLogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackMonitor"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="scopeFactory">The service scope factory.</param>
    /// <param name="logger">The application logger.</param>
    public PlaybackMonitor(
        ISessionManager sessionManager,
        IServiceScopeFactory scopeFactory,
        IAppLogger logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    /// <param name="disposing">True to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _sessionManager.PlaybackStopped -= OnPlaybackStopped;
                _sessionManager.PlaybackProgress -= OnPlaybackProgress;
            }

            _disposed = true;
        }
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        if (e.Session == null || e.Session.PlayState == null)
        {
            return;
        }

        // Try to check if paused
        bool isPaused = false;
        try
        {
             isPaused = e.Session.PlayState.IsPaused;
        }
        catch
        {
            /* ignore */
        }

        if (isPaused)
        {
            // If paused, we want to record the state so far
            ProcessPlaybackEvent(e.Item, e.Session, e.PlaybackPositionTicks, "Paused");
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        ProcessPlaybackEvent(e.Item, e.Session, e.PlaybackPositionTicks, "Stopped");
    }

    private void ProcessPlaybackEvent(MediaBrowser.Controller.Entities.BaseItem? item, SessionInfo? session, long? playbackTicks, string eventType)
    {
        if (item == null || session == null)
        {
            return;
        }

        try
        {
            var mediaName = item.Name ?? "Unknown";
            var clientName = session.Client ?? "Unknown Client";
            var mediaType = item.MediaType.ToString();

            // Only record for video items
            if (!string.Equals(mediaType, "Video", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var playbackPositionTicks = playbackTicks ?? 0;
            var playbackDuration = playbackPositionTicks / 10000000; // Ticks to seconds

            // Allow shorter durations if it's a "Paused" event updates?
            if (playbackDuration < 30)
            {
                // _logger.LogInformation($"[PlaybackMonitor] Skipping short playback ({playbackDuration}s) for '{mediaName}'");
                return;
            }

            long droppedFrames = 0;
            if (session.PlayState != null)
            {
                var playState = session.PlayState;
                _logger.LogDebug($"[PlaybackMonitor] Inspecting PlayState for '{mediaName}'. PlayMethod: {playState.PlayMethod}");

                var prop = playState.GetType().GetProperty("DroppedFrames");
                if (prop == null)
                {
                    // If not found directly, maybe it's in a sub-object or named differently?
                    // Let's also check if it's in a 'TranscodingInfo' (though user said Direct Play)
                    _logger.LogDebug($"[PlaybackMonitor] 'DroppedFrames' property not found in {playState.GetType().Name}.");
                }
                else
                {
                    var val = prop.GetValue(playState);
                    if (val != null && long.TryParse(val.ToString(), out var df))
                    {
                        droppedFrames = df;
                        _logger.LogInformation($"[PlaybackMonitor] Captured {droppedFrames} dropped frames for '{mediaName}' via reflection.");
                    }
                }

                // Fallback for Direct Play: Some clients send it in a custom header or different field
                // BUT, if the user sees 159 in UI, it MUST match something we receive.
            }

            var stats = new ClientPlaybackStats
            {
                Id = Guid.NewGuid().ToString(),
                MediaId = item.Id.ToString(),
                UserId = session.UserId.ToString(),
                ClientName = clientName,
                Timestamp = DateTime.UtcNow,
                FramesDropped = (int)droppedFrames,
                PlaybackDuration = (int)playbackDuration,
                ValidationResult = eventType,
                JellyfinSessionId = session.Id // Capture Session ID
            };

            Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var mediaRepo = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
                var statsRepo = scope.ServiceProvider.GetRequiredService<IClientPlaybackStatsRepository>();

                try
                {
                    // Ensure Media exists in DB
                    var existingMedia = await mediaRepo.GetByIdAsync(item.Id.ToString()).ConfigureAwait(false);
                    if (existingMedia == null)
                    {
                        var newMedia = new Media
                        {
                            MediaId = item.Id.ToString(),
                            Name = mediaName,
                            Path = item.Path ?? string.Empty,
                            CreatedAt = DateTime.UtcNow,
                            OptimizationStatus = "Pending"
                        };
                        try
                        {
                            var runTimeTicksProp = item.GetType().GetProperty("RunTimeTicks");
                            if (runTimeTicksProp != null)
                            {
                                var ticksVal = runTimeTicksProp.GetValue(item);
                                if (ticksVal != null && long.TryParse(ticksVal.ToString(), out var ticks))
                                {
                                    newMedia.Duration = (int)TimeSpan.FromTicks(ticks).TotalSeconds;
                                }
                            }

                            await mediaRepo.AddAsync(newMedia).ConfigureAwait(false);
                            _logger.LogInformation($"[PlaybackMonitor] Auto-discovered media: {mediaName}");
                        }
                        catch
                        {
                            // Ignore race conditions
                        }
                    }

                    // Use Upsert instead of Add
                    await statsRepo.UpsertAsync(stats).ConfigureAwait(false);
                    _logger.LogInformation($"[PlaybackMonitor] Recorded stats ({eventType}) for '{mediaName}'. Duration: {stats.PlaybackDuration}s, Dropped: {stats.FramesDropped}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[PlaybackMonitor] Failed to record stats: {ex.Message}", ex);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[PlaybackMonitor] Error processing playback event: {ex.Message}", ex);
        }
    }
}
