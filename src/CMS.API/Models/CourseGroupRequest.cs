using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 課程群組 CourseGroup 寫入 DTO.
/// <para>
/// <c>Description</c> 沒有唯一性限制 — 資料庫中本來就有重複值, 因此新增/更新皆不檢查重複.
/// </para>
/// </summary>
public class CourseGroupRequest
{
    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public short Pkid { get; set; }

    /// <summary>群組名稱.</summary>
    [Required(ErrorMessage = "群組名稱為必填")]
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
}
