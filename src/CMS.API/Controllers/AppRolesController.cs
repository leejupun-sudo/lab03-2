using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>角色 AppRole CRUD.</summary>
[ApiController]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController : ControllerBase
{
    private readonly IAppRoleRepository _repository;

    public AppRolesController(IAppRoleRepository repository) => _repository = repository;

    /// <summary>取得所有角色.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await _repository.GetAllAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>依查詢條件搜尋角色.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> Query(
        [FromBody] AppRoleQuery query,
        CancellationToken cancellationToken)
    {
        var roles = await _repository.QueryAsync(query ?? new AppRoleQuery(), cancellationToken);
        return Ok(roles);
    }

    /// <summary>依主代碼取得單一角色 (含關聯使用者).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetById(int id, CancellationToken cancellationToken)
    {
        var role = await _repository.GetByIdAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>新增角色.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.RoleIdExistsAsync(request.RoleId, null, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "角色代碼重複",
                Detail = $"角色代碼「{request.RoleId}」已存在。"
            });
        }

        var pkid = await _repository.CreateAsync(request, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>更新角色 (主代碼由 body 帶入; 角色代碼不可變更).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Update(
        [FromBody] AppRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid <= 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        if (await _repository.RoleIdExistsAsync(request.RoleId, request.Pkid, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "角色代碼重複",
                Detail = $"角色代碼「{request.RoleId}」已被其他角色使用。"
            });
        }

        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var role = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(role);
    }

    /// <summary>刪除角色 (連同 AppUserRole 關聯).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
