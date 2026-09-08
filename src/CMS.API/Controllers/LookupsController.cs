using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>下拉選單用的精簡清單.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _repository;

    public LookupsController(ILookupRepository repository) => _repository = repository;

    /// <summary>使用者清單 (AppUser).</summary>
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IEnumerable<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUserLookup>>> GetAppUsers(CancellationToken cancellationToken)
    {
        var users = await _repository.GetAppUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>角色清單 (AppRole) — value 為 RoleId.</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IEnumerable<AppRoleLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRoleLookup>>> GetAppRoles(CancellationToken cancellationToken)
    {
        var roles = await _repository.GetAppRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>發布狀態清單 (PublishStatus).</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatusLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatusLookup>>> GetPublishStatuses(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetPublishStatusesAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>課程群組清單 (CourseGroup).</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroupLookup>>> GetCourseGroups(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetCourseGroupsAsync(cancellationToken);
        return Ok(groups);
    }

    /// <summary>合作廠商清單 (Partner).</summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(IEnumerable<PartnerLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PartnerLookup>>> GetPartners(CancellationToken cancellationToken)
    {
        var partners = await _repository.GetPartnersAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>認證清單 (Certification, 含原廠名稱).</summary>
    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IEnumerable<CertificationLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationLookup>>> GetCertifications(CancellationToken cancellationToken)
    {
        var certifications = await _repository.GetCertificationsAsync(cancellationToken);
        return Ok(certifications);
    }

    /// <summary>職務類別清單 (JobCategory).</summary>
    [HttpGet("job-categories")]
    [ProducesResponseType(typeof(IEnumerable<JobCategoryLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<JobCategoryLookup>>> GetJobCategories(CancellationToken cancellationToken)
    {
        var categories = await _repository.GetJobCategoriesAsync(cancellationToken);
        return Ok(categories);
    }

    /// <summary>課程清單 (Course) — 約 1084 筆.</summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(IEnumerable<CourseLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseLookup>>> GetCourses(CancellationToken cancellationToken)
    {
        var courses = await _repository.GetCoursesAsync(cancellationToken);
        return Ok(courses);
    }

    /// <summary>據點清單 (TrainingCenter) — 5 筆, 依 DisplayOrder.</summary>
    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IEnumerable<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingCenterLookup>>> GetTrainingCenters(CancellationToken cancellationToken)
    {
        var centers = await _repository.GetTrainingCentersAsync(cancellationToken);
        return Ok(centers);
    }

    /// <summary>促銷活動查詢 (Promotion2) — keyword 比對促銷代碼, 最多 20 筆, 新到舊. 給自動完成用.</summary>
    [HttpGet("promotion2s")]
    [ProducesResponseType(typeof(IEnumerable<Promotion2Lookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Promotion2Lookup>>> GetPromotion2s(
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
    {
        var promotions = await _repository.GetPromotion2sAsync(keyword, cancellationToken);
        return Ok(promotions);
    }
}
