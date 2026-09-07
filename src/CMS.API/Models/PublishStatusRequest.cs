using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>發布狀態 PublishStatus 寫入 DTO.</summary>
public class PublishStatusRequest
{
    /// <summary>
    /// 主代碼 — 新增時必填 (pkid 為 tinyint 且非 IDENTITY), 更新後不可變更
    /// (Course / Promotion2 外鍵參照). 0 保留為「未提供」的哨兵值.
    /// </summary>
    [Range(1, 255, ErrorMessage = "主代碼必須介於 1 到 255")]
    public byte Pkid { get; set; }

    /// <summary>狀態名稱.</summary>
    [Required(ErrorMessage = "狀態名稱為必填")]
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿.</summary>
    public bool IsDraft { get; set; }

    /// <summary>已上架.</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架.</summary>
    public bool IsDiscontinued { get; set; }
}
