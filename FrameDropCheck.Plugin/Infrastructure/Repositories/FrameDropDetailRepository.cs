using System.Data;
using Dapper;
using FrameDropCheck.Plugin.Domain.Models;
using FrameDropCheck.Plugin.Infrastructure.Data;

namespace FrameDropCheck.Plugin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for <see cref="FrameDropDetail"/>.
/// </summary>
public class FrameDropDetailRepository : IFrameDropDetailRepository
{
    private readonly IDbConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDropDetailRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public FrameDropDetailRepository(FrameDropCheckDbContext dbContext)
    {
        _connection = dbContext.Connection;
    }

    /// <inheritdoc/>
    public async Task<FrameDropDetail> AddAsync(FrameDropDetail entity)
    {
        const string sql = @"
            INSERT INTO FrameDropDetail (
                CheckId, Timestamp, DropType, TimeOffset, Description)
            VALUES (
                @CheckId, @Timestamp, @DropType, @TimeOffset, @Description);
            SELECT * FROM FrameDropDetail WHERE DetailId = last_insert_rowid();";

    return await _connection.QuerySingleAsync<FrameDropDetail>(sql, entity).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(int id)
    {
        const string sql = "DELETE FROM FrameDropDetail WHERE DetailId = @Id;";
    await _connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropDetail>> GetAllAsync()
    {
        const string sql = @"
            SELECT d.*, f.*
            FROM FrameDropDetail d
            LEFT JOIN FrameDropCheck f ON d.CheckId = f.CheckId;";

        var detailDict = new Dictionary<int, FrameDropDetail>();

        await _connection.QueryAsync<FrameDropDetail, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, FrameDropDetail>(
            sql,
            (detail, check) =>
            {
                if (!detailDict.TryGetValue(detail.DetailId, out var existingDetail))
                {
                    existingDetail = detail;
                    detailDict.Add(detail.DetailId, existingDetail);
                }

                existingDetail.Check = check;

                return existingDetail;
            },

            splitOn: "CheckId"
        ).ConfigureAwait(false);

        return detailDict.Values;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropDetail>> GetByCheckIdAsync(int checkId)
    {
        const string sql = @"
            SELECT d.*, f.*
            FROM FrameDropDetail d
            LEFT JOIN FrameDropCheck f ON d.CheckId = f.CheckId
            WHERE d.CheckId = @CheckId;";

        var detailDict = new Dictionary<int, FrameDropDetail>();

        await _connection.QueryAsync<FrameDropDetail, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, FrameDropDetail>(
            sql,
            (detail, check) =>
            {
                if (!detailDict.TryGetValue(detail.DetailId, out var existingDetail))
                {
                    existingDetail = detail;
                    detailDict.Add(detail.DetailId, existingDetail);
                }
                existingDetail.Check = check;
                return existingDetail;
            },

            new { CheckId = checkId },

            splitOn: "CheckId"
        ).ConfigureAwait(false);

        return detailDict.Values;
    }

    /// <inheritdoc/>
    public async Task<FrameDropDetail?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT d.*, f.*
            FROM FrameDropDetail d
            LEFT JOIN FrameDropCheck f ON d.CheckId = f.CheckId
            WHERE d.DetailId = @Id;";

        var details = await _connection.QueryAsync<FrameDropDetail, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, FrameDropDetail>(
            sql,
            (detail, check) =>
            {
                detail.Check = check;
                return detail;
            },

            new { Id = id },

            splitOn: "CheckId"
        ).ConfigureAwait(false);

        return details.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FrameDropDetail>> GetByDropTypeAsync(string dropType)
    {
        const string sql = @"
            SELECT d.*, f.*
            FROM FrameDropDetail d
            LEFT JOIN FrameDropCheck f ON d.CheckId = f.CheckId
            WHERE d.DropType = @DropType;";

        var detailDict = new Dictionary<int, FrameDropDetail>();

        await _connection.QueryAsync<FrameDropDetail, FrameDropCheck.Plugin.Domain.Models.FrameDropCheck, FrameDropDetail>(
            sql,
            (detail, check) =>
            {
                if (!detailDict.TryGetValue(detail.DetailId, out var existingDetail))
                {
                    existingDetail = detail;
                    detailDict.Add(detail.DetailId, existingDetail);
                }
                existingDetail.Check = check;
                return existingDetail;
            },

            new { DropType = dropType },

            splitOn: "CheckId"
        ).ConfigureAwait(false);

        return detailDict.Values;
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(FrameDropDetail entity)
    {
        const string sql = @"
            UPDATE FrameDropDetail
            SET Timestamp = @Timestamp,
                DropType = @DropType,
                TimeOffset = @TimeOffset,
                Description = @Description
            WHERE DetailId = @DetailId;";

        await _connection.ExecuteAsync(sql, entity).ConfigureAwait(false);
    }
}
