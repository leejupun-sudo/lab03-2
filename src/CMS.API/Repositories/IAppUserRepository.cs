using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppUserRepository
{
    Task<IEnumerable<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增. <paramref name="passwordHash"/> 由呼叫端 (controller) 從 SysConfig 預設密碼算出 —
    /// repository 本身不讀 SysConfig, 也不接受明文密碼.
    /// </summary>
    Task<int> CreateAsync(AppUserRequest request, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>更新 UserName / IsActive / 角色; 絕不寫 UserId 或 PasswordHash.</summary>
    Task<bool> UpdateAsync(AppUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>刪除使用者連同其 AppUserRole 列 (同一交易).</summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>重設密碼: 寫入 <paramref name="passwordHash"/> 並將 PasswordUpdatedTime 設為 NULL.</summary>
    Task<bool> ResetPasswordAsync(int pkid, string passwordHash, CancellationToken cancellationToken = default);

    /// <summary>是否已有其他資料列使用該帳號 (<paramref name="excludePkid"/> 為更新時排除自身). 比對不分大小寫.</summary>
    Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null, CancellationToken cancellationToken = default);
}
