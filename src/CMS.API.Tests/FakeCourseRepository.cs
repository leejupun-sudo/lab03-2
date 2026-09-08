using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="ICourseRepository"/> that mirrors the SQL semantics of
/// <c>CourseRepository</c>: keyword LIKE across the five identifying columns, exact FK
/// matches, inclusive date ranges, tri-state CanRepeat, <c>CourseId ASC</c> ordering,
/// IDENTITY pkid assignment, case-insensitive CourseId uniqueness, CourseId frozen on
/// update, junction ids returned only from GetById, copy cloning both junction sets, the
/// four usage counts and the delete guard.
/// </summary>
public class FakeCourseRepository : ICourseRepository
{
    private readonly List<Course> _courses = [];
    private readonly Dictionary<int, List<int>> _certifications = [];
    private readonly Dictionary<int, List<short>> _jobCategories = [];
    private int _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakeCourseRepository Seed(
        string courseId,
        string title,
        short partnerPkid = 1,
        string partnerName = "Microsoft",
        short? courseGroupPkid = 10,
        string? courseGroupDescription = "Azure系列課程",
        byte publishStatusPkid = 2,
        string publishStatusDescription = "上架中",
        DateOnly? scheduleOn = null,
        DateOnly? scheduleOff = null,
        int displayOrder = 1,
        short hour = 21,
        decimal listPrice = 30000,
        decimal learningCredit = 9.5m,
        bool canRepeat = false,
        string? friendlyUrl = null,
        string? outline = null,
        int faqCount = 0,
        int relatedLinkCount = 0,
        int hotCourseCount = 0,
        int recommCount = 0,
        int[]? certificationPkids = null,
        short[]? jobCategoryPkids = null)
    {
        var pkid = _nextPkid++;
        _courses.Add(new Course
        {
            Pkid = pkid,
            Title = title,
            OfficialTitle = null,
            CourseId = courseId,
            ProdCourseId = courseId + "P",
            FriendlyUrl = friendlyUrl ?? title,
            DisplayOrder = displayOrder,
            PartnerPkid = partnerPkid,
            PartnerName = partnerName,
            CourseGroupPkid = courseGroupPkid,
            CourseGroupDescription = courseGroupPkid is null ? null : courseGroupDescription,
            PublishStatusPkid = publishStatusPkid,
            PublishStatusDescription = publishStatusDescription,
            ScheduleOn = scheduleOn ?? new DateOnly(2024, 1, 1),
            ScheduleOff = scheduleOff ?? new DateOnly(2034, 1, 1),
            Hour = hour,
            ListPrice = listPrice,
            LearningCredit = learningCredit,
            Outline = outline,
            CanRepeat = canRepeat,
            FaqCount = faqCount,
            RelatedLinkCount = relatedLinkCount,
            HotCourseCount = hotCourseCount,
            RecommCount = recommCount
        });
        _certifications[pkid] = [.. certificationPkids ?? []];
        _jobCategories[pkid] = [.. jobCategoryPkids ?? []];
        return this;
    }

    public Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Course>>(Ordered(_courses).Select(ProjectRow).ToList());

    public Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Course> results = _courses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(c =>
                Contains(c.Title, keyword)
                || Contains(c.OfficialTitle, keyword)
                || Contains(c.CourseId, keyword)
                || Contains(c.ProdCourseId, keyword)
                || Contains(c.FriendlyUrl, keyword));
        }

        if (query.PartnerPkid is not null)
        {
            results = results.Where(c => c.PartnerPkid == query.PartnerPkid);
        }

        if (query.CourseGroupPkid is not null)
        {
            results = results.Where(c => c.CourseGroupPkid == query.CourseGroupPkid);
        }

        if (query.PublishStatusPkid is not null)
        {
            results = results.Where(c => c.PublishStatusPkid == query.PublishStatusPkid);
        }

        if (query.ScheduleOnFrom is not null)
        {
            results = results.Where(c => c.ScheduleOn >= query.ScheduleOnFrom);
        }

        if (query.ScheduleOnTo is not null)
        {
            results = results.Where(c => c.ScheduleOn <= query.ScheduleOnTo);
        }

        if (query.ScheduleOffFrom is not null)
        {
            results = results.Where(c => c.ScheduleOff >= query.ScheduleOffFrom);
        }

        if (query.ScheduleOffTo is not null)
        {
            results = results.Where(c => c.ScheduleOff <= query.ScheduleOffTo);
        }

        if (query.CanRepeat is not null)
        {
            results = results.Where(c => c.CanRepeat == query.CanRepeat);
        }

        return Task.FromResult<IEnumerable<Course>>(Ordered(results).Select(ProjectRow).ToList());
    }

    public Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == pkid);
        if (course is null)
        {
            return Task.FromResult<Course?>(null);
        }

        // Junction ids ride along only on the single-row read, ordered like the SQL.
        var projected = ProjectRow(course);
        projected.CertificationPkids = _certifications[pkid].Order().ToList();
        projected.JobCategoryPkids = _jobCategories[pkid].Order().ToList();
        return Task.FromResult<Course?>(projected);
    }

    public Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY — assigned here, never taken from the request.
        var course = new Course { Pkid = _nextPkid++, CourseId = request.CourseId.Trim() };
        ApplyWritable(course, request);
        _courses.Add(course);
        _certifications[course.Pkid] = request.CertificationPkids.Distinct().ToList();
        _jobCategories[course.Pkid] = request.JobCategoryPkids.Distinct().ToList();
        return Task.FromResult(course.Pkid);
    }

    public Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var course = _courses.SingleOrDefault(c => c.Pkid == request.Pkid);
        if (course is null)
        {
            return Task.FromResult(false);
        }

        // CourseId is never written — CourseRecomm keys on its value.
        ApplyWritable(course, request);
        _certifications[course.Pkid] = request.CertificationPkids.Distinct().ToList();
        _jobCategories[course.Pkid] = request.JobCategoryPkids.Distinct().ToList();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == pkid);
        if (course is null)
        {
            return Task.FromResult(false);
        }

        // Both junctions cascade in SQL Server.
        _courses.Remove(course);
        _certifications.Remove(pkid);
        _jobCategories.Remove(pkid);
        return Task.FromResult(true);
    }

    public Task<int?> CopyAsync(int sourcePkid, string newCourseId, CancellationToken cancellationToken = default)
    {
        var source = _courses.SingleOrDefault(c => c.Pkid == sourcePkid);
        if (source is null)
        {
            return Task.FromResult<int?>(null);
        }

        var copy = ProjectRow(source);
        copy.Pkid = _nextPkid++;
        copy.CourseId = newCourseId;
        // A fresh row is referenced by nothing yet.
        copy.FaqCount = 0;
        copy.RelatedLinkCount = 0;
        copy.HotCourseCount = 0;
        copy.RecommCount = 0;
        _courses.Add(copy);
        _certifications[copy.Pkid] = [.. _certifications[sourcePkid]];
        _jobCategories[copy.Pkid] = [.. _jobCategories[sourcePkid]];
        return Task.FromResult<int?>(copy.Pkid);
    }

    /// <summary>Case-insensitive, matching the column's CI collation and its unique index.</summary>
    public Task<bool> CourseIdExistsAsync(string courseId, int? excludePkid = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_courses.Any(c =>
            string.Equals(c.CourseId, courseId.Trim(), StringComparison.OrdinalIgnoreCase)
            && (excludePkid is null || c.Pkid != excludePkid)));

    public Task<bool> IsInUseAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == pkid);
        return Task.FromResult(course is not null && (
            course.FaqCount > 0
            || course.RelatedLinkCount > 0
            || course.HotCourseCount > 0
            // CourseRecomm declares no FK, but the reference is real — the guard must block on it.
            || course.RecommCount > 0));
    }

    private static void ApplyWritable(Course course, CourseRequest request)
    {
        course.Title = request.Title.Trim();
        course.OfficialTitle = NullIfBlank(request.OfficialTitle);
        course.ProdCourseId = request.ProdCourseId.Trim();
        course.FriendlyUrl = request.FriendlyUrl.Trim();
        course.DisplayOrder = request.DisplayOrder;
        course.PartnerPkid = request.PartnerPkid;
        course.PartnerName = "Partner#" + request.PartnerPkid;
        course.CourseGroupPkid = request.CourseGroupPkid;
        course.CourseGroupDescription = request.CourseGroupPkid is null ? null : "Group#" + request.CourseGroupPkid;
        course.PublishStatusPkid = request.PublishStatusPkid;
        course.PublishStatusDescription = "Status#" + request.PublishStatusPkid;
        course.ScheduleOn = request.ScheduleOn!.Value;
        course.ScheduleOff = request.ScheduleOff!.Value;
        course.Hour = request.Hour;
        course.ListPrice = request.ListPrice;
        course.LearningCredit = request.LearningCredit;
        course.Material = NullIfBlank(request.Material);
        course.Objective = NullIfBlank(request.Objective);
        course.Target = NullIfBlank(request.Target);
        course.Prerequisites = NullIfBlank(request.Prerequisites);
        course.Outline = NullIfBlank(request.Outline);
        course.TowardCertOrExam = NullIfBlank(request.TowardCertOrExam);
        course.Note = NullIfBlank(request.Note);
        course.OtherInfo = NullIfBlank(request.OtherInfo);
        course.CanRepeat = request.CanRepeat;
    }

    private static bool Contains(string? value, string keyword) =>
        value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<Course> Ordered(IEnumerable<Course> courses) =>
        courses.OrderBy(c => c.CourseId, StringComparer.OrdinalIgnoreCase);

    /// <summary>A list/query row: every column plus the counts, but no junction ids.</summary>
    private static Course ProjectRow(Course c) => new()
    {
        Pkid = c.Pkid,
        Title = c.Title,
        OfficialTitle = c.OfficialTitle,
        CourseId = c.CourseId,
        ProdCourseId = c.ProdCourseId,
        FriendlyUrl = c.FriendlyUrl,
        DisplayOrder = c.DisplayOrder,
        PartnerPkid = c.PartnerPkid,
        PartnerName = c.PartnerName,
        CourseGroupPkid = c.CourseGroupPkid,
        CourseGroupDescription = c.CourseGroupDescription,
        PublishStatusPkid = c.PublishStatusPkid,
        PublishStatusDescription = c.PublishStatusDescription,
        ScheduleOn = c.ScheduleOn,
        ScheduleOff = c.ScheduleOff,
        Hour = c.Hour,
        ListPrice = c.ListPrice,
        LearningCredit = c.LearningCredit,
        Material = c.Material,
        Objective = c.Objective,
        Target = c.Target,
        Prerequisites = c.Prerequisites,
        Outline = c.Outline,
        TowardCertOrExam = c.TowardCertOrExam,
        Note = c.Note,
        OtherInfo = c.OtherInfo,
        CanRepeat = c.CanRepeat,
        FaqCount = c.FaqCount,
        RelatedLinkCount = c.RelatedLinkCount,
        HotCourseCount = c.HotCourseCount,
        RecommCount = c.RecommCount
    };
}
