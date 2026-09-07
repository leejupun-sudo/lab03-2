using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>發布狀態 PublishStatus CRUD.</summary>
[ApiController]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController : ControllerBase
{
    private readonly IPublishStatusRepository _repository;

    public PublishStatusesController(IPublishStatusRepository repository) => _repository = repository;

    /// <summary>取得所有發布狀態.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetAll(CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetAllAsync(cancellationToken);
        return Ok(statuses);
    }

    /// <summary>依查詢條件搜尋發布狀態.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> Query(
        [FromBody] PublishStatusQuery query,
        CancellationToken cancellationToken)
    {
        var statuses = await _repository.QueryAsync(query ?? new PublishStatusQuery(), cancellationToken);
        return Ok(statuses);
    }

    /// <summary>依主代碼取得單一發布狀態.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetById(byte id, CancellationToken cancellationToken)
    {
        var status = await _repository.GetByIdAsync(id, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>新增發布狀態 (主代碼由呼叫端指定, pkid 非 IDENTITY).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.PkidExistsAsync(request.Pkid, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "主代碼重複",
                Detail = $"主代碼「{request.Pkid}」已存在。"
            });
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>更新發布狀態 (主代碼由 body 帶入; 主代碼本身不可變更).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> Update(
        [FromBody] PublishStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid == 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var status = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(status);
    }

    /// <summary>刪除發布狀態 (被 Course 或 Promotion2 參照時回 409).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(byte id, CancellationToken cancellationToken)
    {
        var status = await _repository.GetByIdAsync(id, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        if (await _repository.IsInUseAsync(id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "狀態使用中",
                Detail = $"發布狀態「{status.Description}」已被 {status.CourseCount} 筆課程與 "
                    + $"{status.Promotion2Count} 筆活動使用，無法刪除。"
            });
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
