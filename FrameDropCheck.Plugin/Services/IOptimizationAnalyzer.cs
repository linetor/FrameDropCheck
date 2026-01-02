using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Service responsible for analyzing media health and recommending optimizations.
/// </summary>
public interface IOptimizationAnalyzer
{
    /// <summary>
    /// Analyzes the specified media and returns an optimization recommendation.
    /// </summary>
    /// <param name="media">The media to analyze.</param>
    /// <returns>Optimization recommendation.</returns>
    Task<OptimizationRecommendation> AnalyzeMediaAsync(Media media);
}
