namespace FrameDropCheck.Plugin.Infrastructure.Logging;

/// <summary>
/// Application logger interface used for simple file logging.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// Logs an information level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogInformation(string message);

    /// <summary>
    /// Logs a debug level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogDebug(string message);

    /// <summary>
    /// Logs a warning level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogWarning(string message);

    /// <summary>
    /// Logs an error level message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="ex">The related exception, if any.</param>
    void LogError(string message, Exception? ex = null);
}
