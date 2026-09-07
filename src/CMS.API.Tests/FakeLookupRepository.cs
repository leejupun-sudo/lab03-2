using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>In-memory stand-in for <see cref="ILookupRepository"/>.</summary>
public class FakeLookupRepository : ILookupRepository
{
    private readonly List<AppUserLookup> _users = [];
    private readonly List<PublishStatusLookup> _publishStatuses = [];

    public FakeLookupRepository SeedUser(string userId, string userName, bool isActive = true)
    {
        _users.Add(new AppUserLookup { UserId = userId, UserName = userName, IsActive = isActive });
        return this;
    }

    public FakeLookupRepository SeedPublishStatus(byte pkid, string description)
    {
        _publishStatuses.Add(new PublishStatusLookup { Pkid = pkid, Description = description });
        return this;
    }

    public Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppUserLookup>>(
            _users.OrderBy(u => u.UserName, StringComparer.Ordinal).ToList());

    public Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<PublishStatusLookup>>(
            _publishStatuses.OrderBy(s => s.Pkid).ToList());
}
