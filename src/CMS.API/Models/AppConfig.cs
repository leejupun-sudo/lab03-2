namespace CMS.API.Models;

/// <summary>
/// <c>SysConfig.configValue</c> for <c>configKey = 'appConfig'</c>, parsed from JSON.
/// <para>
/// Every property here is a backend secret or policy switch, so this type is <b>never</b>
/// returned by a controller and never logged — the same rule that already applies to
/// <see cref="DefaultPassword"/> now covers <see cref="SymmetricSecurityKey"/>.
/// </para>
/// </summary>
public class AppConfig
{
    /// <summary>新帳號與重設密碼時使用的預設密碼 (明文, 只存在於後端記憶體).</summary>
    public string? DefaultPassword { get; set; }

    /// <summary>是否強制密碼原則.</summary>
    public bool EnforcePasswordPolicy { get; set; }

    /// <summary>
    /// JWT 簽章金鑰 — HMAC-SHA256 對稱金鑰, 只在後端簽發 access token 時使用.
    /// <para>
    /// 從資料庫讀取, 絕不寫死在程式或設定檔中 (see <c>spec/auth/Login.md</c>).
    /// </para>
    /// </summary>
    public string? SymmetricSecurityKey { get; set; }
}
