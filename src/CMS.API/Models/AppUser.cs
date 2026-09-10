namespace CMS.API.Models;

/// <summary>
/// 使用者 AppUser — 後台登入帳號.
/// <para>
/// 刻意沒有 <c>PasswordHash</c> 屬性: 該欄位絕不離開後端 (see <c>spec/auth/AppUser.md</c>).
/// </para>
/// </summary>
public class AppUser
{
    /// <summary>主代碼.</summary>
    public int Pkid { get; set; }

    /// <summary>帳號 — 叢集主鍵, AppUserRole 外鍵參照, 建立後不可變更.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>姓名.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用.</summary>
    public bool IsActive { get; set; }

    /// <summary>密碼更新時間 — NULL 表示仍為系統預設密碼. 唯讀.</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>角色數 — AppUserRole 關聯筆數.</summary>
    public int RoleCount { get; set; }

    /// <summary>關聯角色代碼 (只在 GET by id 時填入).</summary>
    public List<string> RoleIds { get; set; } = [];
}
