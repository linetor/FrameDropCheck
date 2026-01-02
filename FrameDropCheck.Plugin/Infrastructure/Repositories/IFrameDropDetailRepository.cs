using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the <see cref="FrameDropDetail"/> entity.
/// </summary>
public interface IFrameDropDetailRepository : IRepository<FrameDropDetail, int>
{
    /// <summary>
    /// Gets frame drop detail entries by check identifier.
    /// </summary>
    /// <param name="checkId">The check identifier.</param>
    /// <returns>A task that returns the matching details.</returns>
    Task<IEnumerable<FrameDropDetail>> GetByCheckIdAsync(int checkId);

    /// <summary>
    /// Gets frame drop detail entries by drop type.
    /// </summary>
    /// <param name="dropType">The drop type.</param>
    /// <returns>A task that returns the matching details.</returns>
    Task<IEnumerable<FrameDropDetail>> GetByDropTypeAsync(string dropType);
}
