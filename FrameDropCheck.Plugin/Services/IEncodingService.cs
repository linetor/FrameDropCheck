using System.Threading;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Provides methods for encoding media files.
/// </summary>
public interface IEncodingService
{
    /// <summary>
    /// Starts an encoding job for the specified media.
    /// </summary>
    /// <param name="media">The media to encode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<EncodingJob> EncodeMediaAsync(Media media, CancellationToken cancellationToken = default);
}
