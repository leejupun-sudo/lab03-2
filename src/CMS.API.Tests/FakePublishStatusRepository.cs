using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IPublishStatusRepository"/> that mirrors the SQL semantics
/// of <c>PublishStatusRepository</c> (keyword LIKE on Description, tri-state bool filters,
/// client-supplied non-IDENTITY pkid, immutable pkid on update, Course/Promotion2 usage counts).
/// </summary>
public class FakePublishStatusRepository : IPublishStatusRepository
{
    private readonly List<PublishStatus> _statuses = [];

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    public FakePublishStatusRepository Seed(
        byte pkid,
        string description,
        bool isDraft,
        bool isPublished,
        bool isDiscontinued,
        int courseCount = 0,
        int promotion2Count = 0)
    {
        _statuses.Add(new PublishStatus
        {
            Pkid = pkid,
            Description = description,
            IsDraft = isDraft,
            IsPublished = isPublished,
            IsDiscontinued = isDiscontinued,
            CourseCount = courseCount,
            Promotion2Count = promotion2Count
        });
        return this;
    }

    public Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<PublishStatus>>(_statuses.OrderBy(s => s.Pkid).Select(Project).ToList());

    public Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<PublishStatus> results = _statuses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            results = results.Where(s => s.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsDraft.HasValue)
        {
            results = results.Where(s => s.IsDraft == query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            results = results.Where(s => s.IsPublished == query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            results = results.Where(s => s.IsDiscontinued == query.IsDiscontinued.Value);
        }

        return Task.FromResult<IEnumerable<PublishStatus>>(
            results.OrderBy(s => s.Pkid).Select(Project).ToList());
    }

    public Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == pkid);
        return Task.FromResult(status is null ? null : Project(status));
    }

    public Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is supplied by the caller — there is no IDENTITY to read back.
        _statuses.Add(new PublishStatus
        {
            Pkid = request.Pkid,
            Description = request.Description,
            IsDraft = request.IsDraft,
            IsPublished = request.IsPublished,
            IsDiscontinued = request.IsDiscontinued
        });
        return Task.FromResult(request.Pkid);
    }

    public Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var status = _statuses.SingleOrDefault(s => s.Pkid == request.Pkid);
        if (status is null)
        {
            return Task.FromResult(false);
        }

        // pkid is never written — it is the key Course and Promotion2 reference.
        status.Description = request.Description;
        status.IsDraft = request.IsDraft;
        status.IsPublished = request.IsPublished;
        status.IsDiscontinued = request.IsDiscontinued;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == pkid);
        if (status is null)
        {
            return Task.FromResult(false);
        }

        _statuses.Remove(status);
        return Task.FromResult(true);
    }

    public Task<bool> PkidExistsAsync(byte pkid, CancellationToken cancellationToken = default) =>
        Task.FromResult(_statuses.Any(s => s.Pkid == pkid));

    public Task<bool> IsInUseAsync(byte pkid, CancellationToken cancellationToken = default)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == pkid);
        return Task.FromResult(status is not null && (status.CourseCount > 0 || status.Promotion2Count > 0));
    }

    private static PublishStatus Project(PublishStatus status) => new()
    {
        Pkid = status.Pkid,
        Description = status.Description,
        IsDraft = status.IsDraft,
        IsPublished = status.IsPublished,
        IsDiscontinued = status.IsDiscontinued,
        CourseCount = status.CourseCount,
        Promotion2Count = status.Promotion2Count
    };
}
