using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // UserId is the clustered primary key, so this is a seek. The comparison follows the
        // column collation (CI) — the same rule AppUserRepository.UserIdExistsAsync enforces on
        // create, which is why two accounts differing only in case cannot exist.
        const string credentialSql = @"
SELECT u.UserId, u.UserName, u.IsActive, u.PasswordHash
FROM AppUser u
WHERE u.UserId = @UserId";

        const string roleIdsSql = @"
SELECT ur.RoleId
FROM AppUserRole ur
WHERE ur.UserId = @UserId
ORDER BY ur.RoleId ASC";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var credential = await connection.QuerySingleOrDefaultAsync<AppUserCredential>(
            new CommandDefinition(credentialSql, new { UserId = userId }, cancellationToken: cancellationToken));

        if (credential is null)
        {
            return null;
        }

        // Join on the stored UserId, not the supplied one, so the junction lookup uses the exact
        // stored casing.
        var roleIds = await connection.QueryAsync<string>(new CommandDefinition(
            roleIdsSql, new { credential.UserId }, cancellationToken: cancellationToken));

        credential.RoleIds = roleIds.ToList();
        return credential;
    }

    public async Task<bool> IsActiveAccountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // UserId is the clustered primary key (PK_AppUser), so this is a seek — the same shape as
        // the signing-key read the middleware already does per request, and on the same order of
        // cost. IsActive is bit NOT NULL, so no null case to fold in.
        const string sql = @"
SELECT COUNT(1)
FROM AppUser u
WHERE u.UserId = @UserId AND u.IsActive = 1";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { UserId = userId }, cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task<bool> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        // One column, one row, keyed on the clustered PK — no transaction to wrap, and nothing
        // else in this statement for a request body to reach. The WHERE follows the column's CI
        // collation, the same rule the login lookup uses.
        const string sql = @"
UPDATE AppUser
SET UserName = @UserName
WHERE UserId = @UserId";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { UserId = userId, UserName = userName }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
