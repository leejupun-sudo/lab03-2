using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppRoleRepository
{
    Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken cancellationToken = default);

    Task<AppRole?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>是否已有其他資料列使用該角色代碼 (<paramref name="excludePkid"/> 為更新時排除自身).</summary>
    Task<bool> RoleIdExistsAsync(string roleId, int? excludePkid = null, CancellationToken cancellationToken = default);
}
