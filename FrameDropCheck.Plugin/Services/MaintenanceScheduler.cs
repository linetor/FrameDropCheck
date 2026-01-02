using System;
using FrameDropCheck.Plugin.Configuration;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service responsible for checking maintenance windows and scheduling constraints.
/// </summary>
public class MaintenanceScheduler : ISchedulingService
{
    private readonly PluginConfiguration _config;
    private readonly IAppLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaintenanceScheduler"/> class.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="logger">The application logger.</param>
    public MaintenanceScheduler(PluginConfiguration config, IAppLogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsWithinMaintenanceWindow()
    {
        // Always get the latest configuration from the plugin instance
        var config = Plugin.Instance?.Configuration ?? _config;

        if (string.IsNullOrEmpty(config.MaintenanceStartTime) || string.IsNullOrEmpty(config.MaintenanceEndTime))
        {
             return true; // No window defined, assume always allowed
        }

        try
        {
            var now = DateTime.Now.TimeOfDay;
            var start = TimeSpan.Parse(config.MaintenanceStartTime, System.Globalization.CultureInfo.InvariantCulture);
            var end = TimeSpan.Parse(config.MaintenanceEndTime, System.Globalization.CultureInfo.InvariantCulture);

            if (start < end)
            {
                return now >= start && now <= end;
            }

            // Overnights case (e.g., 22:00 to 04:00)
            return now >= start || now <= end;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error parsing maintenance window: {ex.Message}. Defaulting to true.");
            return true;
        }
    }
}
