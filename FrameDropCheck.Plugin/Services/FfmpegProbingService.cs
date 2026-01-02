using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using FrameDropCheck.Plugin.Services.Encoding;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Implementation of <see cref="IFfmpegProbingService"/> that runs FFmpeg processes to test media compatibility.
/// </summary>
public class FfmpegProbingService : IFfmpegProbingService
{
    private readonly IMediaRepository _mediaRepo;
    private readonly IFrameDropCheckRepository _checkRepo;
    private readonly IAppLogger _logger;
    private readonly Configuration.PluginConfiguration _config;
    private readonly IEncoderStrategyFactory _strategyFactory;

    private static readonly Regex FrameRegex = new Regex(@"frame=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex DropRegex = new Regex(@"drop=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex SpeedRegex = new Regex(@"speed=\s*([\d.]+)x", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegProbingService"/> class.
    /// </summary>
    /// <param name="mediaRepo">The media repository.</param>
    /// <param name="checkRepo">The frame drop check repository.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="strategyFactory">The encoder strategy factory.</param>
    public FfmpegProbingService(
        IMediaRepository mediaRepo,
        IFrameDropCheckRepository checkRepo,
        IAppLogger logger,
        Configuration.PluginConfiguration config,
        IEncoderStrategyFactory strategyFactory)
    {
        _mediaRepo = mediaRepo;
        _checkRepo = checkRepo;
        _logger = logger;
        _config = config;
        _strategyFactory = strategyFactory;
    }

    /// <inheritdoc/>
    public event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <inheritdoc/>
    public event EventHandler<CheckStatusEventArgs>? ProbeStatusChanged;

    /// <inheritdoc/>
    public async Task ProbeMediaAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        // Normalize mediaId to standard hyphenated GUID format if possible
        if (Guid.TryParse(mediaId, out var guid))
        {
            mediaId = guid.ToString();
        }

        var media = await _mediaRepo.GetByIdAsync(mediaId).ConfigureAwait(false);
        if (media == null || string.IsNullOrEmpty(media.Path) || !File.Exists(media.Path))
        {
            _logger.LogWarning($"[FfmpegProbingService] Media {mediaId} not found or path invalid.");
            EmitLog(mediaId, "Error", $"미디어를 찾을 수 없거나 경로가 유효하지 않습니다: {mediaId}");
            return;
        }

        _logger.LogInformation($"[FfmpegProbingService] Starting multi-point probes for '{media.Name}' (ID: {mediaId}). FFmpeg Path: '{_config.FfmpegPath}'");
        EmitLog(mediaId, "Info", $"정밀 성능 테스트(Probe) 시작: {media.Name} (설정된 인코더 사용)");

        var check = new Domain.Models.FrameDropCheck
        {
            MediaId = mediaId,
            CheckStartTime = DateTime.UtcNow,
            Status = "running"
        };
        check = await _checkRepo.AddAsync(check).ConfigureAwait(false);
        OnProbeStatusChanged(check);

        int totalFramesAggregated = 0;
        int droppedFramesAggregated = 0;
        double speedSum = 0;
        int speedCount = 0;

        // Sampling points: 10%, 50%, 90%
        int duration = media.Duration ?? 300; // Default to 5 mins if unknown
        int[] offsets = { (int)(duration * 0.1), (int)(duration * 0.5), (int)(duration * 0.9) };
        string[] labels = { "도입부", "중반부", "후반부" };

        try
        {
            for (int i = 0; i < offsets.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int offset = offsets[i];
                string label = labels[i];
                _logger.LogInformation($"[FfmpegProbingService] [{i + 1}/3] Probing '{media.Name}' at {offset}s ({label})");
                EmitLog(mediaId, "Info", $"[{i + 1}/3] {label} 분석 중 (위치: {offset}초)...");

                var result = await ProbeSegmentAsync(media.Path, offset, cancellationToken).ConfigureAwait(false);

                // Update aggregated stats
                totalFramesAggregated += result.TotalFrames;
                droppedFramesAggregated += result.DroppedFrames;
                if (result.AverageSpeed > 0)
                {
                    speedSum += result.AverageSpeed;
                    speedCount++;
                }

                _logger.LogInformation($"[FfmpegProbingService] Segment Result ({label}): Frames={result.TotalFrames}, Drops={result.DroppedFrames}, Speed={result.AverageSpeed:F2}x");

                // Update UI with partial progress
                OnProbeStatusChanged(check, totalFramesAggregated, droppedFramesAggregated, result.AverageSpeed);

                EmitLog(mediaId, "Info", $" - {label} 결과: {result.TotalFrames} 프레임 처리, {result.DroppedFrames} 드롭, 속도 {result.AverageSpeed:F2}x");
            }

            double avgSpeed = speedCount > 0 ? speedSum / speedCount : 0;
            double totalDropRate = totalFramesAggregated > 0 ? (double)droppedFramesAggregated / totalFramesAggregated * 100.0 : 0;

            check.Status = "completed";
            check.HasFrameDrop = droppedFramesAggregated > 0;
            check.FrameDropCount = droppedFramesAggregated;
            check.TotalFrameCount = totalFramesAggregated;
            check.CheckEndTime = DateTime.UtcNow;

            await _checkRepo.UpdateAsync(check).ConfigureAwait(false);

            // Update media health
            media.LastScanned = DateTime.UtcNow;
            media.LastScanSpeed = avgSpeed;
            media.AverageFrameDrops = media.AverageFrameDrops == null ? droppedFramesAggregated : Math.Max(media.AverageFrameDrops.Value, droppedFramesAggregated);

            // Use MAX strategy for DropRate to prevent dilution of bad results.
            // If it was bad once, it stays bad until re-encoded/reset.
            media.AverageDropRate = media.AverageDropRate == null ? totalDropRate : Math.Max(media.AverageDropRate.Value, totalDropRate);

            // Update status based on results
            // If speed is too slow (< 1.0), mark as Action Required even if drops are low (potential buffer issues)
            bool isTooSlow = avgSpeed > 0 && avgSpeed < 1.0;
            bool highDrops = media.AverageDropRate >= _config.DropThreshold;

            if (!highDrops && !isTooSlow)
            {
                media.OptimizationStatus = "Healthy";
                _logger.LogInformation($"[FfmpegProbingService] '{media.Name}' marked as Healthy.");
            }
            else if (media.OptimizationStatus == "Healthy" || media.OptimizationStatus == "Pending")
            {
                media.OptimizationStatus = "Action Required";
                string reason = isTooSlow ? "Slow Transcoding Speed" : "High Drop Rate";
                _logger.LogInformation($"[FfmpegProbingService] '{media.Name}' marked as Action Required ({reason}: Rate={media.AverageDropRate:F2}%, Speed={avgSpeed:F2}x)");
            }

            await _mediaRepo.UpdateAsync(media).ConfigureAwait(false);

            _logger.LogInformation($"[FfmpegProbingService] Full probe completed for '{media.Name}'. Avg Speed: {avgSpeed:F2}x, Total Drops: {droppedFramesAggregated}");
            EmitLog(mediaId, "Info", $"정밀 테스트 완료. 평균 속도: {avgSpeed:F2}x, 총 드롭: {droppedFramesAggregated}");

            if (isTooSlow)
            {
                EmitLog(mediaId, "Warning", $"주의: 변환 속도가 {avgSpeed:F2}x로 실시간(1.0x)보다 느려 스트리밍 시 버퍼링이 예상됩니다.");
            }

            OnProbeStatusChanged(check, totalFramesAggregated, droppedFramesAggregated, avgSpeed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"[FfmpegProbingService] Probe cancelled for '{media.Name}' (ID: {mediaId})");
            EmitLog(mediaId, "Warning", "성능 테스트가 취소되었습니다.");
            check.Status = "cancelled";
            check.CheckEndTime = DateTime.UtcNow;
            await _checkRepo.UpdateAsync(check).ConfigureAwait(false);
            OnProbeStatusChanged(check, totalFramesAggregated, droppedFramesAggregated, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FfmpegProbingService] Probe failed for {mediaId}: {ex.Message}", ex);
            EmitLog(mediaId, "Error", $"성능 테스트 실패: {ex.Message}");
            check.Status = "failed";
            check.CheckEndTime = DateTime.UtcNow;
            await _checkRepo.UpdateAsync(check).ConfigureAwait(false);
            OnProbeStatusChanged(check, totalFramesAggregated, droppedFramesAggregated, 0);
        }
    }

    private async Task<(int TotalFrames, int DroppedFrames, double AverageSpeed)> ProbeSegmentAsync(string mediaPath, int offset, CancellationToken cancellationToken)
    {
        int currentTotalFrames = 0;
        int currentDroppedFrames = 0;
        double currentSpeed = 0;

        // Use the configured strategy to simulate actual transcoding performance
        var strategy = _strategyFactory.GetStrategy(_config);

        // Build arguments using a dummy output path (Linux: /dev/null, Windows: NUL)
        // Note: Using -f null - is better for performance testing as it avoids I/O, but Strategy requires outputPath.
        // We will construct the command with a dummy path and then tweak it or just let it write to /dev/null
        string dummyOutput = "/dev/null"; // Assuming Linux environment (Jellyfin Docker)

        // Arguments: -i input ... -c:v ... output -y
        string arguments = strategy.BuildArguments(
            mediaPath,
            dummyOutput,
            _config.TargetCrf,
            "ultrafast", // Preset mainly for CPU, might be ignored by Hardware encoders
            _config.TargetBitrate
        );

        // Inject -ss {offset} and -t 10
        // Strategy.BuildArguments usually starts with "-i ...". We need to inject -ss BEFORE -i for fast seeking.
        // Or if it starts with "-hwaccel", inject -ss before -i.

        // Simple heuristic insertion:
        int inputIndex = arguments.IndexOf("-i ", StringComparison.Ordinal);
        if (inputIndex >= 0)
        {
            arguments = arguments.Insert(inputIndex, $"-ss {offset} ");
        }
        else
        {
            // Fallback if no -i found (weird)
            arguments = $"-ss {offset} " + arguments;
        }

        // Append time limit -t 10 at the end (before output) or just append.
        // Safer to use -f null - if possible, but let's just create a new Args string for Probe.
        // Actually modifying strategy output strings is brittle.
        // Let's rely on FFmpeg flexibility. Putting -t 10 at the end usually applies to output.
        arguments += " -t 10";

        // But if strategy included output filename, two outputs might be defined.
        // Let's Replace the output filename with "-" and add "-f null"
        if (arguments.Contains(dummyOutput, StringComparison.Ordinal))
        {
            arguments = arguments.Replace($"\"{dummyOutput}\"", "- -f null", StringComparison.Ordinal);
            arguments = arguments.Replace(dummyOutput, "- -f null", StringComparison.Ordinal);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.FfmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            var fMatch = FrameRegex.Match(e.Data);
            if (fMatch.Success)
            {
                currentTotalFrames = Math.Max(currentTotalFrames, int.Parse(fMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            var dMatch = DropRegex.Match(e.Data);
            if (dMatch.Success)
            {
                currentDroppedFrames = Math.Max(currentDroppedFrames, int.Parse(dMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            var sMatch = SpeedRegex.Match(e.Data);
            if (sMatch.Success && double.TryParse(sMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out double speed))
            {
                currentSpeed = speed;
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(25)); // Buffer time

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
            throw;
        }

        return (currentTotalFrames, currentDroppedFrames, currentSpeed);
    }

    private void EmitLog(string mediaId, string level, string message)
    {
        LogMessageEmitted?.Invoke(this, new LogMessageEventArgs
        {
            MediaId = mediaId,
            Level = level,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnProbeStatusChanged(Domain.Models.FrameDropCheck check, int? totalFrames = null, int? droppedFrames = null, double? speed = null)
    {
        ProbeStatusChanged?.Invoke(this, new CheckStatusEventArgs
        {
            CheckId = check.CheckId,
            MediaId = check.MediaId,
            Status = check.Status,
            Timestamp = DateTime.UtcNow,
            DroppedFrames = droppedFrames ?? check.FrameDropCount,
            TotalFrames = totalFrames ?? check.TotalFrameCount,
            SpeedFactor = speed
        });
    }
}
