using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseGroupRepository : ICourseGroupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CourseGroupRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // No JOINs (CourseGroup has no FK columns) and no RTRIM (no nchar columns).
    // The two subqueries drive the usage display and the delete guard.
    // PartnerCourseGroup is a standalone entity, not an N-N junction — Promotion2 holds an
    // FK to its pkid, so it is counted here rather than synced delete-then-reinsert.
    private const string SelectColumns = @"
SELECT g.pkid AS Pkid, g.Description,
       (SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = g.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup p WHERE p.CourseGroup_pkid = g.pkid) AS PartnerCourseGroupCount
FROM CourseGroup g";

    public async Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " ORDER BY g.Description ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseGroup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("g.Description LIKE @Keyword");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY g.Description ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseGroup>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE g.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CourseGroup>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    public async Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY; SCOPE_IDENTITY() casts to smallint to match the column type.
        const string sql = @"
INSERT INTO CourseGroup (Description) VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            sql, new { request.Description }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CourseGroup SET Description = @Description WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { request.Pkid, request.Description }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM CourseGroup WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default)
    {
        // Guards DELETE: without this an FK violation would surface as a 500.
        const string sql = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course WHERE CourseGroup_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM PartnerCourseGroup WHERE CourseGroup_pkid = @Pkid)
            THEN 1 ELSE 0 END;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var inUse = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        return inUse == 1;
    }
}
