namespace CMS.API.Models;

/// <summary>
/// <c>SysConfig.configValue</c> for <c>configKey = 'appConfig'</c>, parsed from JSON.
/// <para>
/// Deliberately narrow: the live JSON also carries <c>symmetricSecurityKey</c>, which is not
/// modelled so it cannot leak into a DTO or a log by accident.
/// </para>
/// </summary>
public class AppConfig
{
    /// <summary>新帳號與重設密碼時使用的預設密碼 (明文, 只存在於後端記憶體).</summary>
    public string? DefaultPassword { get; set; }

    /// <summary>是否強制密碼原則.</summary>
    public bool EnforcePasswordPolicy { get; set; }
}
