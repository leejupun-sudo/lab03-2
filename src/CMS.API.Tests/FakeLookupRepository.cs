using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>In-memory stand-in for <see cref="ILookupRepository"/>.</summary>
public class FakeLookupRepository : ILookupRepository
{
    private readonly List<AppUserLookup> _users = [];
    private readonly List<AppRoleLookup> _roles = [];
    private readonly List<PublishStatusLookup> _publishStatuses = [];
    private readonly List<CourseGroupLookup> _courseGroups = [];
    private readonly List<(PartnerLookup Lookup, int DisplayOrder)> _partners = [];
    private readonly List<(CertificationLookup Lookup, int PartnerDisplayOrder, short PartnerPkid)> _certifications = [];
    private readonly List<JobCategoryLookup> _jobCategories = [];
    private readonly List<CourseLookup> _courses = [];
    private readonly List<(TrainingCenterLookup Lookup, int DisplayOrder)> _trainingCenters = [];
    private readonly List<(Promotion2Lookup Lookup, DateOnly ScheduleOn)> _promotion2s = [];

    public FakeLookupRepository SeedUser(string userId, string userName, bool isActive = true)
    {
        _users.Add(new AppUserLookup { UserId = userId, UserName = userName, IsActive = isActive });
        return this;
    }

    public FakeLookupRepository SeedRole(int pkid, string roleId, string roleName)
    {
        _roles.Add(new AppRoleLookup { Pkid = pkid, RoleId = roleId, RoleName = roleName });
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

    /// <summary>Title is stored padded, as nchar(100) would hand it back before RTRIM.</summary>
    public FakeLookupRepository SeedCertification(int pkid, string title, string partnerName, int partnerDisplayOrder, short partnerPkid)
    {
        _certifications.Add((new CertificationLookup { Pkid = pkid, Title = title.TrimEnd(), PartnerName = partnerName }, partnerDisplayOrder, partnerPkid));
        return this;
    }

    public FakeLookupRepository SeedJobCategory(short pkid, string description)
    {
        _jobCategories.Add(new JobCategoryLookup { Pkid = pkid, Description = description });
        return this;
    }

    public FakeLookupRepository SeedCourse(int pkid, string courseId, string title)
    {
        _courses.Add(new CourseLookup { Pkid = pkid, CourseId = courseId, Title = title });
        return this;
    }

    public FakeLookupRepository SeedTrainingCenter(short pkid, string name, string appKey, int displayOrder)
    {
        _trainingCenters.Add((new TrainingCenterLookup { Pkid = pkid, Name = name, AppKey = appKey }, displayOrder));
        return this;
    }

    public FakeLookupRepository SeedPromotion2(int pkid, string promoCode, string topic, string description, string scheduleOn)
    {
        _promotion2s.Add((
            new Promotion2Lookup { Pkid = pkid, PromoCode = promoCode, Topic = topic, Description = description },
            DateOnly.Parse(scheduleOn)));
        return this;
    }

    public Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppUserLookup>>(
            _users.OrderBy(u => u.UserName, StringComparer.Ordinal).ToList());

    /// <summary>Ordered by RoleId (the clustered PK), matching the SQL.</summary>
    public Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppRoleLookup>>(
            _roles.OrderBy(r => r.RoleId, StringComparer.OrdinalIgnoreCase).ToList());

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

    /// <summary>Ordered by partner (DisplayOrder, Name, pkid) then Title, matching the SQL.</summary>
    public Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<CertificationLookup>>(
            _certifications
                .OrderBy(c => c.PartnerDisplayOrder)
                .ThenBy(c => c.Lookup.PartnerName, StringComparer.Ordinal)
                .ThenBy(c => c.PartnerPkid)
                .ThenBy(c => c.Lookup.Title, StringComparer.Ordinal)
                .Select(c => c.Lookup)
                .ToList());

    /// <summary>Ordered by pkid — JobCategory has no DisplayOrder column.</summary>
    public Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<JobCategoryLookup>>(
            _jobCategories.OrderBy(j => j.Pkid).ToList());

    /// <summary>Ordered by CourseId, which is unique.</summary>
    public Task<IEnumerable<CourseLookup>> GetCoursesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<CourseLookup>>(
            _courses.OrderBy(c => c.CourseId, StringComparer.OrdinalIgnoreCase).ToList());

    /// <summary>Ordered by DisplayOrder then pkid, matching the SQL.</summary>
    public Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TrainingCenterLookup>>(
            _trainingCenters
                .OrderBy(t => t.DisplayOrder)
                .ThenBy(t => t.Lookup.Pkid)
                .Select(t => t.Lookup)
                .ToList());

    /// <summary>
    /// Contains-match on PromoCode, case-insensitive (the column collation is CI), newest
    /// ScheduleOn first, capped at <see cref="ILookupRepository.Promotion2LookupLimit"/> — matching the SQL.
    /// </summary>
    public Task<IEnumerable<Promotion2Lookup>> GetPromotion2sAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        IEnumerable<(Promotion2Lookup Lookup, DateOnly ScheduleOn)> results = _promotion2s;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            results = results.Where(p => p.Lookup.PromoCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IEnumerable<Promotion2Lookup>>(
            results
                .OrderByDescending(p => p.ScheduleOn)
                .ThenBy(p => p.Lookup.PromoCode, StringComparer.OrdinalIgnoreCase)
                .Take(ILookupRepository.Promotion2LookupLimit)
                .Select(p => p.Lookup)
                .ToList());
    }
}
