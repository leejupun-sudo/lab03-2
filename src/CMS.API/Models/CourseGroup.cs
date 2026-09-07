namespace CMS.API.Models;

/// <summary>課程群組 CourseGroup — 課程系列分類 (Course / PartnerCourseGroup 共用).</summary>
public class CourseGroup
{
    /// <summary>主代碼.</summary>
    public short Pkid { get; set; }

    /// <summary>群組名稱.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數.</summary>
    public int PartnerCourseGroupCount { get; set; }
}
