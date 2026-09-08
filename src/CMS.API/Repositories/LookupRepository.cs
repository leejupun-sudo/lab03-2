using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LookupRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT u.UserId, u.UserName, u.IsActive
            FROM AppUser u
            ORDER BY u.UserName ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppUserLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default)
    {
        // 2 live rows; RoleId is the clustered PK, so it orders alone — same key as the role list.
        const string sql = """
            SELECT r.pkid AS Pkid, r.RoleId, r.RoleName
            FROM AppRole r
            ORDER BY r.RoleId ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppRoleLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT s.pkid AS Pkid, s.Description
            FROM PublishStatus s
            ORDER BY s.pkid ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PublishStatusLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default)
    {
        // 215 rows in the live database — ordered by name so a filterable dropdown is browsable.
        const string sql = """
            SELECT g.pkid AS Pkid, g.Description
            FROM CourseGroup g
            ORDER BY g.Description ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseGroupLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        // 66 rows — past the 10-option filter threshold but below the ~100 virtual-scroll one.
        // Name ASC is the tie-break behind DisplayOrder (23 rows share 9999); pkid closes it,
        // since Name is not unique either.
        const string sql = """
            SELECT p.pkid AS Pkid, p.Name, p.AppKey
            FROM Partner p
            ORDER BY p.DisplayOrder ASC, p.Name ASC, p.pkid ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<PartnerLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default)
    {
        // Title is nchar(100) — RTRIM or every label carries trailing padding.
        // Ordered by partner (same keys as the partner lookup) then title, per sample1.
        const string sql = """
            SELECT c.pkid AS Pkid, RTRIM(c.Title) AS Title, p.Name AS PartnerName
            FROM Certification c
            JOIN Partner p ON p.pkid = c.Partner_pkid
            ORDER BY p.DisplayOrder ASC, p.Name ASC, p.pkid ASC, c.Title ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CertificationLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default)
    {
        // 18 rows, no DisplayOrder column — pkid order is the house order.
        const string sql = """
            SELECT j.pkid AS Pkid, j.Description
            FROM JobCategory j
            ORDER BY j.pkid ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<JobCategoryLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<CourseLookup>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        // ~1084 rows — far past the virtual-scroll threshold; CourseId is unique so it orders alone.
        const string sql = """
            SELECT c.pkid AS Pkid, c.CourseId, c.Title
            FROM Course c
            ORDER BY c.CourseId ASC
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<CourseLookup>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
