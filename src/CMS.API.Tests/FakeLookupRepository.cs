using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>In-memory stand-in for <see cref="ILookupRepository"/>.</summary>
public class FakeLookupRepository : ILookupRepository
{
    private readonly List<AppUserLookup> _users = [];
    private readonly List<PublishStatusLookup> _publishStatuses = [];
    private readonly List<CourseGroupLookup> _courseGroups = [];
    private readonly List<(PartnerLookup Lookup, int DisplayOrder)> _partners = [];

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

    public FakeLookupRepository SeedCourseGroup(short pkid, string description)
    {
        _courseGroups.Add(new CourseGroupLookup { Pkid = pkid, Description = description });
        return this;
    }

    public FakeLookupRepository SeedPartner(short pkid, string name, string appKey, int displayOrder)
    {
        _partners.Add((new PartnerLookup { Pkid = pkid, Name = name, AppKey = appKey }, displayOrder));
        return this;
    }

    public Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppUserLookup>>(
            _users.OrderBy(u => u.UserName, StringComparer.Ordinal).ToList());

    public Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<PublishStatusLookup>>(
            _publishStatuses.OrderBy(s => s.Pkid).ToList());

    /// <summary>Ordered by Description, matching the SQL — the live table has 215 rows.</summary>
    public Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<CourseGroupLookup>>(
            _courseGroups.OrderBy(g => g.Description, StringComparer.Ordinal).ToList());

    /// <summary>
    /// Ordered by DisplayOrder then Name, matching the SQL. The Name tie-break is not cosmetic:
    /// 23 of the 66 live rows share DisplayOrder = 9999.
    /// </summary>
    public Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<PartnerLookup>>(
            _partners
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Lookup.Name, StringComparer.Ordinal)
                .ThenBy(p => p.Lookup.Pkid)
                .Select(p => p.Lookup)
                .ToList());
}
