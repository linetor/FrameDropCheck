using System;
using System.Linq;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Repositories;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Implementation of <see cref="IOptimizationAnalyzer"/>.
/// </summary>
public class OptimizationAnalyzer : IOptimizationAnalyzer
{
    private readonly IClientPlaybackStatsRepository _clientStatsRepo;
    private readonly Configuration.PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizationAnalyzer"/> class.
    /// </summary>
    /// <param name="clientStatsRepo">The client stats repository.</param>
    /// <param name="config">The plugin configuration.</param>
    public OptimizationAnalyzer(
        IClientPlaybackStatsRepository clientStatsRepo,
        Configuration.PluginConfiguration config)
    {
        _clientStatsRepo = clientStatsRepo ?? throw new ArgumentNullException(nameof(clientStatsRepo));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public async Task<OptimizationRecommendation> AnalyzeMediaAsync(Media media)
    {
        ArgumentNullException.ThrowIfNull(media);

        // 1. Check server-side frame drops (already computed and stored in Media.AverageDropRate usually,
        // but here we might just check the property or recent checks.
        // For simplicity, we assume Media entity is up to date with server-side stats.

        bool serverSideIssue = media.AverageDropRate > _config.DropThreshold;

        if (!Guid.TryParse(media.MediaId, out var mediaGuid))
        {
             // If MediaId is not a GUID, we cannot look up client stats which are keyed by GUID.
             // Assume healthy or return a warning.
             return new OptimizationRecommendation
             {
                 Action = OptimizationAction.None,
                 Reason = "Skipped analysis: MediaId format not supported for client stats."
             };
        }

        // 2. Check client-side stats
        var clientStatsQueryResult = await _clientStatsRepo.GetByMediaIdAsync(media.MediaId).ConfigureAwait(false);
        var clientStats = clientStatsQueryResult?.ToList() ?? new List<ClientPlaybackStats>();

        // Simple logic: if more than 3 distinct clients reported > 5% drops, or average client drop rate > threshold
        var badClientReports = clientStats.Count(s =>
            s.PlaybackDuration >= 10 && // Filter out very short plays (include 10s+)
            (double)s.FramesDropped / (s.PlaybackDuration * 24) * 100 > _config.DropThreshold); // Approx 24fps assumption if total frames unknowable here

        bool clientSideIssue = badClientReports >= 2; // Threshold: at least 2 bad reports

        if (serverSideIssue || clientSideIssue)
        {
            var reason = serverSideIssue
                ? $"Server detected {media.AverageDropRate:F2}% frame drops."
                : $"Multiple clients ({badClientReports}) reported playback issues.";

            if (clientSideIssue && !serverSideIssue)
            {
                reason += " (Client-side usage implies transcoding struggle or network issues).";
            }

            return new OptimizationRecommendation
            {
                Action = OptimizationAction.ReEncode,
                Reason = reason,
                TargetBitrate = media.Bitrate > 0 ? (long)(media.Bitrate * 0.7) : null // Reduce bitrate by 30%?
            };
        }

        return new OptimizationRecommendation
        {
            Action = OptimizationAction.None,
            Reason = "Media appears healthy."
        };
    }
}
