using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppUserRepository : IAppUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AppUserRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // PasswordHash is never selected — it must not reach a DTO.
    private const string SelectColumns = @"
SELECT u.pkid AS Pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u";

    private const string OrderBy = " ORDER BY u.UserId ASC";

    public async Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppUser>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(u.UserId LIKE @Keyword OR u.UserName LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.IsActive.HasValue)
        {
            where.Add("u.IsActive = @IsActive");
            parameters.Add("IsActive", query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.RoleId))
        {
            where.Add("EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId)");
            parameters.Add("RoleId", query.RoleId.Trim());
        }

        if (query.PasswordUpdatedFrom is not null)
        {
            where.Add("u.PasswordUpdatedTime >= @PasswordUpdatedFrom");
            parameters.Add("PasswordUpdatedFrom", query.PasswordUpdatedFrom);
        }

        if (query.PasswordUpdatedTo is not null)
        {
            // datetime column, date-only bound: the whole To day is inclusive.
            where.Add("u.PasswordUpdatedTime < DATEADD(day, 1, @PasswordUpdatedTo)");
            parameters.Add("PasswordUpdatedTo", query.PasswordUpdatedTo);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<AppUser>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<AppUser?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE u.pkid = @Pkid";
        const string roleIdsSql = "SELECT ur.RoleId FROM AppUserRole ur WHERE ur.UserId = @UserId ORDER BY ur.RoleId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var user = await connection.QuerySingleOrDefaultAsync<AppUser>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        if (user is null)
        {
            return null;
        }

        var roleIds = await connection.QueryAsync<string>(
            new CommandDefinition(roleIdsSql, new { user.UserId }, cancellationToken: cancellationToken));
        user.RoleIds = roleIds.ToList();

        return user;
    }

    public async Task<int> CreateAsync(AppUserRequest request, string passwordHash, CancellationToken cancellationToken = default)
    {
        // PasswordUpdatedTime stays NULL: the account is on the system default password.
        const string sql = @"
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, NULL);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { request.UserId, request.UserName, request.IsActive, PasswordHash = passwordHash },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, request.UserId, request.RoleIds, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        // UserId is the clustered PK and the AppUserRole FK target — immutable.
        // PasswordHash / PasswordUpdatedTime belong to ResetPasswordAsync only.
        const string sql = @"
UPDATE AppUser
SET UserName = @UserName,
    IsActive = @IsActive
WHERE pkid = @Pkid;";
        const string userIdSql = "SELECT u.UserId FROM AppUser u WHERE u.pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var userId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            userIdSql, new { request.Pkid }, transaction, cancellationToken: cancellationToken));

        if (userId is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { request.Pkid, request.UserName, request.IsActive },
            transaction,
            cancellationToken: cancellationToken));

        await SyncUserRolesAsync(connection, transaction, userId, request.RoleIds, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string userIdSql = "SELECT u.UserId FROM AppUser u WHERE u.pkid = @Pkid";
        const string deleteUserRolesSql = "DELETE FROM AppUserRole WHERE UserId = @UserId";
        const string deleteSql = "DELETE FROM AppUser WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var userId = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            userIdSql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        if (userId is null)
        {
            transaction.Rollback();
            return false;
        }

        // The junction is owned by the user; FK_AppUserRole_AppUser is NO_ACTION, so it goes first.
        await connection.ExecuteAsync(new CommandDefinition(
            deleteUserRolesSql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql, new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int pkid, string passwordHash, CancellationToken cancellationToken = default)
    {
        // Back to the just-created state: default hash, and NULL = "on the default password".
        const string sql = @"
UPDATE AppUser
SET PasswordHash = @PasswordHash,
    PasswordUpdatedTime = NULL
WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid, PasswordHash = passwordHash }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null, CancellationToken cancellationToken = default)
    {
        // Equality inherits the column's CI collation, so this is case-insensitive.
        const string sql = @"
SELECT COUNT(1) FROM AppUser u
WHERE u.UserId = @UserId AND (@ExcludePkid IS NULL OR u.pkid <> @ExcludePkid)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { UserId = userId, ExcludePkid = excludePkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    /// <summary>AppUserRole N-N sync: delete-then-reinsert on the caller's connection/transaction.</summary>
    private static async Task SyncUserRolesAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        string userId,
        IEnumerable<string>? roleIds,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM AppUserRole WHERE UserId = @UserId";
        const string insertSql = "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)";

        await connection.ExecuteAsync(new CommandDefinition(
            deleteSql, new { UserId = userId }, transaction, cancellationToken: cancellationToken));

        var ids = roleIds?
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
            ids.Select(id => new { UserId = userId, RoleId = id }).ToList(),
            transaction,
            cancellationToken: cancellationToken));
    }
}
