namespace CMS.API.Models;

/// <summary>
/// 登入驗證用的 <c>AppUser</c> 資料列 — <b>後端內部模型, 絕不序列化為 API 回應</b>.
/// <para>
/// 這是整個專案唯一帶有 <see cref="PasswordHash"/> 的模型; <see cref="AppUser"/> 依契約沒有該屬性.
/// 登入端點只用它做比對, 回傳的是 <see cref="LoginResponse"/>.
/// </para>
/// </summary>
public class AppUserCredential
{
    /// <summary>帳號 — 叢集主鍵.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>姓名.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — 只有 1 才能登入.</summary>
    public bool IsActive { get; set; }

    /// <summary>SHA-256 密碼雜湊 (64 位小寫十六進位).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>此帳號在 <c>AppUserRole</c> 的所有 RoleId.</summary>
    public List<string> RoleIds { get; set; } = [];
}
