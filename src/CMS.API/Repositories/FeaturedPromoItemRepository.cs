using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FeaturedPromoItemRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // Both FK labels ride on the row (TrainingCenterName, PromoCode) so the weekly grid renders
    // with no lookup calls — same shape as Course. Both FKs are NOT NULL, so plain JOINs.
    // No nchar columns anywhere in the three tables, so no RTRIM. No usage counts: nothing
    // references this table.
    private const string SelectColumns = @"
SELECT f.pkid AS Pkid, f.ScheduleOn,
       f.TrainingCenter_pkid AS TrainingCenterPkid, t.Name      AS TrainingCenterName,
       f.Slot,
       f.Promotion_pkid      AS PromotionPkid,      p.PromoCode AS PromoCode,
       f.Topic, f.Description
FROM FeaturedPromoItem f
JOIN TrainingCenter t ON t.pkid = f.TrainingCenter_pkid
JOIN Promotion2     p ON p.pkid = f.Promotion_pkid";

    // The unique index, in index order — a total order, so the weekly grid is stable.
    private const string OrderBy = " ORDER BY f.ScheduleOn ASC, f.TrainingCenter_pkid ASC, f.Slot ASC";

    public async Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (query.TrainingCenterPkid is { } trainingCenterPkid)
        {
            where.Add("f.TrainingCenter_pkid = @TrainingCenterPkid");
            parameters.Add("TrainingCenterPkid", trainingCenterPkid);
        }

        if (query.WeekOf is { } weekOf)
        {
            // Half-open [Monday, next Monday) on a date column — no DATEADD(day, 1) dance
            // needed because ScheduleOn is `date`, not `datetime`.
            var weekStart = FeaturedPromoItemQuery.StartOfWeek(weekOf);
            where.Add("f.ScheduleOn >= @WeekStart AND f.ScheduleOn < @WeekEnd");
            parameters.Add("WeekStart", weekStart);
            parameters.Add("WeekEnd", weekStart.AddDays(7));
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE f.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<FeaturedPromoItem>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    public async Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO FeaturedPromoItem (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
VALUES (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, WriteParameters(request), cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        // Every non-identity column is writable — the row is not an FK target, so nothing
        // depends on the key staying put. Slot is written here too (the form does not expose
        // it, but the request carries it and the move endpoints are the intended way to change it).
        const string sql = @"
UPDATE FeaturedPromoItem
SET ScheduleOn = @ScheduleOn, TrainingCenter_pkid = @TrainingCenterPkid, Slot = @Slot,
    Promotion_pkid = @PromotionPkid, Topic = @Topic, Description = @Description
WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, WriteParameters(request), cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int? excludePkid = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(1) FROM FeaturedPromoItem f
WHERE f.ScheduleOn = @ScheduleOn AND f.TrainingCenter_pkid = @TrainingCenterPkid AND f.Slot = @Slot
  AND (@ExcludePkid IS NULL OR f.pkid <> @ExcludePkid)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { ScheduleOn = scheduleOn, TrainingCenterPkid = trainingCenterPkid, Slot = slot, ExcludePkid = excludePkid },
            cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        // A straight swap would trip the unique index mid-way (two rows on the same slot), so the
        // neighbour is parked on slot 0 first. 0 is safe as a parking value: the live minimum is
        // 1 and the request model rejects anything below 1, so no real row ever sits there.
        const string findSql = @"
SELECT f.pkid AS Pkid, f.ScheduleOn, f.TrainingCenter_pkid AS TrainingCenterPkid, f.Slot
FROM FeaturedPromoItem f
WHERE f.pkid = @Pkid";
        const string neighbourSql = @"
SELECT f.pkid FROM FeaturedPromoItem f
WHERE f.ScheduleOn = @ScheduleOn AND f.TrainingCenter_pkid = @TrainingCenterPkid AND f.Slot = @Slot";
        const string setSlotSql = "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var row = await connection.QuerySingleOrDefaultAsync<SlotRow>(new CommandDefinition(
            findSql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        if (row is null)
        {
            transaction.Rollback();
            return false;
        }

        if (row.Slot == targetSlot)
        {
            transaction.Rollback();
            return true;
        }

        var neighbourPkid = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            neighbourSql,
            new { row.ScheduleOn, row.TrainingCenterPkid, Slot = targetSlot },
            transaction,
            cancellationToken: cancellationToken));

        if (neighbourPkid is { } neighbour)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                setSlotSql, new { Slot = (byte)0, Pkid = neighbour }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            setSlotSql, new { Slot = targetSlot, Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (neighbourPkid is { } parked)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                setSlotSql, new { row.Slot, Pkid = parked }, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
        return true;
    }

    private static object WriteParameters(FeaturedPromoItemRequest request) => new
    {
        request.Pkid,
        request.ScheduleOn,
        request.TrainingCenterPkid,
        request.Slot,
        request.PromotionPkid,
        Topic = request.Topic.Trim(),
        Description = request.Description.Trim()
    };

    /// <summary>The three columns MoveToSlotAsync needs to locate a neighbour.</summary>
    private sealed class SlotRow
    {
        public int Pkid { get; set; }

        public DateOnly ScheduleOn { get; set; }

        public short TrainingCenterPkid { get; set; }

        public byte Slot { get; set; }
    }
}
