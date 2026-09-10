namespace CMS.API.Models;

/// <summary>
/// 登入成功後回傳的使用者資訊.
/// <para>
/// 刻意只有三個屬性: <c>PasswordHash</c> 絕不離開後端, 角色只存在於 JWT claims 之中.
/// </para>
/// </summary>
public class LoginResponse
{
    /// <summary>帳號.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>姓名.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>已簽章的 JWT access token, 有效期 24 小時.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
