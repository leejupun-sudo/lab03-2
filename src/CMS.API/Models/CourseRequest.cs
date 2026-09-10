using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 課程 Course 寫入 DTO.
/// <para>
/// <c>CourseId</c> 新增時必填且需唯一 (資料庫有 UNIQUE 索引, 重複回 409); 更新時忽略 —
/// CourseRecomm 以其值參照課程, 建立後不可變更. <c>Title</c> / <c>ProdCourseId</c> /
/// <c>FriendlyUrl</c> 皆不唯一, 不做重複檢查.
/// </para>
/// </summary>
public class CourseRequest
{
    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱.</summary>
    [Required(ErrorMessage = "課程名稱為必填")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱 — 選填.</summary>
    [MaxLength(300)]
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼 — 唯一, 建立後不可變更 (更新時忽略).</summary>
    [Required(ErrorMessage = "簡介代碼為必填")]
    [MaxLength(50)]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼.</summary>
    [Required(ErrorMessage = "科目代碼為必填")]
    [MaxLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址 — 線上最長值剛好 100 字, MaxLength 是真實限制.</summary>
    [Required(ErrorMessage = "友善網址為必填")]
    [MaxLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序 — SQL 為無約束 int, 這裡收斂到 0..9999 (線上 0..1000).</summary>
    [Range(0, 9999, ErrorMessage = "顯示順序須介於 0 與 9999 之間")]
    public int DisplayOrder { get; set; }

    /// <summary>原廠 — 0 視為未提供.</summary>
    [Range(1, short.MaxValue, ErrorMessage = "原廠為必填")]
    public short PartnerPkid { get; set; }

    /// <summary>課程群組 — 選填.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — 0 視為未提供.</summary>
    [Range(1, byte.MaxValue, ErrorMessage = "上架狀態為必填")]
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期 — nullable 以便缺值時回 400 而非默默綁成 0001-01-01.</summary>
    [Required(ErrorMessage = "上架日期為必填")]
    public DateOnly? ScheduleOn { get; set; }

    /// <summary>下架日期 — API 不檢查 >= 上架日期 (無 CHECK; 線上 1 筆違反), 由表單擋.</summary>
    [Required(ErrorMessage = "下架日期為必填")]
    public DateOnly? ScheduleOff { get; set; }

    /// <summary>時數.</summary>
    [Range(0, short.MaxValue, ErrorMessage = "時數不可為負")]
    public short Hour { get; set; }

    /// <summary>定價 — decimal(9,0).</summary>
    [Range(0, 999_999_999, ErrorMessage = "定價須介於 0 與 999999999 之間")]
    public decimal ListPrice { get; set; }

    /// <summary>點數 — decimal(9,1).</summary>
    [Range(0, 99_999_999.9, ErrorMessage = "點數須介於 0 與 99999999.9 之間")]
    public decimal LearningCredit { get; set; }

    [MaxLength(500)]
    public string? Material { get; set; }

    [MaxLength(4000)]
    public string? Objective { get; set; }

    [MaxLength(500)]
    public string? Target { get; set; }

    [MaxLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — nvarchar(max), 無長度限制.</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 — nvarchar(max), 無長度限制.</summary>
    public string? TowardCertOrExam { get; set; }

    [MaxLength(4000)]
    public string? Note { get; set; }

    [MaxLength(4000)]
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽.</summary>
    public bool CanRepeat { get; set; }

    /// <summary>對應認證 — CourseInCertification 以 delete-then-reinsert 同步.</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>職務類別 — CourseJobCategories 以 delete-then-reinsert 同步.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
