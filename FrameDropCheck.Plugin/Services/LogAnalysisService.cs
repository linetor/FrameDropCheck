using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using Microsoft.Extensions.Logging; // For ILogger based logging

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service implementation for analyzing FFmpeg logs.
/// </summary>
public class LogAnalysisService : ILogAnalysisService
{
    private readonly IAppLogger _logger;
    private readonly MediaBrowser.Common.Configuration.IApplicationPaths _appPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogAnalysisService"/> class.
    /// </summary>
    /// <param name="logger">The application logger.</param>
    /// <param name="appPaths">The application paths.</param>
    public LogAnalysisService(
        IAppLogger logger,
        MediaBrowser.Common.Configuration.IApplicationPaths appPaths)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
    }

    /// <inheritdoc />
    public event EventHandler<LogMessageEventArgs>? LogMessageEmitted;

    /// <inheritdoc />
    public async Task<string> AnalyzeLogsAsync(Media media, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"AnalyzeLogsAsync: start for media={media.MediaId}");
        EmitLog(media.MediaId, "Info", $"분석 중: {media.MediaId}");

        try
        {
            var candidates = new List<string>();

            // 1) If media has a path, check same directory for related log files
            if (!string.IsNullOrWhiteSpace(media.Path))
            {
                try
                {
                    var mediaDir = Path.GetDirectoryName(media.Path) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(mediaDir))
                    {
                        candidates.Add(Path.Combine(mediaDir, media.MediaId + ".ffmpeg.log"));
                        candidates.Add(Path.Combine(mediaDir, Path.GetFileNameWithoutExtension(media.Path) + ".log"));
                        candidates.Add(media.Path + ".log");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"AnalyzeLogsAsync: failed to determine media directory: {ex.Message}");
                }
            }

            string? found = null;
            var searchDirs = new List<string> { _appPaths.LogDirectoryPath };

            // Also search the parent directory of LogDirectoryPath
            try
            {
                var parentLogDir = Directory.GetParent(_appPaths.LogDirectoryPath)?.FullName;
                if (!string.IsNullOrEmpty(parentLogDir) && Directory.Exists(parentLogDir))
                {
                    searchDirs.Add(parentLogDir);
                }
            }
            catch
            {
                // Ignore
            }

            // Check for 'transcodes' folder in CachePath
            var cachePath = _appPaths.CachePath;
            if (!string.IsNullOrEmpty(cachePath))
            {
                var transcodeDir = Path.Combine(cachePath, "transcodes");
                if (Directory.Exists(transcodeDir))
                {
                    searchDirs.Add(transcodeDir);
                }

                searchDirs.Add(cachePath);
            }

            var props = _appPaths.GetType().GetProperties();
            foreach (var p in props)
            {
                try
                {
                    if (p.Name.Contains("Transcode", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = p.GetValue(_appPaths)?.ToString();
                        if (!string.IsNullOrEmpty(val) && Directory.Exists(val))
                        {
                            searchDirs.Add(val);
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            foreach (var logDir in searchDirs.Distinct())
            {
                _logger.LogInformation($"AnalyzeLogsAsync: searching in directory: {logDir}");
                if (!Directory.Exists(logDir))
                {
                    continue;
                }

                var patterns = new[] { "FFmpeg.Transcode*.log", "ffmpeg-transcode-*.log", "transcode-*.log", "ffmpeg-*.log" };
                var logFileList = new List<string>();
                foreach (var p in patterns)
                {
                    try
                    {
                        logFileList.AddRange(Directory.GetFiles(logDir, p));
                    }
                    catch
                    {
                        // Ignore access errors
                    }
                }

                var uniqueSortedLogs = logFileList.Distinct()
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(20);

                _logger.LogInformation($"AnalyzeLogsAsync: found {logFileList.Count} potential transcode logs in {logDir}, inspecting top 20...");

                var mediaIdNoHyphen = media.MediaId.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

                foreach (var logFile in uniqueSortedLogs)
                {
                    try
                    {
                        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var logReader = new StreamReader(fs);
                        int lineCount = 0;
                        string? l;
                        while ((l = await logReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null && lineCount < 200)
                        {
                            if (l.Contains(media.MediaId, StringComparison.OrdinalIgnoreCase) ||
                                l.Contains(mediaIdNoHyphen, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(media.Path) && l.Contains(media.Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                found = logFile;
                                break;
                            }
                            lineCount++;
                        }

                        if (found != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"AnalyzeLogsAsync: error reading log file {logFile}: {ex.Message}");
                    }
                }

                if (found != null)
                {
                    break;
                }
            }

            if (found == null)
            {
                _logger.LogInformation($"AnalyzeLogsAsync: no matching transcode log found for media {media.MediaId}");
                // Fallback to searching for direct ffmpeg logs in log directory as last resort candidates
                var logDir = _appPaths.LogDirectoryPath;
                candidates.Add(Path.Combine(logDir, "ffmpeg-transcode-" + media.MediaId + ".log"));
                candidates.Add(Path.Combine(logDir, "ffmpeg-transcode-" + media.MediaId.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase) + ".log"));
            }

            string? foundFile = found;
            if (foundFile == null)
            {
                foreach (var c in candidates)
                {
                    if (string.IsNullOrWhiteSpace(c))
                    {
                        continue;
                    }

                    try
                    {
                        if (File.Exists(c))
                        {
                            foundFile = c;
                            _logger.LogInformation($"AnalyzeLogsAsync: using log file {c}");
                            EmitLog(media.MediaId, "Info", $"로그 파일 발견: {c}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"AnalyzeLogsAsync: error checking candidate log {c}: {ex.Message}");
                    }
                }
            }
            else
            {
                 _logger.LogInformation($"AnalyzeLogsAsync: found official transcode log {foundFile}");
                 EmitLog(media.MediaId, "Info", $"트랜스코딩 로그 발견: {Path.GetFileName(foundFile)}");
            }

            if (foundFile == null)
            {
                _logger.LogInformation("AnalyzeLogsAsync: no log file found; returning frames_dropped=0");
                EmitLog(media.MediaId, "Warning", "로그 파일을 찾을 수 없습니다.");
                return JsonSerializer.Serialize(new { frames_dropped = 0 });
            }

            var dropEvents = new List<object>();
            var totalDrops = 0;
            var maxTotalFrames = 0;

            // Regex for 'frame=  123'
            var frameRegex = new System.Text.RegularExpressions.Regex(@"(?i)frame=\s*(\d+)");
            var dropRegex1 = new System.Text.RegularExpressions.Regex(@"(?i)(?:dropped frames|dropped frame|frame dropped|frame drop|dropped):?\s*(\d+)");
            var dropRegex2 = new System.Text.RegularExpressions.Regex(@"(?i)\bdrop=?\s*(\d+)\b");
            var timeRegex = new System.Text.RegularExpressions.Regex(@"time=(\d{2}:\d{2}:\d{2}(?:\.\d+)?)");

            using var sr = new StreamReader(foundFile);
            string? line;
            while ((line = await sr.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                // Check for total frames (cumulative counter)
                var fMatch = frameRegex.Match(line);
                if (fMatch.Success && int.TryParse(fMatch.Groups[1].Value, out int total))
                {
                    maxTotalFrames = Math.Max(maxTotalFrames, total);
                }

                // Check for cumulative drop counters (e.g., from FFmpeg's "drop=X" output)
                var dropMatch2 = dropRegex2.Match(line);
                if (dropMatch2.Success && int.TryParse(dropMatch2.Groups[1].Value, out int d2) && d2 > 0)
                {
                    totalDrops = Math.Max(totalDrops, d2); // Use Max since FFmpeg reports cumulative counters
                }

                // Look for explicit numeric declarations first (often non-cumulative or warning lines)
                var m1 = dropRegex1.Match(line);
                if (m1.Success && int.TryParse(m1.Groups[1].Value, out var n1))
                {
                    totalDrops += n1;
                    var t = timeRegex.Match(line);
                    dropEvents.Add(new { timestamp = t.Success ? t.Groups[1].Value : (string?)null, message = line.Trim(), count = n1 });
                    EmitLog(media.MediaId, "Warning", $"드롭 감지: {n1}개 프레임");
                    continue;
                }

                // Heuristic: presence of words 'drop' or 'dropped' without numeric value -> count as 1
                if (line.Contains("drop", StringComparison.OrdinalIgnoreCase) || line.Contains("dropped", StringComparison.OrdinalIgnoreCase))
                {
                    totalDrops += 1;
                    var t = timeRegex.Match(line);
                    dropEvents.Add(new { timestamp = t.Success ? t.Groups[1].Value : (string?)null, message = line.Trim(), count = 1 });
                }
            }

            _logger.LogInformation($"AnalyzeLogsAsync: finished for media={media.MediaId}. Transcoded Frames: {maxTotalFrames}, Dropped Frames: {totalDrops}");
            EmitLog(media.MediaId, "Info", $"로그 분석 완료. 총 변환 프레임: {maxTotalFrames}, 드롭 프레임: {totalDrops}");

            return JsonSerializer.Serialize(new { frames_dropped = totalDrops, total_frames = maxTotalFrames, events = dropEvents });
        }
        catch (OperationCanceledException)
        {
            EmitLog(media.MediaId, "Warning", "분석이 취소되었습니다.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"AnalyzeLogsAsync: unexpected error: {ex.Message}");
            EmitLog(media.MediaId, "Error", $"분석 중 오류: {ex.Message}");
            return JsonSerializer.Serialize(new { frames_dropped = 0 });
        }
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
}
