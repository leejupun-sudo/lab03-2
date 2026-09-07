namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus 查詢條件.</summary>
public class PublishStatusQuery
{
    /// <summary>關鍵字 — LIKE 比對 Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>草稿 — null 為不篩選.</summary>
    public bool? IsDraft { get; set; }

    /// <summary>已上架 — null 為不篩選.</summary>
    public bool? IsPublished { get; set; }

    /// <summary>已下架 — null 為不篩選.</summary>
    public bool? IsDiscontinued { get; set; }
}
