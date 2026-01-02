using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Implementation of <see cref="IFrameDropCheckManager"/>.
/// Tracks active checks and uses a scoped service provider for each background run.
/// </summary>
public class FrameDropCheckManager : IFrameDropCheckManager, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeChecks = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDropCheckManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public FrameDropCheckManager(IServiceProvider serviceProvider, IAppLogger logger)
    {
        this._serviceProvider = serviceProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <inheritdoc/>
    public event EventHandler<CheckStatusEventArgs>? StatusChanged;

    /// <inheritdoc/>
    public void StartCheck(string mediaId)
    {
        ExecuteBackgroundTask<IFrameDropCheckService>(
            mediaId,
            "Check",
            async (service, id, token) =>
            {
                service.CheckStarted += OnCheckStatusChanged;
                service.CheckCompleted += OnCheckStatusChanged;
                try
                {
                    await service.CheckMediaAsync(id, token).ConfigureAwait(false);
                }
                finally
                {
                    service.CheckStarted -= OnCheckStatusChanged;
                    service.CheckCompleted -= OnCheckStatusChanged;
                }
            });
    }

    /// <inheritdoc/>
    public void StartProbe(string mediaId)
    {
        ExecuteBackgroundTask<IFfmpegProbingService>(
            mediaId,
            "Probe",
            async (service, id, token) =>
            {
                service.ProbeStatusChanged += OnCheckStatusChanged;
                try
                {
                    await service.ProbeMediaAsync(id, token).ConfigureAwait(false);
                }
                finally
                {
                    service.ProbeStatusChanged -= OnCheckStatusChanged;
                }
            });
    }

    private void ExecuteBackgroundTask<TService>(
        string mediaId,
        string taskName,
        Func<TService, string, CancellationToken, Task> action)
        where TService : class
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return;
        }

        if (Guid.TryParse(mediaId, out var guid))
        {
            mediaId = guid.ToString();
        }

        if (_activeChecks.ContainsKey(mediaId))
        {
            _logger.LogWarning($"{taskName} already running for media: {mediaId}");
            return;
        }

        var cts = new CancellationTokenSource();
        if (!_activeChecks.TryAdd(mediaId, cts))
        {
            cts.Dispose();
            return;
        }

        var taskMediaId = mediaId;
        _logger.LogInformation($"Starting background {taskName} for media: {taskMediaId}");

        _ = Task.Run(
            async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                try
                {
                    var service = scope.ServiceProvider.GetRequiredService<TService>();

                    // Shared Log handler
                    var logHandler = new EventHandler<LogMessageEventArgs>((s, e) =>
                    {
                        if (e.MediaId == taskMediaId)
                        {
                            LogMessageEmitted?.Invoke(this, e);
                        }
                    });

                    // Reflection to attach LogMessageEmitted if it exists
                    var logEvent = typeof(TService).GetEvent("LogMessageEmitted");
                    if (logEvent != null)
                    {
                        logEvent.AddEventHandler(service, logHandler);
                    }

                    _logger.LogInformation($"[FrameDropCheckManager] Background {taskName} task started for media: {taskMediaId}");

                    try
                    {
                        await action(service, taskMediaId, cts.Token).ConfigureAwait(false);
                        _logger.LogInformation($"[FrameDropCheckManager] Background {taskName} task successfully completed for media: {taskMediaId}");
                    }
                    finally
                    {
                        if (logEvent != null)
                        {
                            logEvent.RemoveEventHandler(service, logHandler);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation($"[FrameDropCheckManager] Background {taskName} task was cancelled for media: {taskMediaId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[FrameDropCheckManager] Background {taskName} task failed for media {taskMediaId}: {ex.Message}", ex);
                    StatusChanged?.Invoke(
                        this,
                        new CheckStatusEventArgs
                        {
                            MediaId = taskMediaId,
                            Status = "failed",
                            Timestamp = DateTime.UtcNow
                        });
                }
                finally
                {
                    if (_activeChecks.TryRemove(taskMediaId, out var removedCts))
                    {
                        removedCts.Dispose();
                    }
                    _logger.LogInformation($"[FrameDropCheckManager] Background {taskName} task state cleaned up for media: {taskMediaId}");
                }
            },
            cts.Token);
    }

    private void OnCheckStatusChanged(object? sender, CheckStatusEventArgs e)
    {
        // Event handlers in ExecuteBackgroundTask are generic/reflection based or closures,
        // but StatusChanged needs simple filtering.
        // For simplicity in the generic method, we delegate status handling to the specific callbacks passed in.
        // However, to keep it clean, we can just invoke the manager's event directly from the specific callbacks.

        // This method matches the signature for direct subscription if needed,
        // but since we filter by ID in the caller, we might just duplicate the invoke there.
        // Actually, let's keep it simple: the lambda passes events to this helper.

        // Wait, the lambda in StartCheck/StartProbe has access to 'this'.
        StatusChanged?.Invoke(this, e);
    }

    /// <inheritdoc/>
    public void CancelCheck(string mediaId)
    {
        if (Guid.TryParse(mediaId, out var guid))
        {
            mediaId = guid.ToString();
        }

        if (_activeChecks.TryGetValue(mediaId, out var cts))
        {
            _logger.LogInformation($"Cancelling check for media: {mediaId}");
            cts.Cancel();
        }
    }

    /// <inheritdoc/>
    public bool IsRunning(string mediaId)
    {
        if (Guid.TryParse(mediaId, out var guid))
        {
            mediaId = guid.ToString();
        }

        return _activeChecks.ContainsKey(mediaId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs resource cleanup.
    /// </summary>
    /// <param name="disposing">True to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var cts in _activeChecks.Values)
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Ignore cancel errors
                }

                cts.Dispose();
            }

            _activeChecks.Clear();
        }

        _disposed = true;
    }
}
