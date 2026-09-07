namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus — 內容發布狀態代碼表 (Course / Promotion2 共用).</summary>
public class PublishStatus
{
    /// <summary>主代碼 — tinyint, 非 IDENTITY, 新增時由呼叫端指定.</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態名稱.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿.</summary>
    public bool IsDraft { get; set; }

    /// <summary>已上架.</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架.</summary>
    public bool IsDiscontinued { get; set; }

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的活動數 — Promotion2 參照筆數.</summary>
    public int Promotion2Count { get; set; }
}
