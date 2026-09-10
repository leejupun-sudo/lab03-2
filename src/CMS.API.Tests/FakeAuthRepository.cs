using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IAuthRepository"/> that mirrors the SQL semantics of
/// <c>AuthRepository</c>: a single-row lookup on <c>UserId</c> using the column's
/// case-insensitive collation, the <c>AppUserRole</c> RoleIds ordered by RoleId, and the
/// SHA-256 hash format the live rows use.
/// <para>
/// Seeds take a <b>plaintext</b> password and store its hash, so a test can never accidentally
/// assert against a hand-written digest that the production hasher would not produce.
/// </para>
/// </summary>
public class FakeAuthRepository : IAuthRepository
{
    private readonly Dictionary<string, AppUserCredential> _users = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Number of lookups performed — lets a test prove the repository was consulted.</summary>
    public int LookupCount { get; private set; }

    /// <summary>Number of UserName writes attempted — lets a test prove validation ran first.</summary>
    public int UpdateCount { get; private set; }

    /// <summary>
    /// Number of per-request account checks — lets a test prove the bearer middleware really
    /// re-reads the account rather than trusting the token alone.
    /// </summary>
    public int AccountCheckCount { get; private set; }

    /// <summary>
    /// Flips a seeded account's <c>IsActive</c>, standing in for an admin deactivating someone
    /// while their token is still in flight.
    /// </summary>
    public FakeAuthRepository SetActive(string userId, bool isActive)
    {
        _users[userId].IsActive = isActive;
        return this;
    }

    /// <summary>Removes a seeded account, standing in for <c>DELETE FROM AppUser</c>.</summary>
    public FakeAuthRepository Remove(string userId)
    {
        _users.Remove(userId);
        return this;
    }

    /// <summary>The stored row, for asserting what a write did (and did not) change.</summary>
    public AppUserCredential Row(string userId) => _users[userId];

    public FakeAuthRepository Seed(
        string userId,
        string userName,
        string password,
        bool isActive = true,
        params string[] roleIds)
    {
        _users[userId] = new AppUserCredential
        {
            UserId = userId,
            UserName = userName,
            IsActive = isActive,
            PasswordHash = PasswordHasher.Sha256Hex(password),
            RoleIds = roleIds.OrderBy(r => r, StringComparer.Ordinal).ToList()
        };
        return this;
    }

    /// <summary>Overwrites a seeded row's stored hash verbatim — for malformed-hash cases.</summary>
    public FakeAuthRepository SetRawHash(string userId, string passwordHash)
    {
        _users[userId].PasswordHash = passwordHash;
        return this;
    }

    public Task<AppUserCredential?> GetCredentialAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        LookupCount++;

        // Return a copy: the controller must not be able to mutate the stored row, just as it
        // cannot mutate a database row it only SELECTed.
        var found = _users.TryGetValue(userId ?? string.Empty, out var user)
            ? new AppUserCredential
            {
                UserId = user.UserId,
                UserName = user.UserName,
                IsActive = user.IsActive,
                PasswordHash = user.PasswordHash,
                RoleIds = [.. user.RoleIds]
            }
            : null;

        return Task.FromResult(found);
    }

    /// <summary>
    /// Mirrors <c>SELECT COUNT(1) FROM AppUser WHERE UserId = @UserId AND IsActive = 1</c>:
    /// the same case-insensitive key match as the lookup, and a single <c>false</c> for both
    /// "no such row" and "row is inactive" — the two the SQL cannot tell apart either.
    /// </summary>
    public Task<bool> IsActiveAccountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        AccountCheckCount++;

        var active = _users.TryGetValue(userId ?? string.Empty, out var user) && user.IsActive;
        return Task.FromResult(active);
    }

    /// <summary>
    /// Mirrors <c>UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId</c>: the same
    /// case-insensitive key match as the lookup, one column written, and <c>false</c> when no row
    /// matched. Everything else on the row — <c>IsActive</c>, <c>PasswordHash</c>, the RoleIds —
    /// is left exactly as SQL would leave it, so a test can assert none of it moved.
    /// </summary>
    public Task<bool> UpdateUserNameAsync(
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        UpdateCount++;

        if (!_users.TryGetValue(userId ?? string.Empty, out var user))
        {
            return Task.FromResult(false);
        }

        user.UserName = userName;
        return Task.FromResult(true);
    }
}
