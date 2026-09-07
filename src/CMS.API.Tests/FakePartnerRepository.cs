using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IPartnerRepository"/> that mirrors the SQL semantics of
/// <c>PartnerRepository</c> (keyword LIKE across the four identifying columns,
/// <c>DisplayOrder ASC, Name ASC</c> ordering, IDENTITY pkid assignment, the five usage
/// counts, and the AppKey uniqueness rule).
/// <para>
/// Two rules pull in opposite directions and both are modelled here: <c>AppKey</c> IS unique
/// (application-level rule; 66/66 distinct live) while <c>Name</c> is NOT (66 rows / 64
/// distinct), so duplicate names must be accepted.
/// </para>
/// </summary>
public class FakePartnerRepository : IPartnerRepository
{
    private readonly List<Partner> _partners = [];
    private short _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakePartnerRepository Seed(
        string name,
        string appKey,
        int displayOrder = 9999,
        string? imageFilename = null,
        int courseCount = 0,
        int certificationCount = 0,
        int partnerCourseGroupCount = 0,
        int promotion2Count = 0,
        int seminarCount = 0)
    {
        _partners.Add(new Partner
        {
            Pkid = _nextPkid++,
            Name = name,
            AppKey = appKey,
            NameOnPartnerMenu = name + "選單",
            NameOnCourseDetailPage = name,
            DisplayOrder = displayOrder,
            ImageFilename = imageFilename,
            CourseCount = courseCount,
            CertificationCount = certificationCount,
            PartnerCourseGroupCount = partnerCourseGroupCount,
            Promotion2Count = promotion2Count,
            SeminarCount = seminarCount
        });
        return this;
    }

    public Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Partner>>(Ordered(_partners).Select(Project).ToList());

    public Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<Partner> results = _partners;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(p =>
                Contains(p.Name, keyword)
                || Contains(p.AppKey, keyword)
                || Contains(p.NameOnPartnerMenu, keyword)
                || Contains(p.NameOnCourseDetailPage, keyword));
        }

        return Task.FromResult<IEnumerable<Partner>>(Ordered(results).Select(Project).ToList());
    }

    public Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == pkid);
        return Task.FromResult(partner is null ? null : Project(partner));
    }

    public Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY — assigned here, never taken from the request.
        var partner = new Partner
        {
            Pkid = _nextPkid++,
            Name = request.Name,
            AppKey = request.AppKey,
            NameOnPartnerMenu = request.NameOnPartnerMenu,
            NameOnCourseDetailPage = request.NameOnCourseDetailPage,
            DisplayOrder = request.DisplayOrder,
            ImageFilename = NullIfBlank(request.ImageFilename)
        };
        _partners.Add(partner);
        return Task.FromResult(partner.Pkid);
    }

    public Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var partner = _partners.SingleOrDefault(p => p.Pkid == request.Pkid);
        if (partner is null)
        {
            return Task.FromResult(false);
        }

        // pkid is never written — it is an FK target for five tables.
        partner.Name = request.Name;
        partner.AppKey = request.AppKey;
        partner.NameOnPartnerMenu = request.NameOnPartnerMenu;
        partner.NameOnCourseDetailPage = request.NameOnCourseDetailPage;
        partner.DisplayOrder = request.DisplayOrder;
        partner.ImageFilename = NullIfBlank(request.ImageFilename);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == pkid);
        if (partner is null)
        {
            return Task.FromResult(false);
        }

        _partners.Remove(partner);
        return Task.FromResult(true);
    }

    public Task<bool> AppKeyExistsAsync(string appKey, short? excludePkid = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_partners.Any(p =>
            string.Equals(p.AppKey, appKey, StringComparison.OrdinalIgnoreCase)
            && (excludePkid is null || p.Pkid != excludePkid)));

    public Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == pkid);
        return Task.FromResult(partner is not null && (
            partner.CourseCount > 0
            || partner.CertificationCount > 0
            || partner.PartnerCourseGroupCount > 0
            || partner.Promotion2Count > 0
            // Seminar declares no FK in SQL Server, but the reference is real — the guard
            // must block on it or deleting such a row silently orphans Seminar data.
            || partner.SeminarCount > 0));
    }

    private static bool Contains(string value, string keyword) =>
        value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<Partner> Ordered(IEnumerable<Partner> partners) =>
        partners.OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.Pkid);

    private static Partner Project(Partner partner) => new()
    {
        Pkid = partner.Pkid,
        Name = partner.Name,
        AppKey = partner.AppKey,
        NameOnPartnerMenu = partner.NameOnPartnerMenu,
        NameOnCourseDetailPage = partner.NameOnCourseDetailPage,
        DisplayOrder = partner.DisplayOrder,
        ImageFilename = partner.ImageFilename,
        CourseCount = partner.CourseCount,
        CertificationCount = partner.CertificationCount,
        PartnerCourseGroupCount = partner.PartnerCourseGroupCount,
        Promotion2Count = partner.Promotion2Count,
        SeminarCount = partner.SeminarCount
    };
}
