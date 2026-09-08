using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>複製課程的請求 — 只需要新的簡介代碼, 其餘欄位與兩組關聯原樣複製.</summary>
public class CourseCopyRequest
{
    /// <summary>新課程的簡介代碼 — 需唯一, 重複回 409.</summary>
    [Required(ErrorMessage = "新簡介代碼為必填")]
    [MaxLength(50)]
    public string NewCourseId { get; set; } = string.Empty;
}
