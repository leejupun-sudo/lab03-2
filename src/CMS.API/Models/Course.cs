namespace CMS.API.Models;

/// <summary>課程 Course — 訓練課程主資料.</summary>
public class Course
{
    /// <summary>主代碼.</summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱.</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>
    /// 簡介代碼 — 唯一 (線上有 IX_Course_UniqueCourseId, DDL 未宣告) 且建立後不可變更:
    /// CourseRecomm 以此值 (非 pkid) 參照課程, 改名會讓推薦資料失聯.
    /// </summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼 — 不唯一 (1084 筆 / 1000 相異).</summary>
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址 — 不唯一; 線上最長值剛好 100/100.</summary>
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序 — 廠商內的排序值, 跨廠商無意義.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>原廠 Partner_pkid.</summary>
    public short PartnerPkid { get; set; }

    /// <summary>原廠名稱 — JOIN Partner.Name.</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>課程群組 CourseGroup_pkid — 可為 null (線上 0 筆為 null).</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>課程群組名稱 — LEFT JOIN CourseGroup.Description.</summary>
    public string? CourseGroupDescription { get; set; }

    /// <summary>上架狀態 PublishStatus_pkid.</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架狀態名稱 — JOIN PublishStatus.Description.</summary>
    public string PublishStatusDescription { get; set; } = string.Empty;

    /// <summary>上架日期.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期.</summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數.</summary>
    public short Hour { get; set; }

    /// <summary>定價 — decimal(9,0).</summary>
    public decimal ListPrice { get; set; }

    /// <summary>點數 — decimal(9,1), 線上 387 筆帶小數.</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>教材.</summary>
    public string? Material { get; set; }

    /// <summary>課程目標.</summary>
    public string? Objective { get; set; }

    /// <summary>適合對象.</summary>
    public string? Target { get; set; }

    /// <summary>先備知識.</summary>
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 — nvarchar(max); 線上 112 筆含 HTML 標籤, 前端以純文字呈現.</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 — nvarchar(max).</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註.</summary>
    public string? Note { get; set; }

    /// <summary>其他資訊 — 線上 1084 筆全為 null.</summary>
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽.</summary>
    public bool CanRepeat { get; set; }

    /// <summary>對應課程問答數 — CourseFAQ 參照筆數 (FK, NO_ACTION).</summary>
    public int FaqCount { get; set; }

    /// <summary>對應相關連結數 — CourseRelatedLink 參照筆數 (FK, NO_ACTION).</summary>
    public int RelatedLinkCount { get; set; }

    /// <summary>對應熱門課程數 — HotCourse 參照筆數 (FK, NO_ACTION).</summary>
    public int HotCourseCount { get; set; }

    /// <summary>
    /// 對應推薦課程數 — CourseRecomm 以 CourseId / RecommCourseId 參照的筆數.
    /// <para>
    /// CourseRecomm 沒有 FOREIGN KEY, 以 varchar CourseId 值參照; 線上 3118 筆中已有 895 筆孤兒.
    /// 比照 Seminar → Partner 的先例, 仍納入刪除保護.
    /// </para>
    /// </summary>
    public int RecommCount { get; set; }

    /// <summary>對應認證 pkid 清單 — 只在 GetById 填入.</summary>
    public List<int> CertificationPkids { get; set; } = [];

    /// <summary>職務類別 pkid 清單 — 只在 GetById 填入.</summary>
    public List<short> JobCategoryPkids { get; set; } = [];
}
