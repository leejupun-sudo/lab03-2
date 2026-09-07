namespace CMS.API.Models;

/// <summary>合作廠商 Partner — 課程所屬的廠商 / 品牌.</summary>
public class Partner
{
    /// <summary>主代碼.</summary>
    public short Pkid { get; set; }

    /// <summary>廠商名稱.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼.</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>廠商選單顯示名稱.</summary>
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁顯示名稱.</summary>
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序 — 9999 表示排在最後.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>圖檔名稱 — 自由文字, 不保證帶副檔名.</summary>
    public string? ImageFilename { get; set; }

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的認證數 — Certification 參照筆數.</summary>
    public int CertificationCount { get; set; }

    /// <summary>使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數.</summary>
    public int PartnerCourseGroupCount { get; set; }

    /// <summary>使用中的促銷活動數 — Promotion2.RelatedPartner_pkid 參照筆數.</summary>
    public int Promotion2Count { get; set; }

    /// <summary>
    /// 使用中的研討會數 — Seminar.Partner_pkid 參照筆數.
    /// <para>
    /// Seminar 並未宣告 FOREIGN KEY (sys.foreign_keys 對 Seminar 回傳 0 筆), 但線上有 364 筆
    /// 資料帶著 Partner_pkid. 少了這個計數, 刪除只被 Seminar 參照的廠商會靜默產生孤兒資料.
    /// </para>
    /// </summary>
    public int SeminarCount { get; set; }
}
