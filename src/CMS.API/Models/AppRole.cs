namespace CMS.API.Models;

/// <summary>角色 AppRole — 使用者角色.</summary>
public class AppRole
{
    /// <summary>主代碼.</summary>
    public int Pkid { get; set; }

    /// <summary>角色代碼.</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級 (數字越小權限越高).</summary>
    public int PermissionLevel { get; set; }

    /// <summary>描述.</summary>
    public string? Description { get; set; }

    /// <summary>使用者數 — AppUserRole 關聯筆數.</summary>
    public int UserCount { get; set; }

    /// <summary>關聯使用者 (只在 GET by id 時填入).</summary>
    public List<string> UserIds { get; set; } = [];
}
