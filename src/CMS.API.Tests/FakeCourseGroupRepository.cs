using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="ICourseGroupRepository"/> that mirrors the SQL semantics
/// of <c>CourseGroupRepository</c> (keyword LIKE on Description, Description ASC ordering,
/// IDENTITY pkid assignment, Course/PartnerCourseGroup usage counts).
/// <para>
/// Note there is deliberately no uniqueness rule on Description — the live table holds
/// duplicates, so the repository must accept them.
/// </para>
/// </summary>
public class FakeCourseGroupRepository : ICourseGroupRepository
{
    private readonly List<CourseGroup> _groups = [];
    private short _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakeCourseGroupRepository Seed(
        string description,
        int courseCount = 0,
        int partnerCourseGroupCount = 0)
    {
        _groups.Add(new CourseGroup
        {
            Pkid = _nextPkid++,
            Description = description,
            CourseCount = courseCount,
            PartnerCourseGroupCount = partnerCourseGroupCount
        });
        return this;
    }

    public Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<CourseGroup>>(Ordered(_groups).Select(Project).ToList());

    public Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<CourseGroup> results = _groups;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(g => g.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IEnumerable<CourseGroup>>(Ordered(results).Select(Project).ToList());
    }

    public Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        return Task.FromResult(group is null ? null : Project(group));
    }

    public Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY — assigned here, never taken from the request.
        var group = new CourseGroup
        {
            Pkid = _nextPkid++,
            Description = request.Description
        };
        _groups.Add(group);
        return Task.FromResult(group.Pkid);
    }

    public Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var group = _groups.SingleOrDefault(g => g.Pkid == request.Pkid);
        if (group is null)
        {
            return Task.FromResult(false);
        }

        group.Description = request.Description;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        if (group is null)
        {
            return Task.FromResult(false);
        }

        _groups.Remove(group);
        return Task.FromResult(true);
    }

    public Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        return Task.FromResult(
            group is not null && (group.CourseCount > 0 || group.PartnerCourseGroupCount > 0));
    }

    private static IEnumerable<CourseGroup> Ordered(IEnumerable<CourseGroup> groups) =>
        groups.OrderBy(g => g.Description, StringComparer.Ordinal).ThenBy(g => g.Pkid);

    private static CourseGroup Project(CourseGroup group) => new()
    {
        Pkid = group.Pkid,
        Description = group.Description,
        CourseCount = group.CourseCount,
        PartnerCourseGroupCount = group.PartnerCourseGroupCount
    };
}
