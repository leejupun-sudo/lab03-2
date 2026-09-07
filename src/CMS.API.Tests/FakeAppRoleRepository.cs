using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IAppRoleRepository"/> that mirrors the SQL semantics
/// of <c>AppRoleRepository</c> (keyword LIKE, permission-level range, RoleId uniqueness,
/// AppUserRole delete-then-reinsert, immutable RoleId on update).
/// </summary>
public class FakeAppRoleRepository : IAppRoleRepository
{
    private readonly List<AppRole> _roles = [];
    private readonly Dictionary<string, List<string>> _userRoles = new(StringComparer.OrdinalIgnoreCase);
    private int _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakeAppRoleRepository Seed(string roleId, string roleName, int permissionLevel, string? description, params string[] userIds)
    {
        _roles.Add(new AppRole
        {
            Pkid = _nextPkid++,
            RoleId = roleId,
            RoleName = roleName,
            PermissionLevel = permissionLevel,
            Description = description
        });
        _userRoles[roleId] = [.. userIds];
        return this;
    }

    public Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppRole>>(_roles.OrderBy(r => r.RoleId, StringComparer.Ordinal).Select(Project).ToList());

    public Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<AppRole> results = _roles;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(r =>
                r.RoleId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                r.RoleName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (r.Description ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            results = results.Where(r => r.PermissionLevel >= query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            results = results.Where(r => r.PermissionLevel <= query.PermissionLevelTo.Value);
        }

        return Task.FromResult<IEnumerable<AppRole>>(
            results.OrderBy(r => r.RoleId, StringComparer.Ordinal).Select(Project).ToList());
    }

    public Task<AppRole?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var role = _roles.SingleOrDefault(r => r.Pkid == pkid);
        if (role is null)
        {
            return Task.FromResult<AppRole?>(null);
        }

        var projected = Project(role);
        projected.UserIds = [.. UsersOf(role.RoleId).OrderBy(u => u, StringComparer.Ordinal)];
        return Task.FromResult<AppRole?>(projected);
    }

    public Task<int> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = new AppRole
        {
            Pkid = _nextPkid++,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description
        };
        _roles.Add(role);
        SyncUsers(role.RoleId, request.UserIds);
        return Task.FromResult(role.Pkid);
    }

    public Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var role = _roles.SingleOrDefault(r => r.Pkid == request.Pkid);
        if (role is null)
        {
            return Task.FromResult(false);
        }

        // RoleId is immutable — the repository re-reads it from the row rather than the request.
        role.RoleName = request.RoleName;
        role.PermissionLevel = request.PermissionLevel;
        role.Description = request.Description;
        SyncUsers(role.RoleId, request.UserIds);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var role = _roles.SingleOrDefault(r => r.Pkid == pkid);
        if (role is null)
        {
            return Task.FromResult(false);
        }

        _roles.Remove(role);
        _userRoles.Remove(role.RoleId);
        return Task.FromResult(true);
    }

    public Task<bool> RoleIdExistsAsync(string roleId, int? excludePkid = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_roles.Any(r =>
            string.Equals(r.RoleId, roleId, StringComparison.OrdinalIgnoreCase) &&
            (!excludePkid.HasValue || r.Pkid != excludePkid.Value)));

    private AppRole Project(AppRole role) => new()
    {
        Pkid = role.Pkid,
        RoleId = role.RoleId,
        RoleName = role.RoleName,
        PermissionLevel = role.PermissionLevel,
        Description = role.Description,
        UserCount = UsersOf(role.RoleId).Count
    };

    private List<string> UsersOf(string roleId) =>
        _userRoles.TryGetValue(roleId, out var users) ? users : [];

    private void SyncUsers(string roleId, IEnumerable<string>? userIds) =>
        _userRoles[roleId] = userIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
}
