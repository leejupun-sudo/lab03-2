using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository : IPublishStatusRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PublishStatusRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // No JOINs (PublishStatus has no FK columns) and no RTRIM (no nchar columns).
    // The two subqueries drive the usage display and the delete guard.
    private const string SelectColumns = @"
SELECT s.pkid AS Pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued,
       (SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = s.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Promotion2 p WHERE p.PublishStatus_pkid = s.pkid) AS Promotion2Count
FROM PublishStatus s";

    public async Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " ORDER BY s.pkid ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PublishStatus>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("s.Description LIKE @Keyword");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.IsDraft.HasValue)
        {
            where.Add("s.IsDraft = @IsDraft");
            parameters.Add("IsDraft", query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            where.Add("s.IsPublished = @IsPublished");
            parameters.Add("IsPublished", query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            where.Add("s.IsDiscontinued = @IsDiscontinued");
            parameters.Add("IsDiscontinued", query.IsDiscontinued.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY s.pkid ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PublishStatus>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE s.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PublishStatus>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    public async Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is NOT an IDENTITY column — it is written explicitly and there is no SCOPE_IDENTITY().
        const string sql = @"
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.Pkid, request.Description, request.IsDraft, request.IsPublished, request.IsDiscontinued },
            cancellationToken: cancellationToken));

        return request.Pkid;
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is the key referenced by Course and Promotion2, so it is immutable after creation.
        const string sql = @"
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.Pkid, request.Description, request.IsDraft, request.IsPublished, request.IsDiscontinued },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM PublishStatus WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> PkidExistsAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(1) FROM PublishStatus s WHERE s.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<bool> IsInUseAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        // Guards DELETE: without this an FK violation would surface as a 500.
        const string sql = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course WHERE PublishStatus_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM Promotion2 WHERE PublishStatus_pkid = @Pkid)
            THEN 1 ELSE 0 END;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var inUse = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        return inUse == 1;
    }
}
