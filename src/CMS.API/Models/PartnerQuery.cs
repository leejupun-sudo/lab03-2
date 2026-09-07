namespace CMS.API.Models;

/// <summary>合作廠商 Partner 查詢條件.</summary>
public class PartnerQuery
{
    /// <summary>
    /// 關鍵字 — LIKE 比對 Name / AppKey / NameOnPartnerMenu / NameOnCourseDetailPage.
    /// ImageFilename 不納入比對, 它是資產路徑而非識別名稱.
    /// </summary>
    public string? Keyword { get; set; }
}
