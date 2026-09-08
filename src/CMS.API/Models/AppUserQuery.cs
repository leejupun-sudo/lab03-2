namespace CMS.API.Models;

/// <summary>使用者 AppUser 查詢條件.</summary>
public class AppUserQuery
{
    /// <summary>關鍵字 — LIKE 比對 UserId / UserName.</summary>
    public string? Keyword { get; set; }

    /// <summary>啟用 — null 不篩選.</summary>
    public bool? IsActive { get; set; }

    /// <summary>角色代碼 — 篩選具有該角色 (AppUserRole) 的使用者.</summary>
    public string? RoleId { get; set; }

    /// <summary>密碼更新時間 (起, 含).</summary>
    public DateOnly? PasswordUpdatedFrom { get; set; }

    /// <summary>密碼更新時間 (迄, 含整日).</summary>
    public DateOnly? PasswordUpdatedTo { get; set; }
}
