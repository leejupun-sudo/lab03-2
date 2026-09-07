using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPartnerRepository
{
    Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default);

    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);

    Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// AppKey 是否已被其他資料列使用 — 應用層唯一性規則, 資料庫並未以 UNIQUE 索引約束.
    /// 線上 66 筆全部相異, 因此這條規則成立; 變更前請重跑 COUNT(DISTINCT) 探測.
    /// </summary>
    Task<bool> AppKeyExistsAsync(string appKey, short? excludePkid = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否已被 Course / Certification / PartnerCourseGroup / Promotion2 / Seminar 參照 — 刪除前須檢查.
    /// 前四者有 FK 約束, Seminar 沒有 — 見 SeminarCount 的說明.
    /// </summary>
    Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default);

    // No NameExistsAsync: Name carries no uniqueness rule (66 rows / 64 distinct values in
    // the live database), so guarding it would make the three duplicate rows uneditable.
}
