using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPublishStatusRepository
{
    Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default);

    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default);

    Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>主代碼是否已被使用 — pkid 非 IDENTITY, 新增前須檢查.</summary>
    Task<bool> PkidExistsAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>是否已被 Course 或 Promotion2 參照 — 刪除前須檢查.</summary>
    Task<bool> IsInUseAsync(byte pkid, CancellationToken cancellationToken = default);
}
