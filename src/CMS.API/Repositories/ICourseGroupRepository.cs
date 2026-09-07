using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseGroupRepository
{
    Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default);

    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>是否已被 Course 或 PartnerCourseGroup 參照 — 刪除前須檢查.</summary>
    Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default);

    // No *ExistsAsync: Description carries no uniqueness rule (215 rows / 213 distinct values
    // in the live database), so there is no duplicate to guard against.
}
