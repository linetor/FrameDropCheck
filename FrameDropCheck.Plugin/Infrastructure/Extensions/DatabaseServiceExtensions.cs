using FrameDropCheck.Plugin.Infrastructure.Data;
using FrameDropCheck.Plugin.Infrastructure.Data.Transactions;
using FrameDropCheck.Plugin.Infrastructure.Repositories;
using FrameDropCheck.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FrameDropCheck.Plugin.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods to register database-related services.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Registers the FrameDropCheck SQLite database and related services (UnitOfWork and repositories) into
    /// the provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="dbPath">Path to the SQLite database file used by the plugin.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddFrameDropCheckDatabase(
        this IServiceCollection services,
        string dbPath)
    {
        // Register DbContext
        services.AddSingleton(new FrameDropCheckDbContext(dbPath));

        // Transaction management
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IFrameDropCheckRepository, FrameDropCheckRepository>();
        services.AddScoped<IFrameDropDetailRepository, FrameDropDetailRepository>();
        services.AddScoped<IEncodingJobRepository, EncodingJobRepository>();
        services.AddScoped<IBackupCleanupLogRepository, BackupCleanupLogRepository>();
        services.AddScoped<IClientPlaybackStatsRepository, ClientPlaybackStatsRepository>();

        // Strategies
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategy, FrameDropCheck.Plugin.Services.Encoding.CpuLibX264Strategy>();
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategy, FrameDropCheck.Plugin.Services.Encoding.NvencH264Strategy>();
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategy, FrameDropCheck.Plugin.Services.Encoding.VaapiH264Strategy>();
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategy, FrameDropCheck.Plugin.Services.Encoding.QsvH264Strategy>();
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategy, FrameDropCheck.Plugin.Services.Encoding.V4l2H264Strategy>();
        services.AddScoped<FrameDropCheck.Plugin.Services.Encoding.IEncoderStrategyFactory, FrameDropCheck.Plugin.Services.Encoding.EncoderStrategyFactory>();

        // Domain services
        services.AddScoped<ILogAnalysisService, LogAnalysisService>();
        services.AddScoped<IFrameDropCheckService, FrameDropCheckService>();
        // Jellyfin internal API wrapper (best-effort implementation that works inside the server)
        services.AddScoped<IJellyfinApiService, InternalJellyfinApiService>();

        // Central manager for background tasks and real-time streaming - needs to be singleton
        services.AddSingleton<IFrameDropCheckManager, FrameDropCheckManager>();

        // Domain services that depend on configuration - MUST BE SCOPED
        // Otherwise they hold a stale configuration object if they were created before a config change.
        services.AddScoped<IFfmpegProbingService, FfmpegProbingService>();
        services.AddScoped<IEncodingService, FfmpegEncodingService>();
        services.AddScoped<IFileReplacementService, FileReplacementService>();
        services.AddScoped<ISchedulingService, MaintenanceScheduler>();
        services.AddScoped<IOptimizationAnalyzer, OptimizationAnalyzer>();
        services.AddScoped<MediaSyncService>();

        // Playback monitoring and Web Injection
        services.AddSingleton<WebInjectionService>();
        services.AddSingleton<IHostedService, WebInjectionStartupService>();
        services.AddSingleton<IHostedService, PlaybackMonitor>();

        // Always resolve the latest configuration from the Plugin instance
        services.AddTransient(sp => Plugin.Instance?.Configuration ?? new Configuration.PluginConfiguration());

        return services;
    }
}
