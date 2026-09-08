using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests;

/// <summary>
/// In-memory stand-in for <see cref="IFeaturedPromoItemRepository"/> that mirrors the SQL semantics
/// of <c>FeaturedPromoItemRepository</c>: the half-open Monday..next-Monday week filter, the
/// TrainingCenter filter, <c>ScheduleOn, TrainingCenter_pkid, Slot</c> ordering, IDENTITY pkid
/// assignment, the JOINed <c>TrainingCenterName</c> / <c>PromoCode</c> labels, the
/// (date, centre, slot) uniqueness rule and the park-then-swap slot move.
/// </summary>
public class FakeFeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private static readonly Dictionary<short, string> TrainingCenters = new()
    {
        [1] = "台北",
        [2] = "新竹",
        [3] = "台中",
        [5] = "高雄",
        [54] = "線上研討會"
    };

    private readonly List<FeaturedPromoItem> _items = [];
    private readonly Dictionary<int, string> _promoCodes = [];
    private int _nextPkid = 1;

    /// <summary>Number of times <see cref="UpdateAsync"/> was called — used to assert wiring.</summary>
    public int UpdateCallCount { get; private set; }

    /// <summary>Registers a promotion the seed rows and new rows can reference.</summary>
    public FakeFeaturedPromoItemRepository SeedPromotion(int pkid, string promoCode)
    {
        _promoCodes[pkid] = promoCode;
        return this;
    }

    public FakeFeaturedPromoItemRepository Seed(
        string scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int promotionPkid,
        string topic,
        string description)
    {
        _items.Add(new FeaturedPromoItem
        {
            Pkid = _nextPkid++,
            ScheduleOn = DateOnly.Parse(scheduleOn),
            TrainingCenterPkid = trainingCenterPkid,
            Slot = slot,
            PromotionPkid = promotionPkid,
            Topic = topic,
            Description = description
        });
        return this;
    }

    public Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<FeaturedPromoItem>>(Ordered(_items).Select(Project).ToList());

    public Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<FeaturedPromoItem> results = _items;

        if (query.TrainingCenterPkid is { } trainingCenterPkid)
        {
            results = results.Where(i => i.TrainingCenterPkid == trainingCenterPkid);
        }

        if (query.WeekOf is { } weekOf)
        {
            // Same half-open range as the SQL: >= Monday AND < next Monday.
            var weekStart = FeaturedPromoItemQuery.StartOfWeek(weekOf);
            var weekEnd = weekStart.AddDays(7);
            results = results.Where(i => i.ScheduleOn >= weekStart && i.ScheduleOn < weekEnd);
        }

        return Task.FromResult<IEnumerable<FeaturedPromoItem>>(Ordered(results).Select(Project).ToList());
    }

    public Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        return Task.FromResult(item is null ? null : Project(item));
    }

    public Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY — assigned here, never taken from the request.
        var item = new FeaturedPromoItem
        {
            Pkid = _nextPkid++,
            ScheduleOn = request.ScheduleOn!.Value,
            TrainingCenterPkid = request.TrainingCenterPkid,
            Slot = request.Slot,
            PromotionPkid = request.PromotionPkid,
            Topic = request.Topic.Trim(),
            Description = request.Description.Trim()
        };
        _items.Add(item);
        return Task.FromResult(item.Pkid);
    }

    public Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;

        var item = _items.SingleOrDefault(i => i.Pkid == request.Pkid);
        if (item is null)
        {
            return Task.FromResult(false);
        }

        item.ScheduleOn = request.ScheduleOn!.Value;
        item.TrainingCenterPkid = request.TrainingCenterPkid;
        item.Slot = request.Slot;
        item.PromotionPkid = request.PromotionPkid;
        item.Topic = request.Topic.Trim();
        item.Description = request.Description.Trim();
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        if (item is null)
        {
            return Task.FromResult(false);
        }

        _items.Remove(item);
        return Task.FromResult(true);
    }

    public Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int? excludePkid = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Any(i =>
            i.ScheduleOn == scheduleOn
            && i.TrainingCenterPkid == trainingCenterPkid
            && i.Slot == slot
            && (excludePkid is null || i.Pkid != excludePkid)));

    public Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        if (item is null)
        {
            return Task.FromResult(false);
        }

        if (item.Slot == targetSlot)
        {
            return Task.FromResult(true);
        }

        var neighbour = _items.SingleOrDefault(i =>
            i.Pkid != pkid
            && i.ScheduleOn == item.ScheduleOn
            && i.TrainingCenterPkid == item.TrainingCenterPkid
            && i.Slot == targetSlot);

        var originalSlot = item.Slot;
        item.Slot = targetSlot;
        if (neighbour is not null)
        {
            neighbour.Slot = originalSlot;
        }

        return Task.FromResult(true);
    }

    private static IEnumerable<FeaturedPromoItem> Ordered(IEnumerable<FeaturedPromoItem> items) =>
        items.OrderBy(i => i.ScheduleOn).ThenBy(i => i.TrainingCenterPkid).ThenBy(i => i.Slot);

    /// <summary>Resolves the two JOINed labels exactly as the SELECT does.</summary>
    private FeaturedPromoItem Project(FeaturedPromoItem item) => new()
    {
        Pkid = item.Pkid,
        ScheduleOn = item.ScheduleOn,
        TrainingCenterPkid = item.TrainingCenterPkid,
        TrainingCenterName = TrainingCenters.TryGetValue(item.TrainingCenterPkid, out var name) ? name : string.Empty,
        Slot = item.Slot,
        PromotionPkid = item.PromotionPkid,
        PromoCode = _promoCodes.TryGetValue(item.PromotionPkid, out var code) ? code : string.Empty,
        Topic = item.Topic,
        Description = item.Description
    };
}
