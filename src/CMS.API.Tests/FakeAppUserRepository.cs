using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IAppUserRepository"/> that mirrors the SQL semantics of
/// <c>AppUserRepository</c>: keyword LIKE on UserId / UserName, the IsActive / RoleId /
/// date-range predicates, <c>UserId ASC</c> ordering, IDENTITY pkid assignment, the
/// AppUserRole junction (delete-then-reinsert, deleted with the user), and a
/// case-insensitive UserId uniqueness rule (the column collation is CI).
/// <para>
/// The hash is held in a private dictionary, never on <see cref="AppUser"/> — the model has no
/// such property by contract. <see cref="PasswordHashOf"/> lets tests inspect it.
/// </para>
/// </summary>
public class FakeAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = [];
    private readonly Dictionary<int, string> _passwordHashes = [];
    private readonly Dictionary<string, List<string>> _userRoles = new(StringComparer.OrdinalIgnoreCase);
    private int _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakeAppUserRepository Seed(
        string userId,
        string userName,
        bool isActive = true,
        DateTime? passwordUpdatedTime = null,
        params string[] roleIds)
    {
        _users.Add(new AppUser
        {
            Pkid = _nextPkid++,
            UserId = userId,
            UserName = userName,
            IsActive = isActive,
            PasswordUpdatedTime = passwordUpdatedTime
        });
        _passwordHashes[_nextPkid - 1] = "seeded-hash";
        _userRoles[userId] = roleIds.ToList();
        return this;
    }

    /// <summary>The stored hash for a pkid, or null when the row does not exist.</summary>
    public string? PasswordHashOf(int pkid) =>
        _passwordHashes.TryGetValue(pkid, out var hash) ? hash : null;

    /// <summary>The junction rows for a UserId, as the SQL would hold them.</summary>
    public IReadOnlyList<string> RoleIdsOf(string userId) =>
        _userRoles.TryGetValue(userId, out var roles) ? roles : [];

    public Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppUser>>(Ordered(_users).Select(Project).ToList());

    public Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<AppUser> results = _users;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(u => Contains(u.UserId, keyword) || Contains(u.UserName, keyword));
        }

        if (query.IsActive.HasValue)
        {
            results = results.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.RoleId))
        {
            var roleId = query.RoleId.Trim();
            results = results.Where(u =>
                RoleIdsOf(u.UserId).Any(r => string.Equals(r, roleId, StringComparison.OrdinalIgnoreCase)));
        }

        if (query.PasswordUpdatedFrom is not null)
        {
            var from = query.PasswordUpdatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            results = results.Where(u => u.PasswordUpdatedTime >= from);
        }

        if (query.PasswordUpdatedTo is not null)
        {
            // Mirrors `< DATEADD(day, 1, @To)` — the whole To day is inclusive.
            var toExclusive = query.PasswordUpdatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            results = results.Where(u => u.PasswordUpdatedTime < toExclusive);
        }

        return Task.FromResult<IEnumerable<AppUser>>(Ordered(results).Select(Project).ToList());
    }

    public Task<AppUser?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult<AppUser?>(null);
        }

        var projected = Project(user);
        projected.RoleIds = RoleIdsOf(user.UserId).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult<AppUser?>(projected);
    }

    public Task<int> CreateAsync(AppUserRequest request, string passwordHash, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY — assigned here, never taken from the request.
        var user = new AppUser
        {
            Pkid = _nextPkid++,
            UserId = request.UserId,
            UserName = request.UserName,
            IsActive = request.IsActive,
            PasswordUpdatedTime = null
        };
        _users.Add(user);
        _passwordHashes[user.Pkid] = passwordHash;
        SyncUserRoles(user.UserId, request.RoleIds);
        return Task.FromResult(user.Pkid);
    }

    public Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var user = _users.SingleOrDefault(u => u.Pkid == request.Pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        // UserId and the password columns are never written.
        user.UserName = request.UserName;
        user.IsActive = request.IsActive;
        SyncUserRoles(user.UserId, request.RoleIds);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        _userRoles.Remove(user.UserId);
        _passwordHashes.Remove(pkid);
        _users.Remove(user);
        return Task.FromResult(true);
    }

    public Task<bool> ResetPasswordAsync(int pkid, string passwordHash, CancellationToken cancellationToken = default)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        _passwordHashes[pkid] = passwordHash;
        user.PasswordUpdatedTime = null;
        return Task.FromResult(true);
    }

    public Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Any(u =>
            string.Equals(u.UserId, userId, StringComparison.OrdinalIgnoreCase)
            && (excludePkid is null || u.Pkid != excludePkid)));

    private void SyncUserRoles(string userId, IEnumerable<string>? roleIds) =>
        _userRoles[userId] = roleIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static bool Contains(string value, string keyword) =>
        value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<AppUser> Ordered(IEnumerable<AppUser> users) =>
        users.OrderBy(u => u.UserId, StringComparer.OrdinalIgnoreCase);

    private AppUser Project(AppUser user) => new()
    {
        Pkid = user.Pkid,
        UserId = user.UserId,
        UserName = user.UserName,
        IsActive = user.IsActive,
        PasswordUpdatedTime = user.PasswordUpdatedTime,
        RoleCount = RoleIdsOf(user.UserId).Count
    };
}
