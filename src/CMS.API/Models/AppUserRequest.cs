using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 使用者 AppUser 寫入 DTO.
/// <para>
/// 沒有密碼欄位: 新增時後端以 SysConfig 的預設密碼產生 <c>PasswordHash</c>,
/// 更新時不動它, 只有 reset-password 端點會重設.
/// </para>
/// </summary>
public class AppUserRequest
{
    /// <summary>主代碼 — 新增時忽略, 更新時必填.</summary>
    public int Pkid { get; set; }

    /// <summary>帳號 — 新增後不可變更 (AppUserRole 外鍵參照).</summary>
    [Required(ErrorMessage = "帳號為必填")]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>姓名.</summary>
    [Required(ErrorMessage = "姓名為必填")]
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 — 預設 true (DF_AppUser_IsActive).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>關聯角色代碼 (AppUserRole N-N).</summary>
    public List<string> RoleIds { get; set; } = [];
}
