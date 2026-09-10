namespace CMS.API.Models;

/// <summary>
/// 上稿作業 FeaturedPromoItem 查詢條件 — 畫面一次只看「一個據點、一週 (週一到週日)」.
/// </summary>
public class FeaturedPromoItemQuery
{
    /// <summary>據點 — 精確比對 TrainingCenter_pkid; null 不過濾.</summary>
    public short? TrainingCenterPkid { get; set; }

    /// <summary>
    /// 週內任一天 — 伺服器換算成該週的週一, 過濾 ScheduleOn 落在週一 (含) 到下週一 (不含) 之間.
    /// null 不過濾. 由 API 而不是前端決定「一週」的邊界, 兩端才不會各算各的.
    /// </summary>
    public DateOnly? WeekOf { get; set; }

    /// <summary>該週的週一. <see cref="DayOfWeek.Sunday"/> 是 0, 所以週日要退六天而不是進一天.</summary>
    public static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
