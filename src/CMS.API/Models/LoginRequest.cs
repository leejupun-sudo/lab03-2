using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>登入請求.</summary>
public class LoginRequest
{
    /// <summary>帳號 — 對應 <c>AppUser.UserId</c>.</summary>
    [Required(ErrorMessage = "帳號為必填")]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>密碼 (明文) — 後端以 SHA-256 比對 <c>AppUser.PasswordHash</c>, 不會被儲存或記錄.</summary>
    [Required(ErrorMessage = "密碼為必填")]
    public string Password { get; set; } = string.Empty;
}
