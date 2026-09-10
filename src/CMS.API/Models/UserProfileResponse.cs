namespace CMS.API.Models;

/// <summary>
/// 個人資料 — <c>PUT /api/auth/profile</c> 成功後回傳的已儲存值.
/// <para>
/// 只有兩個屬性, 與 <see cref="LoginResponse"/> 同樣的收斂原則: 沒有 <c>PasswordHash</c>,
/// 也沒有 access token — 改名不重新簽發 token (見 <c>spec/auth/MyProfile.md</c>).
/// </para>
/// </summary>
public class UserProfileResponse
{
    /// <summary>帳號 — 取自 JWT, 不可變更.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>已儲存的姓名 (已 trim).</summary>
    public string UserName { get; set; } = string.Empty;
}
