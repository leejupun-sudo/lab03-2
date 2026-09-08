using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IFeaturedPromoItemRepository
{
    Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default);

    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default);

    /// <summary>
    /// (ScheduleOn, TrainingCenter_pkid, Slot) 是否已被其他資料列佔用 —
    /// 對應資料庫的 <c>IX_FeaturedPromoItem_UniqueDateLocSlot</c>, 少了這個檢查 INSERT 會 500.
    /// </summary>
    Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int? excludePkid = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把 pkid 這筆搬到同一天、同一據點的 <paramref name="targetSlot"/>. 目標時段已有資料時兩筆對調,
    /// 沒有時直接搬. 整段在一個交易內完成. 回傳 false 表示找不到這筆.
    /// </summary>
    Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default);

    // No IsInUseAsync: nothing references FeaturedPromoItem (sys.foreign_keys has no row with it
    // as the referenced table, and no other DDL carries a FeaturedPromoItem_pkid column).
}
