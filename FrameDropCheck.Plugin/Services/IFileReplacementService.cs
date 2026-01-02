using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Provides methods for replacing media files and updating the Jellyfin library.
/// </summary>
public interface IFileReplacementService
{
    /// <summary>
    /// Replaces the original media file with the encoded one and updates the library.
    /// </summary>
    /// <param name="media">The media entity.</param>
    /// <param name="encodedJob">The completed encoding job.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReplaceAndRefreshAsync(Media media, EncodingJob encodedJob);
}
