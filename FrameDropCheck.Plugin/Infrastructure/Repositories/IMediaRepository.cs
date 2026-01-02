using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository interface for the <see cref="Media"/> entity.
/// </summary>
public interface IMediaRepository : IRepository<Media, string>
{
    /// <summary>
    /// 처리되지 않은 미디어 목록을 조회합니다.
    /// </summary>
    /// <returns>A task that returns unprocessed media items.</returns>
    Task<IEnumerable<Media>> GetUnprocessedMediaAsync();

    /// <summary>
    /// Gets a media entry by its file path.
    /// </summary>
    /// <param name="path">The media file path.</param>
    /// <returns>A task that returns the media if found; otherwise null.</returns>
    Task<Media?> GetByPathAsync(string path);

    /// <summary>
    /// Gets media items that need scanning because their last scanned time is older than the threshold.
    /// </summary>
    /// <param name="threshold">The threshold time.</param>
    /// <returns>A task that returns the media items needing scan.</returns>
    Task<IEnumerable<Media>> GetMediaNeedingScanAsync(DateTime threshold);
}
