using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ISysConfigRepository
{
    /// <summary>
    /// 讀取 <c>SysConfig</c> 中 <c>configKey = 'appConfig'</c> 的 JSON 並解析.
    /// 找不到該列時回傳 <c>null</c>.
    /// </summary>
    Task<AppConfig?> GetAppConfigAsync(CancellationToken cancellationToken = default);
}
