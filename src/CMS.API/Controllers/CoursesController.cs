using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>課程 Course CRUD.</summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController : ControllerBase
{
    private readonly ICourseRepository _repository;

    public CoursesController(ICourseRepository repository) => _repository = repository;

    /// <summary>取得所有課程 (不含關聯 pkid 清單).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> GetAll(CancellationToken cancellationToken)
    {
        var courses = await _repository.GetAllAsync(cancellationToken);
        return Ok(courses);
    }

    /// <summary>依查詢條件搜尋課程.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> Query(
        [FromBody] CourseQuery query,
        CancellationToken cancellationToken)
    {
        var courses = await _repository.QueryAsync(query ?? new CourseQuery(), cancellationToken);
        return Ok(courses);
    }

    /// <summary>依主代碼取得單一課程 (含對應認證與職務類別).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetById(int id, CancellationToken cancellationToken)
    {
        var course = await _repository.GetByIdAsync(id, cancellationToken);
        return course is null ? NotFound() : Ok(course);
    }

    /// <summary>新增課程 (簡介代碼重複回 409).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Course>> Create(
        [FromBody] CourseRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.CourseIdExistsAsync(request.CourseId, null, cancellationToken))
        {
            return DuplicateCourseId(request.CourseId);
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>更新課程 (主代碼由 body 帶入; 簡介代碼不可變更, body 中的值被忽略).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> Update(
        [FromBody] CourseRequest request,
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

        var course = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(course);
    }

    /// <summary>刪除課程 (被課程問答 / 相關連結 / 熱門課程 / 推薦課程任一參照時回 409).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var course = await _repository.GetByIdAsync(id, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        if (await _repository.IsInUseAsync(id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "課程使用中",
                Detail = $"課程「{course.CourseId}」已被 {course.FaqCount} 筆課程問答、"
                    + $"{course.RelatedLinkCount} 筆相關連結、"
                    + $"{course.HotCourseCount} 筆熱門課程與 "
                    + $"{course.RecommCount} 筆推薦課程使用，無法刪除。"
            });
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>以新簡介代碼複製課程 (含對應認證與職務類別).</summary>
    [HttpPost("{id:int}/copy")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Course>> Copy(
        int id,
        [FromBody] CourseCopyRequest request,
        CancellationToken cancellationToken)
    {
        var newCourseId = request.NewCourseId.Trim();
        if (newCourseId.Length == 0)
        {
            ModelState.AddModelError(nameof(request.NewCourseId), "新簡介代碼為必填。");
            return ValidationProblem(ModelState);
        }

        if (await _repository.CourseIdExistsAsync(newCourseId, null, cancellationToken))
        {
            return DuplicateCourseId(newCourseId);
        }

        var pkid = await _repository.CopyAsync(id, newCourseId, cancellationToken);
        if (pkid is null)
        {
            return NotFound();
        }

        var created = await _repository.GetByIdAsync(pkid.Value, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid.Value }, created);
    }

    private ConflictObjectResult DuplicateCourseId(string courseId) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "簡介代碼重複",
        Detail = $"簡介代碼「{courseId}」已存在。"
    });
}
