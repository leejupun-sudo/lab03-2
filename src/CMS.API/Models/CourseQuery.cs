namespace CMS.API.Models;

/// <summary>課程 Course 查詢條件.</summary>
public class CourseQuery
{
    /// <summary>
    /// 關鍵字 — LIKE 比對 Title / OfficialTitle / CourseId / ProdCourseId / FriendlyUrl.
    /// 八個長文字欄位 (nvarchar(4000) / max) 不納入.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>原廠 — 精確比對 Partner_pkid.</summary>
    public short? PartnerPkid { get; set; }

    /// <summary>課程群組 — 精確比對 CourseGroup_pkid.</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態 — 精確比對 PublishStatus_pkid.</summary>
    public byte? PublishStatusPkid { get; set; }

    /// <summary>上架日期起 (含).</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>上架日期迄 (含).</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>下架日期起 (含).</summary>
    public DateOnly? ScheduleOffFrom { get; set; }

    /// <summary>下架日期迄 (含).</summary>
    public DateOnly? ScheduleOffTo { get; set; }

    /// <summary>允許重聽 — null 不過濾.</summary>
    public bool? CanRepeat { get; set; }
}
