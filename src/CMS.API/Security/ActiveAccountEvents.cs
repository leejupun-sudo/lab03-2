using System.Security.Claims;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CMS.API.Security;

/// <summary>
/// 在簽章驗過之後, 再確認 token 背後的帳號「現在」還存在而且還啟用.
/// <para>
/// 沒有這一關的話, <c>AppUser.IsActive</c> 只在 <c>POST /api/auth/login</c> 被讀過一次
/// (<c>AuthController</c>), 之後那張 token 就靠自己的簽章與 24 小時效期活著。把使用者停用或整筆
/// 刪掉都不會讓它失效 — 畫面上的「啟用」核取方塊看起來是道門, 實際上最多要等一天才關得上。離職
/// 當下停用帳號就是會踩到這個缺口的情境, 這一關把它補上了。
/// </para>
/// <para>
/// <b>這一關只讀「帳號是否存在且啟用」, 不讀角色, 也不讀密碼。</b> 兩個後果要記住:
/// 撤銷某人的 Admin 角色<b>不會</b>立即生效 — 角色是登入當下烘進 token 的, 最多要等 24 小時;
/// 重設密碼<b>也不會</b>讓已外洩的 token 失效, 因為 token 裡沒有任何與密碼連動的印記
/// (而 <c>ResetPasswordAsync</c> 把 <c>PasswordUpdatedTime</c> 設為 NULL, 那一欄現況也當不了印記)。
/// 目前要讓一張外洩的 token 立刻作廢, 正確操作是取消勾選「啟用」, 不是重設密碼。
/// </para>
/// <para>
/// 帳號不存在與帳號停用都走 <c>context.Fail(...)</c>, 結果是 <c>401</c>
/// 而不是 <c>403</c>: 401 表示「這張憑證不能再用了」, 前端的 interceptor 收到就會清掉 session 並
/// 導回登入頁, 正是這裡想要的效果。403 的語意是「你是誰我知道, 但你不能做這件事」, 屬於角色檢查
/// 的範圍, 由 controller 上的 <c>[Authorize(Roles = "Admin")]</c> 負責。
/// </para>
/// <para>
/// 成本是每個請求多一次 <c>AppUser</c> 的叢集主鍵 seek。中介層本來就已經為了簽章金鑰讀一次
/// <c>SysConfig</c> (<see cref="SysConfigSigningKeys"/>), 兩者同一個數量級。真要省, 該做的是在
/// 那兩處一起加快取, 而不是把這一關拿掉 — 拿掉等於把停用功能還原成裝飾品。
/// </para>
/// </summary>
public sealed class ActiveAccountEvents : JwtBearerEvents
{
    /// <summary>沒有 <c>userId</c> claim 的 token 不是本 API 簽的, 一律拒絕.</summary>
    private const string MissingClaimFailure = "存取權杖不包含帳號資訊。";

    /// <summary>帳號不存在或已停用 — 對呼叫端是同一句話, 不透露是哪一種.</summary>
    private const string InactiveAccountFailure = "帳號已停用或不存在。";

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        await base.TokenValidated(context);

        var userId = context.Principal?.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Fail(MissingClaimFailure);
            return;
        }

        // IAuthRepository is scoped (a connection per call) and this runs inside the request
        // pipeline, so the request's own scope is the right one to resolve from.
        var repository = context.HttpContext.RequestServices.GetRequiredService<IAuthRepository>();

        if (!await repository.IsActiveAccountAsync(userId, context.HttpContext.RequestAborted))
        {
            context.Fail(InactiveAccountFailure);
        }
    }
}
