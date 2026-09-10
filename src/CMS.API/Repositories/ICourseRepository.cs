using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    /// <summary>全部課程, CourseId ASC. 不含兩組關聯 pkid 清單.</summary>
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default);

    /// <summary>單筆課程, 含 CertificationPkids / JobCategoryPkids.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>新增課程與兩組關聯 (同一交易).</summary>
    Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    /// <summary>更新課程與兩組關聯 (同一交易). CourseId 永不寫入.</summary>
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 以新簡介代碼複製課程 (所有欄位 + 兩組關聯, 同一交易).
    /// 回傳新 pkid; 來源不存在時回傳 null.
    /// </summary>
    Task<int?> CopyAsync(int sourcePkid, string newCourseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// CourseId 是否已存在 — 對應線上的 IX_Course_UniqueCourseId (DDL 未宣告).
    /// 定序不分大小寫, 比對亦然.
    /// </summary>
    Task<bool> CourseIdExistsAsync(string courseId, int? excludePkid = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 是否已被 CourseFAQ / CourseRelatedLink / HotCourse / CourseRecomm 參照 — 刪除前須檢查.
    /// 前三者有 FK (NO_ACTION), CourseRecomm 沒有 — 見 Course.RecommCount.
    /// 兩個 junction 不算: 它們 ON DELETE CASCADE, 隨課程一起刪除.
    /// </summary>
    Task<bool> IsInUseAsync(int pkid, CancellationToken cancellationToken = default);
}
