using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppRoleRepository : IAppRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AppRoleRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
SELECT r.pkid AS Pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
FROM AppRole r";

    public async Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " ORDER BY r.RoleId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppRole>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(r.RoleId LIKE @Keyword OR r.RoleName LIKE @Keyword OR r.Description LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            where.Add("r.PermissionLevel >= @PermissionLevelFrom");
            parameters.Add("PermissionLevelFrom", query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            where.Add("r.PermissionLevel <= @PermissionLevelTo");
            parameters.Add("PermissionLevelTo", query.PermissionLevelTo.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY r.RoleId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppRole>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<AppRole?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE r.pkid = @Pkid";
        const string userIdsSql = "SELECT ur.UserId FROM AppUserRole ur WHERE ur.RoleId = @RoleId ORDER BY ur.UserId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var role = await connection.QuerySingleOrDefaultAsync<AppRole>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        if (role is null)
        {
            return null;
        }

        var userIds = await connection.QueryAsync<string>(
            new CommandDefinition(userIdsSql, new { role.RoleId }, cancellationToken: cancellationToken));
        role.UserIds = userIds.ToList();

        return role;
    }

    public async Task<int> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { request.RoleId, request.RoleName, request.PermissionLevel, request.Description },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, request.RoleId, request.UserIds, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        // RoleId is the natural key referenced by AppUserRole, so it is immutable after creation.
        const string sql = @"
UPDATE AppRole
SET RoleName = @RoleName,
    PermissionLevel = @PermissionLevel,
    Description = @Description
WHERE pkid = @Pkid;";
        const string roleIdSql = "SELECT r.RoleId FROM AppRole r WHERE r.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var roleId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            roleIdSql, new { request.Pkid }, transaction, cancellationToken: cancellationToken));

        if (roleId is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.Pkid, request.RoleName, request.PermissionLevel, request.Description },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, roleId, request.UserIds, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string roleIdSql = "SELECT r.RoleId FROM AppRole r WHERE r.pkid = @Pkid";
        const string deleteUserRolesSql = "DELETE FROM AppUserRole WHERE RoleId = @RoleId";
        const string deleteSql = "DELETE FROM AppRole WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var roleId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            roleIdSql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (roleId is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            deleteUserRolesSql, new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return true;
    }

    public async Task<bool> RoleIdExistsAsync(string roleId, int? excludePkid = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(1) FROM AppRole r
WHERE r.RoleId = @RoleId AND (@ExcludePkid IS NULL OR r.pkid <> @ExcludePkid)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { RoleId = roleId, ExcludePkid = excludePkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    /// <summary>AppUserRole N-N sync: delete-then-reinsert on the caller's connection/transaction.</summary>
    private static async Task SyncUserRolesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string roleId,
        IEnumerable<string>? userIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM AppUserRole WHERE RoleId = @RoleId";
        const string insertSql = "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)";

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql, new { RoleId = roleId }, transaction, cancellationToken: cancellationToken));

        var ids = userIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (ids.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            ids.Select(id => new { UserId = id, RoleId = roleId }).ToList(),
            transaction,
            cancellationToken: cancellationToken));
    }
}
