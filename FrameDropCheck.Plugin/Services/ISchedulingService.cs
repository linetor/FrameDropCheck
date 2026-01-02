namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service responsible for checking maintenance windows and scheduling constraints.
/// </summary>
public interface ISchedulingService
{
    /// <summary>
    /// Checks if the current time is within the configured maintenance window.
    /// </summary>
    /// <returns>True if within window or no window defined; otherwise false.</returns>
    bool IsWithinMaintenanceWindow();
}
