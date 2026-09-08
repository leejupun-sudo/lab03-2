using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppRoleLookup>> GetAppRolesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CourseLookup>> GetCoursesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 促銷活動查詢 — <paramref name="keyword"/> 以 LIKE 比對 PromoCode; 空白時不過濾.
    /// 最多回 <see cref="Promotion2LookupLimit"/> 筆, 依上架日期新到舊 — 這是給自動完成用的, 不是完整清單.
    /// </summary>
    Task<IEnumerable<Promotion2Lookup>> GetPromotion2sAsync(string? keyword, CancellationToken cancellationToken = default);

    /// <summary>促銷活動查詢的回傳上限 — 線上 1157 筆, 自動完成一次看 20 筆就夠.</summary>
    const int Promotion2LookupLimit = 20;
}
