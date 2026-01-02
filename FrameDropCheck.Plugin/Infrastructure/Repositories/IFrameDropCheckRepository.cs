namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the <see cref="FrameDropCheck.Plugin.Domain.Models.FrameDropCheck"/> entity.
/// </summary>
public interface IFrameDropCheckRepository : IRepository<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, int>
{
    /// <summary>
    /// Gets frame drop check results by media identifier.
    /// </summary>
    /// <param name="mediaId">The media identifier.</param>
    /// <returns>A task that returns the matching frame drop checks.</returns>
    Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetByMediaIdAsync(string mediaId);

    /// <summary>
    /// Gets frame drop check results by status.
    /// </summary>
    /// <param name="status">The check status.</param>
    /// <returns>A task that returns the matching frame drop checks.</returns>
    Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetByStatusAsync(string status);

    /// <summary>
    /// Gets frame drop check results where frame drops occurred.
    /// </summary>
    /// <returns>A task that returns the matching frame drop checks.</returns>
    Task<IEnumerable<FrameDropCheck.Plugin.Domain.Models.FrameDropCheck>> GetWithFrameDropsAsync();
}
