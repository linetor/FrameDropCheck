using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the <see cref="EncodingJob"/> entity.
/// </summary>
public interface IEncodingJobRepository : IRepository<EncodingJob, int>
{
    /// <summary>
    /// Gets encoding jobs by media identifier.
    /// </summary>
    /// <param name="mediaId">The media identifier.</param>
    /// <returns>A task that returns the matching encoding jobs.</returns>
    Task<IEnumerable<EncodingJob>> GetByMediaIdAsync(string mediaId);

    /// <summary>
    /// Gets encoding jobs by status.
    /// </summary>
    /// <param name="status">The job status.</param>
    /// <returns>A task that returns the matching encoding jobs.</returns>
    Task<IEnumerable<EncodingJob>> GetByStatusAsync(string status);

    /// <summary>
    /// Gets all in-progress encoding jobs.
    /// </summary>
    /// <returns>A task that returns the in-progress jobs.</returns>
    Task<IEnumerable<EncodingJob>> GetInProgressJobsAsync();

    /// <summary>
    /// Gets failed encoding jobs.
    /// </summary>
    /// <returns>A task that returns the failed jobs.</returns>
    Task<IEnumerable<EncodingJob>> GetFailedJobsAsync();
}
