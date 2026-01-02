using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using FrameDropCheck.Plugin.Services.Encoding;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Implementation of <see cref="IEncodingService"/> that use FFmpeg for quality-preserving encoding.
/// </summary>
public class FfmpegEncodingService : IEncodingService
{
    private readonly IEncodingJobRepository _jobRepo;
    private readonly IAppLogger _logger;
    private readonly IEncoderStrategyFactory _strategyFactory;
    private readonly Configuration.PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegEncodingService"/> class.
    /// </summary>
    /// <param name="jobRepo">The encoding job repository.</param>
    /// <param name="strategyFactory">The encoder strategy factory.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="config">The plugin configuration.</param>
    public FfmpegEncodingService(
        IEncodingJobRepository jobRepo,
        IEncoderStrategyFactory strategyFactory,
        IAppLogger logger,
        Configuration.PluginConfiguration config)
    {
        _jobRepo = jobRepo ?? throw new ArgumentNullException(nameof(jobRepo));
        _strategyFactory = strategyFactory ?? throw new ArgumentNullException(nameof(strategyFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public async Task<EncodingJob> EncodeMediaAsync(Media media, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (string.IsNullOrEmpty(media.Path) || !File.Exists(media.Path))
        {
            throw new FileNotFoundException("Media file not found.", media.Path);
        }

        var directory = Path.GetDirectoryName(media.Path) ?? string.Empty;
        var fileNameNoExt = Path.GetFileNameWithoutExtension(media.Path);
        var extension = Path.GetExtension(media.Path);
        var newFileName = $"{fileNameNoExt}[encoded]{extension}";
        var newFilePath = Path.Combine(directory, newFileName);

        var job = new EncodingJob
        {
            MediaId = media.MediaId,
            OriginalPath = media.Path,
            NewFilePath = newFilePath,
            Status = "in-progress",
            StartTime = DateTime.UtcNow
        };

        job = await _jobRepo.AddAsync(job).ConfigureAwait(false);

        try
        {
            _logger.LogInformation($"Starting encoding for {media.Name}. Output: {newFilePath}");

            var config = Plugin.Instance?.Configuration ?? _config;
            var strategy = _strategyFactory.GetStrategy(config);
            var presets = new[] { "slower", "medium", "fast", "veryfast" };
            string? finalPresetUsed = null;

            foreach (var preset in presets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogInformation($"Attempting encoding with preset: {preset}");
                var success = await RunEncodingProcessAsync(media, newFilePath, strategy, preset, job, cancellationToken);

                if (success)
                {
                    finalPresetUsed = preset;
                    break;
                }

                _logger.LogWarning($"Encoding with preset '{preset}' failed or was too slow. Retrying with faster preset...");
            }

            if (finalPresetUsed != null)
            {
                _logger.LogInformation($"Encoding completed successfully for {media.Name} using preset: {finalPresetUsed}.");
                job.Status = "completed";
                job.EndTime = DateTime.UtcNow;
            }
            else
            {
                _logger.LogError($"All encoding attempts failed for {media.Name}.");
                job.Status = "failed";
                job.ErrorMessage = "All presets failed or insufficient speed.";
                job.EndTime = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"Encoding task cancelled for {media.Name}.");
            job.Status = "cancelled";
            job.EndTime = DateTime.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error during encoding for {media.Name}: {ex.Message}");
            job.Status = "failed";
            job.ErrorMessage = ex.Message;
            job.EndTime = DateTime.UtcNow;
        }
        finally
        {
            await _jobRepo.UpdateAsync(job).ConfigureAwait(false);
        }

        return job;
    }

    private async Task<bool> RunEncodingProcessAsync(
        Media media,
        string outputPath,
        IEncoderStrategy strategy,
        string preset,
        EncodingJob job,
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? _config;
        var arguments = strategy.BuildArguments(
            media.Path,
            outputPath,
            config.TargetCrf,
            preset,
            config.TargetBitrate);

        var startInfo = new ProcessStartInfo
        {
            FileName = config.FfmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var speedRegex = new System.Text.RegularExpressions.Regex(@"speed=\s*(\d+(?:\.\d+)?)\s*x");
        var speedReadings = new System.Collections.Generic.List<double>();
        bool tooSlow = false;
        var processCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            // _logger.LogDebug($"FFmpeg: {e.Data}");
            _logger.LogWarning($"FFmpeg: {e.Data}");

            var match = speedRegex.Match(e.Data);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double speed))
            {
                // Only monitor speed after some initial warm-up (e.g., first 10 readings or 30 seconds if we had time)
                // Using simple moving average of last 5 readings
                speedReadings.Add(speed);
                if (speedReadings.Count > 10) // Warmup check
                {
                    double avg = 0;
                     // simple average of last 5
                    int count = 0;
                    for (int i = Math.Max(0, speedReadings.Count - 5); i < speedReadings.Count; i++)
                    {
                        avg += speedReadings[i];
                        count++;
                    }
                    avg /= count;

                    if (avg < 0.9) // Threshold: 0.9x (allow slight buffer below 1.0)
                    {
                         // Too slow!
                         tooSlow = true;
                         try
                         {
                             processCts.Cancel();
                         }
                         catch
                         {
                         }
                    }
                }
            }
        };

        try
        {
            process.Start();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(processCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (tooSlow)
            {
                 _logger.LogWarning($"Encoding speed dropped below threshold (0.9x). Aborting current preset.");
                 if (!process.HasExited)
                 {
                     process.Kill();
                 }
                 return false;
            }
            throw; // Real cancellation
        }

        if (tooSlow)
        {
            return false;
        }

        return process.ExitCode == 0;
    }
}
