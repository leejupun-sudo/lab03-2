using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>課程群組 CourseGroup CRUD.</summary>
[ApiController]
[Route("api/course-groups")]
[Produces("application/json")]
public class CourseGroupsController : ControllerBase
{
    private readonly ICourseGroupRepository _repository;

    public CourseGroupsController(ICourseGroupRepository repository) => _repository = repository;

    /// <summary>取得所有課程群組.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> GetAll(CancellationToken cancellationToken)
    {
        var groups = await _repository.GetAllAsync(cancellationToken);
        return Ok(groups);
    }

    /// <summary>依查詢條件搜尋課程群組.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> Query(
        [FromBody] CourseGroupQuery query,
        CancellationToken cancellationToken)
    {
        var groups = await _repository.QueryAsync(query ?? new CourseGroupQuery(), cancellationToken);
        return Ok(groups);
    }

    /// <summary>依主代碼取得單一課程群組.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> GetById(short id, CancellationToken cancellationToken)
    {
        var group = await _repository.GetByIdAsync(id, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>新增課程群組 (群組名稱允許重複, 無 409).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseGroup>> Create(
        [FromBody] CourseGroupRequest request,
        CancellationToken cancellationToken)
    {
        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>更新課程群組 (主代碼由 body 帶入).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> Update(
        [FromBody] CourseGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid <= 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var group = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(group);
    }

    /// <summary>刪除課程群組 (被 Course 或 PartnerCourseGroup 參照時回 409).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var group = await _repository.GetByIdAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (await _repository.IsInUseAsync(id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "群組使用中",
                Detail = $"課程群組「{group.Description}」已被 {group.CourseCount} 筆課程與 "
                    + $"{group.PartnerCourseGroupCount} 筆廠商課程群組使用，無法刪除。"
            });
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
