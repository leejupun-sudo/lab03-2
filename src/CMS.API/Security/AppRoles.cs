namespace CMS.API.Security;

/// <summary>
/// <c>AppRole.RoleId</c> 中會被程式碼引用的角色代碼.
/// <para>
/// 只列出「後端真的拿來做判斷」的那些, 不是 <c>AppRole</c> 資料表的鏡像 — 角色本身是資料, 可以由
/// 管理者自由增刪; 這裡是原始碼對其中特定幾筆的依賴。
/// </para>
/// <para>
/// <b>這個字面值必須與資料庫裡的字樣完全一致, 含大小寫。</b> SQL 端的
/// <c>Chinese_Taiwan_Stroke_CI_AS</c> collation 大小寫不敏感, 但
/// <c>[Authorize(Roles = ...)]</c> 走的是 <c>ClaimsPrincipal.IsInRole</c>, 拿 token 裡的
/// <c>role</c> claim 做<b>區分大小寫</b>的字串比對, 而該 claim 是照資料庫存的字樣原樣寫入的
/// (<c>JwtTokenService</c>)。也就是說, 若有人把角色建成 <c>ADMIN</c>, 在 SQL 眼中它與
/// <c>Admin</c> 重複 (<c>RoleIdExistsAsync</c> 會擋), 但萬一真的存成那樣, 這裡的檢查不會認它。
/// 前端的對應常數在 <c>core/services/auth.service.ts</c> 的 <c>ADMIN_ROLE</c>, 那邊是刻意
/// 大小寫不敏感比對, 因為它只決定選單顯不顯示。
/// </para>
/// </summary>
public static class AppRoles
{
    /// <summary>系統管理 — 使用者、角色、發布狀態三個維護作業的門檻.</summary>
    public const string Admin = "Admin";
}
