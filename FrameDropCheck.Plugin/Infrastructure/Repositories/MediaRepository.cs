using System.Data;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="Media"/>.
/// </summary>
public class MediaRepository : IMediaRepository
{
    private readonly IDbConnection _connection;
    private readonly Logging.IAppLogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The application logger.</param>
    public MediaRepository(FrameDropCheckDbContext dbContext, Logging.IAppLogger logger)
    {
        _connection = dbContext.Connection;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Media> AddAsync(Media entity)
    {
        const string sql = @"
            INSERT INTO Media (MediaId, Path, Name, Duration, Size, LastModified, LastScanned, IsProcessed, CreatedAt, OptimizationStatus, AverageFrameDrops, AverageDropRate, Codec, Resolution, Bitrate, OriginalBitrate, CompressionRatio, LastScanSpeed)
            VALUES (@MediaId, @Path, @Name, @Duration, @Size, @LastModified, @LastScanned, @IsProcessed, @CreatedAt, @OptimizationStatus, @AverageFrameDrops, @AverageDropRate, @Codec, @Resolution, @Bitrate, @OriginalBitrate, @CompressionRatio, @LastScanSpeed);
            SELECT * FROM Media WHERE MediaId = @MediaId;";

    return await _connection.QuerySingleAsync<Media>(sql, entity).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id)
    {
        const string sql = "DELETE FROM Media WHERE MediaId = @Id;";
    await _connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Media>> GetAllAsync()
    {
        const string sql = "SELECT * FROM Media;";
    return await _connection.QueryAsync<Media>(sql).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Media?> GetByIdAsync(string id)
    {
        const string sql = "SELECT * FROM Media WHERE MediaId = @Id COLLATE NOCASE;";
        return await _connection.QuerySingleOrDefaultAsync<Media>(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Media?> GetByPathAsync(string path)
    {
        const string sql = "SELECT * FROM Media WHERE Path = @Path;";
    return await _connection.QuerySingleOrDefaultAsync<Media>(sql, new { Path = path }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Media>> GetMediaNeedingScanAsync(DateTime threshold)
    {
        const string sql = @"
            SELECT * FROM Media
            WHERE LastScanned IS NULL
               OR LastScanned < @Threshold;";

    return await _connection.QueryAsync<Media>(sql, new { Threshold = threshold }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Media>> GetUnprocessedMediaAsync()
    {
        const string sql = "SELECT * FROM Media WHERE IsProcessed = 0;";
    return await _connection.QueryAsync<Media>(sql).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Media entity)
    {
        const string sql = @"
            UPDATE Media
            SET Path = @Path,
                Name = @Name,
                Duration = @Duration,
                Size = @Size,
                LastModified = @LastModified,
                LastScanned = @LastScanned,
                IsProcessed = @IsProcessed,
                OptimizationStatus = @OptimizationStatus,
                AverageFrameDrops = @AverageFrameDrops,
                AverageDropRate = @AverageDropRate,
                Codec = @Codec,
                Resolution = @Resolution,
                Bitrate = @Bitrate,
                OriginalBitrate = @OriginalBitrate,
                CompressionRatio = @CompressionRatio,

                LastScanSpeed = @LastScanSpeed
            WHERE MediaId = @MediaId COLLATE NOCASE;";

        int affected = await _connection.ExecuteAsync(sql, entity).ConfigureAwait(false);
        if (affected == 0)
        {
            _logger.LogWarning($"[MediaRepository] UpdateAsync failed to update 0 rows for MediaId: {entity.MediaId}. Check if ID matches case-insensitively.");
        }
        else
        {
            _logger.LogInformation($"[MediaRepository] Updated MediaId: {entity.MediaId}. Affected rows: {affected}. Status={entity.OptimizationStatus}, Speed={entity.LastScanSpeed}");
        }
    }
}
