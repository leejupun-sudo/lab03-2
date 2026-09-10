using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>登入驗證 Auth.</summary>
/// <remarks>
/// Holds the API's only <see cref="AllowAnonymousAttribute"/>, and it sits on
/// <see cref="Login"/> — <b>not on the class</b>. <c>MapControllers().RequireAuthorization()</c>
/// in <c>Program.cs</c> covers everything else, and a caller with no token has to reach the login
/// action to obtain one, so that action alone opts out.
/// <para>
/// The attribute must stay at action level: <c>[AllowAnonymous]</c> on the class wins over
/// <c>[Authorize]</c> on an action inside it (the authorization middleware skips the endpoint the
/// moment it finds <c>IAllowAnonymous</c> metadata), so a class-level opt-out would silently
/// expose <see cref="UpdateProfile"/> — an endpoint whose whole security rests on there being an
/// authenticated identity to read the account from.
/// </para>
/// </remarks>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// 401 的訊息一律相同 — 帳號不存在、密碼錯誤、帳號停用都回同一句話, 不透露是哪一項失敗.
    /// </summary>
    private const string InvalidCredentialsDetail = "帳號或密碼錯誤。";

    private readonly IAuthRepository _repository;
    private readonly IJwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthRepository repository,
        IJwtTokenService tokenService,
        ILogger<AuthController> logger)
    {
        _repository = repository;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>登入 — 驗證帳號密碼並簽發 24 小時有效的 JWT access token.</summary>
    /// <remarks>
    /// 三項檢查全部通過才算成功: 帳號存在、<c>IsActive = 1</c>、SHA-256(密碼) 等於
    /// <c>PasswordHash</c>. 任何一項失敗都回 <c>401</c> 與同一則泛用訊息.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var credential = await _repository.GetCredentialAsync(request.UserId, cancellationToken);

        // Every failure path logs the reason server-side and returns the same body to the client.
        if (credential is null)
        {
            _logger.LogInformation("Login rejected: unknown UserId.");
            return InvalidCredentials();
        }

        if (!credential.IsActive)
        {
            _logger.LogInformation("Login rejected: user {UserId} is inactive.", credential.UserId);
            return InvalidCredentials();
        }

        if (!HashMatches(credential.PasswordHash, request.Password))
        {
            _logger.LogInformation("Login rejected: wrong password for {UserId}.", credential.UserId);
            return InvalidCredentials();
        }

        var accessToken = await _tokenService.CreateAccessTokenAsync(credential, cancellationToken);

        return Ok(new LoginResponse
        {
            UserId = credential.UserId,
            UserName = credential.UserName,
            AccessToken = accessToken
        });
    }

    /// <summary>修改個人資料 — 只允許已登入的使用者更新<b>自己的</b>姓名.</summary>
    /// <remarks>
    /// 帳號一律取自 access token 的 <c>userId</c> claim, <b>絕不從 request body 讀</b>; 請求裡多帶
    /// 的 <c>userId</c> 屬性會被 JSON 繫結直接忽略, 因為 <see cref="UpdateProfileRequest"/> 上根本
    /// 沒有那個屬性. 角色同理 — 更新只寫 <c>AppUser.UserName</c> 一欄, 碰不到 <c>AppUserRole</c>.
    /// <para>
    /// Token 不會重新簽發: 其中的 <c>userName</c> claim 會停留在改名前的值, 而這無妨 — 沒有任何一端
    /// 讀它 (前端顯示的是 session storage 裡的 profile, 後端的授權只看 <c>userId</c> 與 <c>role</c>).
    /// 重簽會順帶把 24 小時效期往後延, 那是改名不該有的副作用.
    /// </para>
    /// </remarks>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);

        // The middleware guarantees an authenticated principal; a token without the claim would
        // be one this API never issued, so it gets the same 401 rather than a guessed identity.
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Profile update rejected: token carries no {Claim} claim.",
                JwtTokenService.UserIdClaimType);
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "無法識別使用者",
                Detail = "存取權杖不包含帳號資訊，請重新登入。"
            });
        }

        var userName = request.UserName.Trim();

        if (!await _repository.UpdateUserNameAsync(userId, userName, cancellationToken))
        {
            // A valid token for an account that has since been deleted.
            _logger.LogWarning("Profile update failed: {UserId} no longer exists.", userId);
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "查無使用者",
                Detail = "此帳號已不存在，請重新登入。"
            });
        }

        return Ok(new UserProfileResponse { UserId = userId, UserName = userName });
    }

    private UnauthorizedObjectResult InvalidCredentials() =>
        Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "登入失敗",
            Detail = InvalidCredentialsDetail
        });

    /// <summary>
    /// Constant-time comparison of the stored hash against SHA-256 of the supplied password.
    /// <para>
    /// Both sides are 64 lowercase hex characters (<see cref="PasswordHasher"/>); the stored value
    /// is lowercased first so a row written in uppercase hex still verifies, and
    /// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
    /// keeps the comparison free of an early-exit timing signal.
    /// </para>
    /// </summary>
    private static bool HashMatches(string storedHash, string suppliedPassword)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());
        var actual = Encoding.UTF8.GetBytes(PasswordHasher.Sha256Hex(suppliedPassword));

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
