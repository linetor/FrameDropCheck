namespace FrameDropCheck.Plugin.Domain.Models;

/// <summary>
/// Recommendation result from the Optimization Analyzer.
/// </summary>
public enum OptimizationAction
{
    /// <summary>
    /// No action needed. Media is healthy.
    /// </summary>
    None,

    /// <summary>
    /// Media should be re-encoded to improve compatibility or fix corruption.
    /// </summary>
    ReEncode,

    /// <summary>
    /// Media is severely broken and should be replaced/deleted.
    /// </summary>
    Replace
}

/// <summary>
/// Detailed recommendation for a media item.
/// </summary>
public class OptimizationRecommendation
{
    /// <summary>
    /// Gets or sets the recommended action.
    /// </summary>
    public OptimizationAction Action { get; set; }

    /// <summary>
    /// Gets or sets the reason for the recommendation.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommended target bitrate (if applicable).
    /// </summary>
    public long? TargetBitrate { get; set; }
}
