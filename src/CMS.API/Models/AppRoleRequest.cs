using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>角色 AppRole 寫入 DTO.</summary>
public class AppRoleRequest
{
    /// <summary>主代碼 — 新增時忽略, 更新時必填.</summary>
    public int Pkid { get; set; }

    /// <summary>角色代碼 — 新增後不可變更 (AppUserRole 外鍵參照).</summary>
    [Required(ErrorMessage = "角色代碼為必填")]
    [MaxLength(200)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱.</summary>
    [Required(ErrorMessage = "角色名稱為必填")]
    [MaxLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "權限等級必須大於或等於 0")]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>描述.</summary>
    [MaxLength(400)]
    public string? Description { get; set; }

    /// <summary>關聯使用者 (AppUserRole N-N).</summary>
    public List<string> UserIds { get; set; } = [];
}
