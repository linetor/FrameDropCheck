using System;
using System.IO;
using System.Threading.Tasks;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Logging;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace FrameDropCheck.Plugin.Services;

/// <summary>
/// Implementation of <see cref="IFileReplacementService"/> that handles file operations and library updates.
/// </summary>
public class FileReplacementService : IFileReplacementService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IAppLogger _logger;
    private readonly string _backupPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileReplacementService"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="logger">The application logger.</param>
    /// <param name="config">The plugin configuration.</param>
    public FileReplacementService(
        ILibraryManager libraryManager,
        IAppLogger logger,
        Configuration.PluginConfiguration config)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backupPath = config.BackupPath;
    }

    /// <inheritdoc/>
    public async Task ReplaceAndRefreshAsync(Media media, EncodingJob encodedJob)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(encodedJob);

        if (encodedJob.Status != "completed" || string.IsNullOrEmpty(encodedJob.NewFilePath))
        {
            throw new InvalidOperationException("Cannot replace file with an incomplete or invalid encoding job.");
        }

        try
        {
            if (!File.Exists(encodedJob.NewFilePath))
            {
                throw new FileNotFoundException("Encoded file not found.", encodedJob.NewFilePath);
            }

            // 1. Backup or Delete original
            if (!string.IsNullOrEmpty(_backupPath))
            {
                if (!Directory.Exists(_backupPath))
                {
                    Directory.CreateDirectory(_backupPath);
                }

                var backupFileName = Path.GetFileName(encodedJob.OriginalPath);
                var destination = Path.Combine(_backupPath, backupFileName);

                // If backup already exists, add timestamp
                if (File.Exists(destination))
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                    destination = Path.Combine(_backupPath, $"{Path.GetFileNameWithoutExtension(backupFileName)}_{timestamp}{Path.GetExtension(backupFileName)}");
                }

                _logger.LogInformation($"Backing up original file to: {destination}");
                File.Move(encodedJob.OriginalPath, destination);
                encodedJob.BackupPath = destination;
            }
            else
            {
                _logger.LogInformation($"Deleting original file: {encodedJob.OriginalPath}");
                File.Delete(encodedJob.OriginalPath);
            }

            // 2. Update Jellyfin Library
            _logger.LogInformation($"Refreshing library for media ID: {media.MediaId}");

            // Trigger a general library scan to find the new [encoded] file
            await _libraryManager.ValidateMediaLibrary(new Progress<double>(), default).ConfigureAwait(false);

            _logger.LogInformation($"Media replacement workflow finished for {media.Name}.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during file replacement for {media.Name}: {ex.Message}");
            throw;
        }
    }
}
