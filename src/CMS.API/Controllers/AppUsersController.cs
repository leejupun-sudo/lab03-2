using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>使用者 AppUser CRUD — 僅限 Admin.</summary>
/// <remarks>
/// <c>MapControllers().RequireAuthorization()</c> 只保證「有一張有效的 token」, 不區分持有者是誰;
/// 這裡的 <see cref="AuthorizeAttribute"/> 才是真正的角色檢查。沒有它的話, 任何登入者都能呼叫
/// <see cref="ResetPassword"/> 把別人 (包括 Admin) 的密碼重設成 SysConfig 的預設密碼 — 而每個帳號
/// 都是用那個預設密碼開出來的, 所以攻擊者本來就知道它, 接著就能直接登入那個帳號。
/// <para>
/// <b>本檔案與 <c>AppRolesController</c> 的角色檢查必須同進同退。</b>
/// <c>AppRoleRequest.UserIds</c> 會經由 <c>SyncUserRolesAsync</c> 覆寫整份 <c>AppUserRole</c>
/// 名單, 只擋這裡而不擋那裡, 等於留下一個「自己把自己加進 Admin」的入口, 一次請求就能繞過本行。
/// </para>
/// </remarks>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/app-users")]
[Produces("application/json")]
public class AppUsersController : ControllerBase
{
    private readonly IAppUserRepository _repository;
    private readonly ISysConfigRepository _sysConfigRepository;

    public AppUsersController(IAppUserRepository repository, ISysConfigRepository sysConfigRepository)
    {
        _repository = repository;
        _sysConfigRepository = sysConfigRepository;
    }

    /// <summary>取得所有使用者.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _repository.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>依查詢條件搜尋使用者.</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> Query(
        [FromBody] AppUserQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _repository.QueryAsync(query ?? new AppUserQuery(), cancellationToken);
        return Ok(users);
    }

    /// <summary>依主代碼取得單一使用者 (含關聯角色).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>新增使用者 — 密碼設為 SysConfig 的預設密碼 (SHA-256).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppUser>> Create(
        [FromBody] AppUserRequest request,
        CancellationToken cancellationToken)
    {
        if (await _repository.UserIdExistsAsync(request.UserId, null, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "帳號重複",
                Detail = $"帳號「{request.UserId}」已存在。"
            });
        }

        var passwordHash = await DefaultPasswordHashAsync(cancellationToken);

        var pkid = await _repository.CreateAsync(request, passwordHash, cancellationToken);
        var created = await _repository.GetByIdAsync(pkid, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = pkid }, created);
    }

    /// <summary>更新使用者 (主代碼由 body 帶入; 帳號與密碼不可變更).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> Update(
        [FromBody] AppUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Pkid <= 0)
        {
            ModelState.AddModelError(nameof(request.Pkid), "更新時必須提供主代碼。");
            return ValidationProblem(ModelState);
        }

        // No UserIdExistsAsync here: UPDATE never writes UserId, so it cannot collide.
        var updated = await _repository.UpdateAsync(request, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        var user = await _repository.GetByIdAsync(request.Pkid, cancellationToken);
        return Ok(user);
    }

    /// <summary>刪除使用者 (連同 AppUserRole 關聯).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>重設密碼為 SysConfig 的預設密碼. 不接受任何 body — 密碼無法經由此 API 指定.</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> ResetPassword(int id, CancellationToken cancellationToken)
    {
        var passwordHash = await DefaultPasswordHashAsync(cancellationToken);

        var reset = await _repository.ResetPasswordAsync(id, passwordHash, cancellationToken);
        if (!reset)
        {
            return NotFound();
        }

        var user = await _repository.GetByIdAsync(id, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// SHA-256 of <c>appConfig.defaultPassword</c>. A missing row or blank password is a
    /// deployment fault, not a user conflict — surfacing it as a 500 is the honest answer.
    /// </summary>
    private async Task<string> DefaultPasswordHashAsync(CancellationToken cancellationToken)
    {
        var config = await _sysConfigRepository.GetAppConfigAsync(cancellationToken);
        if (config is null || string.IsNullOrEmpty(config.DefaultPassword))
        {
            throw new InvalidOperationException(
                "SysConfig 'appConfig' is missing or has no defaultPassword; cannot set a user password.");
        }

        return PasswordHasher.Sha256Hex(config.DefaultPassword);
    }
}
