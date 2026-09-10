using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 上稿作業 FeaturedPromoItem 寫入 DTO.
/// <para>
/// (ScheduleOn, TrainingCenterPkid, Slot) 受資料庫 UNIQUE 索引約束, 新增／更新前以
/// <c>SlotTakenAsync</c> 檢查, 重複回 409 而不是 500.
/// </para>
/// </summary>
public class FeaturedPromoItemRequest
{
    /// <summary>時段數 — 首頁固定三格. SQL 的 tinyint 沒有 CHECK, 這是刻意的應用層收斂.</summary>
    public const byte SlotCount = 3;

    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期 — nullable 以便缺值時回 400 而非默默綁成 0001-01-01.</summary>
    [Required(ErrorMessage = "上稿日期為必填")]
    public DateOnly? ScheduleOn { get; set; }

    /// <summary>據點 — 0 視為未提供.</summary>
    [Range(1, short.MaxValue, ErrorMessage = "據點為必填")]
    public short TrainingCenterPkid { get; set; }

    /// <summary>時段 — 1..3.</summary>
    [Range(1, SlotCount, ErrorMessage = "時段須介於 1 與 3 之間")]
    public byte Slot { get; set; }

    /// <summary>促銷活動 — 0 視為未提供. 表單由促銷代碼查詢後帶入 pkid.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "促銷活動為必填")]
    public int PromotionPkid { get; set; }

    /// <summary>主題.</summary>
    [Required(ErrorMessage = "主題為必填")]
    [MaxLength(100)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明.</summary>
    [Required(ErrorMessage = "說明為必填")]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;
}
