using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 合作廠商 Partner 寫入 DTO.
/// <para>
/// <c>AppKey</c> 在資料庫沒有 UNIQUE 索引, 但線上 66 筆全部相異且它是應用層的短代碼,
/// 因此新增/更新時檢查重複並回 409. <c>Name</c> 則相反 — 66 筆只有 64 個相異值
/// (「國際標準課程」出現 3 次), 所以不做重複檢查.
/// </para>
/// </summary>
public class PartnerRequest
{
    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public short Pkid { get; set; }

    /// <summary>廠商名稱 — 允許重複.</summary>
    [Required(ErrorMessage = "廠商名稱為必填")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼 — 需唯一 (應用層規則).</summary>
    [Required(ErrorMessage = "應用代碼為必填")]
    [MaxLength(10)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>廠商選單顯示名稱.</summary>
    [Required(ErrorMessage = "廠商選單顯示名稱為必填")]
    [MaxLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁顯示名稱.</summary>
    [Required(ErrorMessage = "課程明細頁顯示名稱為必填")]
    [MaxLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序 — SQL 為無約束的 int, 這裡收斂到線上實際範圍 0..9999.</summary>
    [Range(0, 9999, ErrorMessage = "顯示順序須介於 0 與 9999 之間")]
    public int DisplayOrder { get; set; }

    /// <summary>圖檔名稱 — 選填, 空白時送 null.</summary>
    [MaxLength(50)]
    public string? ImageFilename { get; set; }
}
