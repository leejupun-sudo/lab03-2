using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PartnerRepository : IPartnerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PartnerRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // No JOINs (Partner has no FK columns) and no RTRIM (no nchar columns; AppKey is varchar).
    // The five subqueries drive the usage display and the delete guard.
    // PartnerCourseGroup is a standalone entity, not an N-N junction — Promotion2 holds an FK
    // to its pkid, so it is counted here rather than synced delete-then-reinsert.
    // Seminar has NO foreign key constraint but does reference Partner_pkid — see IsInUseAsync.
    private const string SelectColumns = @"
SELECT p.pkid AS Pkid, p.Name, p.AppKey, p.NameOnPartnerMenu,
       p.NameOnCourseDetailPage, p.DisplayOrder, p.ImageFilename,
       (SELECT COUNT(*) FROM Course              c WHERE c.Partner_pkid        = p.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Certification       t WHERE t.Partner_pkid        = p.pkid) AS CertificationCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup  g WHERE g.Partner_pkid        = p.pkid) AS PartnerCourseGroupCount,
       (SELECT COUNT(*) FROM Promotion2          r WHERE r.RelatedPartner_pkid = p.pkid) AS Promotion2Count,
       (SELECT COUNT(*) FROM Seminar             s WHERE s.Partner_pkid        = p.pkid) AS SeminarCount
FROM Partner p";

    // 23 of the 66 live rows share DisplayOrder = 9999, so Name is needed as a tie-break —
    // without it those rows shuffle between requests and paging is unstable. Name is not
    // unique either (66 rows / 64 distinct), so pkid closes the ordering completely.
    private const string OrderBy = " ORDER BY p.DisplayOrder ASC, p.Name ASC, p.pkid ASC";

    public async Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Partner>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add(@"(p.Name LIKE @Keyword
                      OR p.AppKey LIKE @Keyword
                      OR p.NameOnPartnerMenu LIKE @Keyword
                      OR p.NameOnCourseDetailPage LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Partner>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE p.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Partner>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
    }

    public async Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY; SCOPE_IDENTITY() casts to smallint to match the column type.
        const string sql = @"
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<short>(new CommandDefinition(
            sql,
            new
            {
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                ImageFilename = NullIfBlank(request.ImageFilename)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is never written — five tables reference it, so it is immutable.
        const string sql = @"
UPDATE Partner
SET Name = @Name, AppKey = @AppKey, NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage, DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                request.Pkid,
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                ImageFilename = NullIfBlank(request.ImageFilename)
            },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Partner WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> AppKeyExistsAsync(string appKey, short? excludePkid = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(1) FROM Partner p
WHERE p.AppKey = @AppKey AND (@ExcludePkid IS NULL OR p.pkid <> @ExcludePkid)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { AppKey = appKey, ExcludePkid = excludePkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default)
    {
        // Guards DELETE: without this an FK violation would surface as a 500.
        // The Seminar clause is the one SQL Server would NOT have enforced — Seminar declares
        // no foreign key, yet 364 live rows carry a Partner_pkid. Partner 122 is referenced by
        // none of the four constrained tables and only by Seminar; deleting it would pass every
        // FK check and silently orphan 34 rows.
        const string sql = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course             WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM Certification      WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM PartnerCourseGroup WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM Promotion2         WHERE RelatedPartner_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM Seminar            WHERE Partner_pkid        = @Pkid)
            THEN 1 ELSE 0 END;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var inUse = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        return inUse == 1;
    }

    /// <summary>ImageFilename 沒有空字串資料列, 只有 NULL — 空白一律存成 NULL.</summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
