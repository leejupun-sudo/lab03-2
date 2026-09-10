namespace CMS.API.Models;

/// <summary>
/// 上稿作業 FeaturedPromoItem — 首頁每日、每據點三個時段 (Slot) 各推一檔促銷活動.
/// <para>
/// 資料庫以 <c>IX_FeaturedPromoItem_UniqueDateLocSlot</c> 約束 (ScheduleOn, TrainingCenter_pkid, Slot)
/// 唯一 — 同一天、同一據點的同一時段只能有一筆. 沒有任何資料表以 FK 參照本表, 刪除不需保護.
/// </para>
/// </summary>
public class FeaturedPromoItem
{
    /// <summary>主代碼.</summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期.</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>據點 TrainingCenter_pkid.</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>據點名稱 — JOIN TrainingCenter.Name.</summary>
    public string TrainingCenterName { get; set; } = string.Empty;

    /// <summary>時段 — 1..3; SQL 為無約束的 tinyint, 線上只出現 1、2、3.</summary>
    public byte Slot { get; set; }

    /// <summary>促銷活動 Promotion_pkid → Promotion2.pkid.</summary>
    public int PromotionPkid { get; set; }

    /// <summary>促銷代碼 — JOIN Promotion2.PromoCode; 清單以此顯示, 不用 pkid.</summary>
    public string PromoCode { get; set; } = string.Empty;

    /// <summary>
    /// 主題 — 本表自有的文字, 不是 Promotion2.Topic 的複本:
    /// 線上 31715 筆有 31625 筆與促銷活動本身的 Topic / Description 不同.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明 — 同上, 自由文字.</summary>
    public string Description { get; set; } = string.Empty;
}
