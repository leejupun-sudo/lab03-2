using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>合作廠商 Partner CRUD.</summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController : ControllerBase
{
    private readonly IPartnerRepository _repository;

    public PartnersController(IPartnerRepository repository) => _repository = repository;

    /// <summary>取得所有合作廠商.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetAll(CancellationToken cancellationToken)
    {
        var partners = await _repository.GetAllAsync(cancellationToken);
        return Ok(partners);
    }

    /// <summary>依查詢條件搜尋合作廠商.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> Query(
        [FromBody] PartnerQuery query,
        CancellationToken cancellationToken)
    {
        var partners = await _repository.QueryAsync(query ?? new PartnerQuery(), cancellationToken);
        return Ok(partners);
    }

    /// <summary>依主代碼取得單一合作廠商.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetById(short id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        return partner is null ? NotFound() : Ok(partner);
    }

    /// <summary>新增合作廠商 (廠商名稱允許重複; 應用代碼重複回 409).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Partner>> Create(
        [FromBody] PartnerRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.AppKeyExistsAsync(request.AppKey, null, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "應用代碼重複",
                Detail = $"應用代碼「{request.AppKey}」已存在。"
            });
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = (int)pkid }, created);
    }

    /// <summary>更新合作廠商 (主代碼由 body 帶入且不可變更).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<Partner>> Update(
        [FromBody] PartnerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid <= 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        if (await _repository.AppKeyExistsAsync(request.AppKey, request.Pkid, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "應用代碼重複",
                Detail = $"應用代碼「{request.AppKey}」已被其他廠商使用。"
            });
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var partner = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(partner);
    }

    /// <summary>
    /// 刪除合作廠商 (被 Course / Certification / PartnerCourseGroup / Promotion2 / Seminar
    /// 任一參照時回 409).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(short id, CancellationToken cancellationToken)
    {
        var partner = await _repository.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        if (await _repository.IsInUseAsync(id, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "廠商使用中",
                Detail = $"合作廠商「{partner.Name}」已被 {partner.CourseCount} 筆課程、"
                    + $"{partner.CertificationCount} 筆認證、"
                    + $"{partner.PartnerCourseGroupCount} 筆廠商課程群組、"
                    + $"{partner.Promotion2Count} 筆促銷活動與 "
                    + $"{partner.SeminarCount} 筆研討會使用，無法刪除。"
            });
        }

        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
