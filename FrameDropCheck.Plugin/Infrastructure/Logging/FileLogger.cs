using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin.Infrastructure.Logging;

/// <summary>
/// A very simple file-based logger. File writes are protected by a lock to be concurrency-safe.
/// </summary>
public class FileLogger : IAppLogger, IDisposable
{
    private readonly string _logPath;
    private readonly Microsoft.Extensions.Logging.ILogger? _jellyfinLogger;
    private readonly object _fileLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileLogger"/> class.
    /// </summary>
    /// <param name="logPath">The log file path.</param>
    /// <param name="jellyfinLogger">Optional Jellyfin-native logger.</param>
    public FileLogger(string logPath, Microsoft.Extensions.Logging.ILogger? jellyfinLogger = null)
    {
        _logPath = logPath;
        _jellyfinLogger = jellyfinLogger;

        // Ensure directory exists.
        var dir = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="FileLogger"/> class.
    /// </summary>
    ~FileLogger()
    {
        Dispose(false);
    }

    /// <inheritdoc/>
    public void LogInformation(string message)
    {
        Log("INFO", message);
        _jellyfinLogger?.LogInformation("FrameDropCheck: {Message}", message);
    }

    /// <inheritdoc/>
    public void LogDebug(string message)
    {
        Log("DEBUG", message);
        _jellyfinLogger?.LogDebug("FrameDropCheck: {Message}", message);
    }

    /// <inheritdoc/>
    public void LogWarning(string message)
    {
        Log("WARN", message);
        _jellyfinLogger?.LogWarning("FrameDropCheck: {Message}", message);
    }

    /// <inheritdoc/>
    public void LogError(string message, Exception? ex = null)
    {
        var formatted = ex != null ? message + "\nException: " + ex : message;
        Log("ERROR", formatted);
        if (ex != null)
        {
            _jellyfinLogger?.LogError(ex, "FrameDropCheck: {Message}", message);
        }
        else
        {
            _jellyfinLogger?.LogError("FrameDropCheck: {Message}", message);
        }
    }

    private void Log(string level, string message)
    {
        var line = new StringBuilder();
        line.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        line.Append(" [");
        line.Append(level);
        line.Append("] ");
        line.Append(message);
        line.AppendLine();

        var bytes = Encoding.UTF8.GetBytes(line.ToString());

        lock (_fileLock)
        {
            using var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            fs.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>
    /// Releases owned resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs resource cleanup.
    /// </summary>
    /// <param name="disposing">True to release managed resources; otherwise false.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Release managed resources (none currently).
        }

        // Release unmanaged resources (none currently).
        _disposed = true;
    }
}
