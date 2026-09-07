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
}
