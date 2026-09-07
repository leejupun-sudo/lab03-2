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
}
