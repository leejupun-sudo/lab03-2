# Build Spec for FeaturedPromoItem

- database schema: `.\database\promotion.sql`
- customer spec and mockups: `.\custom\FeaturedPromoItem\FeaturedPromoItem.spec.md`,
  `ui-query.spec.png`, `ui-update.spec.png`, `ui-new.spec.png`

## Summary

`FeaturedPromoItem` is the 上稿作業 table: for every **day**, every **據點
(TrainingCenter)** and each of **three 時段 (Slot)**, which `Promotion2` row the home page
features, with its own headline (`Topic`) and blurb (`Description`). The customer spec
asks for a weekly grid (Monday–Sunday, one centre per tab) with inline edit, +/− to reorder
slots, and copy/paste between cells — not the standard list/detail/form triple. This is the
first feature in the repo built to a **customer UI spec** rather than the `/crud` template,
and the deviations are recorded below.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY(1,1) |
| Foreign Keys | `TrainingCenter_pkid` → `TrainingCenter.pkid` (`NO_ACTION`); `Promotion_pkid` → `Promotion2.pkid` (`NO_ACTION`) |
| Required Fields | `ScheduleOn`, `TrainingCenter_pkid`, `Slot`, `Promotion_pkid`, `Topic`, `Description` — every column |
| N-N Relationships | N/A |
| Primary-Foreign Links | **N/A — nothing references this table** |
| Query Filters | `TrainingCenterPkid` (exact), `WeekOf` (any day → Monday..Sunday of that week) |
| Default Sort | `ScheduleOn ASC, TrainingCenter_pkid ASC, Slot ASC` (the unique index) |
| Unique index | `IX_FeaturedPromoItem_UniqueDateLocSlot` on (`ScheduleOn`, `TrainingCenter_pkid`, `Slot`) — declared in the DDL this time |

### Live data (read-only probe, 2026-09-08)

| Measure | Value |
|---------|-------|
| `FeaturedPromoItem` rows | **31 715** |
| `ScheduleOn` range | 2019-07-09 – 2026-10-08 |
| Rows per `TrainingCenter_pkid` | 1 台北 7926 · 2 新竹 7924 · 3 台中 7923 · 5 高雄 7942 · **54 線上研討會 0** |
| `Slot` values | 1 (10 576) · 2 (10 573) · 3 (10 566) — nothing else, min 1 max 3 |
| Distinct `Promotion_pkid` | 226 of 1157 live promotions; **0 orphans** |
| Rows whose `Topic`/`Description` differ from the promotion's own | **31 625 of 31 715** |
| Max lengths | `Topic` 65/100, `Description` 116/300 |
| Blank `Topic` or `Description` | 4 rows (legacy; the API refuses new ones) |
| Weekday spread | 4521–4536 per weekday — every day of the week is used |
| Tables referencing `FeaturedPromoItem` | **0** (`sys.foreign_keys`, plus a DDL grep for `FeaturedPromoItem_pkid`) |
| `TrainingCenter` rows | 5, `DisplayOrder` 1..5 distinct |
| `Promotion2` rows | 1157; `PromoCode` 1157/1157 distinct, backed by `IX_Promotion2_UniquePromoCode`, collation `Chinese_Taiwan_Stroke_CI_AS` |
| Compatibility level | 100 — no `OFFSET/FETCH`, no `STRING_AGG`; `TOP (@n)` is fine |

Four of these numbers shape the code. Each gets a section.

---

## `Topic` / `Description` are the row's own text

99.7 % of live rows carry text that differs from `Promotion2.Topic` / `.Description`. They
are therefore **editable columns**, not a computed lookup. The form pre-fills them from the
chosen promotion **only when they are blank** — a re-selection never overwrites what an
editor typed. That is the safe reading of the mockup, which shows the fields as free inputs
below the PromoCode box.

## The unique index is the only 409

Nothing references this table, so there is **no delete guard** and no `IsInUseAsync` — the
first feature here without one. The one conflict is the unique triple: `SlotTakenAsync` runs
before `INSERT` and `UPDATE` (excluding the row's own pkid) and returns **409 時段重複**
instead of letting SQL Server 500. The same-slot-other-centre case is a different key and
succeeds; the tests cover both.

## Slot is a `tinyint` with no `CHECK`

The data only ever holds 1, 2, 3 and the customer spec says "Slot 1, 2, 3". The request model
narrows to `[Range(1, 3)]` (`FeaturedPromoItemRequest.SlotCount`), a deliberate application
rule like `PublishStatus.pkid`'s `[Range(1, 255)]`. **Slot 0 is reserved** as the parking
value for the swap (below); nothing in the live data or the API can put a row there.

## Moving a slot must not trip the unique index

Swapping slot 1 and slot 2 in two `UPDATE`s would put two rows on the same slot between
them and violate the index. `MoveToSlotAsync` therefore, in **one transaction**: parks the
neighbour on slot 0, moves the row to the target, then moves the neighbour to the vacated
slot. When the target slot is empty it is a single `UPDATE`. Bounds are checked in the
controller: moving up from 1 or down from 3 is **409 無法移動**, and the UI disables those
buttons anyway.

## `線上研討會` (pkid 54) is a tab with no rows

The customer spec says tabs come from `TrainingCenter`, and the live table has five rows —
including 54, which has never had an item. It gets a tab like the others and shows an empty
week. Not a bug; recorded so nobody "fixes" it by filtering to centres with data.

---

## Localization

### Chinese Table Name

- FeaturedPromoItem: 上稿作業
- Description: 首頁每日、每據點三個時段的促銷活動

### Chinese Column Names

- pkid: 主代碼
- ScheduleOn: 上稿日期
- TrainingCenter_pkid: 據點
- Slot: 時段
- Promotion_pkid: 促銷活動 (displayed as 促銷代碼 PromoCode)
- Topic: 主題
- Description: 說明

The grid labels follow the mockup's English (`PromoCode`, `Topic`, `Description`) alongside
the Chinese, because the customer spec uses those names.

---

## Required Fields

Every column is `NOT NULL`. There are no nullable, computed, `nchar`, `bit` or `DEFAULT`
columns. `ScheduleOn` is `date` → `DateOnly` via the handler in `Data/DapperTypeHandlers.cs`.

---

## Foreign Keys

- `TrainingCenter_pkid` → `TrainingCenter.pkid` (`smallint`, NOT NULL). Aliased
  `TrainingCenterPkid`; `t.Name AS TrainingCenterName` JOINed onto the row. Lookup label =
  `Name` (five distinct names, no need for AppKey). Order `DisplayOrder ASC, pkid ASC`.
- `Promotion_pkid` → `Promotion2.pkid` (`int`, NOT NULL). Aliased `PromotionPkid`;
  `p.PromoCode AS PromoCode` JOINed onto the row. Lookup label = `PromoCode` (unique).

Both labels ride on the row so the weekly grid renders with **no lookup calls** — the Course
pattern.

---

## Foreign-Primary Links

Neither `/training-centers` nor `/promotion2s` exists as a feature, so the grid shows the
labels as plain text. Contract when they land: `/training-centers/{pkid}` and
`/promotion2s/{pkid}`.

---

## Primary-Foreign Links

**N/A** — verified with `sys.foreign_keys` and a DDL grep. No counts, no guard.

---

## N-N Relationships

**N/A**

---

## Query Filters

- **TrainingCenterPkid** — exact match; drives the tab.
- **WeekOf** — any `DateOnly`; the **API** resolves it to Monday with
  `FeaturedPromoItemQuery.StartOfWeek` (`(DayOfWeek + 6) % 7` days back — Sunday is 0 and must
  go *back* six days, not forward one) and filters `ScheduleOn >= @Monday AND ScheduleOn <
  @Monday + 7`. Half-open on a `date` column, so no `DATEADD(day, 1)` dance.

The Angular side has the identical rule in `date.util.ts` `startOfWeek()`; both are unit
tested against the same Monday/Sunday cases so they cannot drift apart silently.

No keyword filter: the spec defines the page as a fixed grid, and PromoCode search lives in
the form's autocomplete.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/training-centers` | **New** | `TrainingCenterLookup[]` `{ pkid, name, appKey, label }`, `DisplayOrder ASC, pkid ASC` |
| `GET /api/lookups/promotion2s?keyword=` | **New** | `Promotion2Lookup[]` `{ pkid, promoCode, topic, description, label }` — **TOP 20**, `ScheduleOn DESC, PromoCode ASC`, `PromoCode LIKE '%kw%'` |

`promotion2s` is an **autocomplete** endpoint, not a full list: 1157 rows, the mockup shows
type-ahead on the code, and the cap (`ILookupRepository.Promotion2LookupLimit`) is applied
after ordering so the newest promotions win. It carries `topic` and `description` so the form
can pre-fill without a second call. Do not feed it to a `p-select`.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/featured-promo-items` | List all — ~31 k rows; kept for parity, the UI never calls it |
| `POST` | `/api/featured-promo-items/query` | `{ trainingCenterPkid?, weekOf? }` |
| `GET` | `/api/featured-promo-items/{id:int}` | Get by pkid |
| `POST` | `/api/featured-promo-items` | Create — 409 on a taken (date, centre, slot) |
| `PUT` | `/api/featured-promo-items` | Update (pkid from body) — 409 on a taken triple, excluding self |
| `DELETE` | `/api/featured-promo-items/{id:int}` | Delete — no guard |
| `POST` | `/api/featured-promo-items/{id:int}/move-up` | Slot − 1 (the mockup's **−**); swaps; 409 at slot 1 |
| `POST` | `/api/featured-promo-items/{id:int}/move-down` | Slot + 1 (the mockup's **+**); swaps; 409 at slot 3 |

The customer spec's wording is inverted from the arithmetic: "**+** moves Slot down (1 → 2)".
`move-down` means the *number goes up*. Both endpoints return the moved row.

### Status codes

| Situation | Response |
|-----------|----------|
| Any required field blank / missing `scheduleOn` | 400 |
| `slot` outside 1..3, `promotionPkid` 0, `trainingCenterPkid` 0 | 400 |
| `topic` > 100 / `description` > 300 chars | 400 |
| Update with `pkid` ≤ 0 | 400 — repository not called |
| Unknown pkid on GET / PUT / DELETE / move | 404 |
| Create / Update onto an occupied (date, centre, slot) | **409 時段重複** |
| Move past the first or last slot | **409 無法移動** |

No auth attributes, consistent with the rest of the API.

---

## Backend Notes

### Models

```csharp
public class FeaturedPromoItem
{
    public int Pkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public short TrainingCenterPkid { get; set; }
    public string TrainingCenterName { get; set; }   // JOIN
    public byte Slot { get; set; }
    public int PromotionPkid { get; set; }
    public string PromoCode { get; set; }            // JOIN
    public string Topic { get; set; }
    public string Description { get; set; }
}

public class FeaturedPromoItemRequest
{
    public const byte SlotCount = 3;
    public int Pkid { get; set; }
    [Required] public DateOnly? ScheduleOn { get; set; }
    [Range(1, short.MaxValue)] public short TrainingCenterPkid { get; set; }
    [Range(1, SlotCount)] public byte Slot { get; set; }
    [Range(1, int.MaxValue)] public int PromotionPkid { get; set; }
    [Required, MaxLength(100)] public string Topic { get; set; }
    [Required, MaxLength(300)] public string Description { get; set; }
}

public class FeaturedPromoItemQuery
{
    public short? TrainingCenterPkid { get; set; }
    public DateOnly? WeekOf { get; set; }
    public static DateOnly StartOfWeek(DateOnly date);
}
```

### SQL — SELECT

```sql
SELECT f.pkid AS Pkid, f.ScheduleOn,
       f.TrainingCenter_pkid AS TrainingCenterPkid, t.Name      AS TrainingCenterName,
       f.Slot,
       f.Promotion_pkid      AS PromotionPkid,      p.PromoCode AS PromoCode,
       f.Topic, f.Description
FROM FeaturedPromoItem f
JOIN TrainingCenter t ON t.pkid = f.TrainingCenter_pkid
JOIN Promotion2     p ON p.pkid = f.Promotion_pkid
-- WHERE f.TrainingCenter_pkid = @TrainingCenterPkid
--   AND f.ScheduleOn >= @WeekStart AND f.ScheduleOn < @WeekEnd
ORDER BY f.ScheduleOn ASC, f.TrainingCenter_pkid ASC, f.Slot ASC
```

Inner JOINs — both FKs are NOT NULL and 0 rows are orphaned. No `RTRIM`.

### SQL — INSERT / UPDATE

```sql
INSERT INTO FeaturedPromoItem (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
VALUES (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);

UPDATE FeaturedPromoItem
SET ScheduleOn = @ScheduleOn, TrainingCenter_pkid = @TrainingCenterPkid, Slot = @Slot,
    Promotion_pkid = @PromotionPkid, Topic = @Topic, Description = @Description
WHERE pkid = @Pkid;
```

Every column is writable: the row is not an FK target, so nothing depends on its key. `Topic`
and `Description` are trimmed before writing. Both are guarded by `SlotTakenAsync`:

```sql
SELECT COUNT(1) FROM FeaturedPromoItem f
WHERE f.ScheduleOn = @ScheduleOn AND f.TrainingCenter_pkid = @TrainingCenterPkid AND f.Slot = @Slot
  AND (@ExcludePkid IS NULL OR f.pkid <> @ExcludePkid)
```

### SQL — move (one transaction)

```sql
-- 1. locate the row and, if any, the neighbour on the target slot
-- 2. UPDATE neighbour SET Slot = 0          (park; 0 is unreachable otherwise)
-- 3. UPDATE row       SET Slot = @Target
-- 4. UPDATE neighbour SET Slot = @Original
```

### Repository interface

```csharp
Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(...);
Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, ...);
Task<FeaturedPromoItem?> GetByIdAsync(int pkid, ...);
Task<int> CreateAsync(FeaturedPromoItemRequest request, ...);
Task<bool> UpdateAsync(FeaturedPromoItemRequest request, ...);
Task<bool> DeleteAsync(int pkid, ...);
Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int? excludePkid = null, ...);
Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, ...);
```

Registered in `Program.cs`. No `RowAuditWriter` — see *Gaps* in `docs/claude/adding-a-feature.md`.

---

## Frontend Notes

### Routes — one, not four

| Path | Component | Title |
|------|-----------|-------|
| `/featured-promo-items` | `FeaturedPromoItemList` | 上稿作業 FeaturedPromoItem |

The mockups put 新增／編輯 **inline in the grid row** and there is nothing a detail page
would show that the row does not. So: no `/new`, `/:id`, `/:id/edit`. `FeaturedPromoItemForm`
is still its own standalone component (`features/featured-promo-items/featured-promo-item-form/`),
rendered by the list in place of the slot row, with signal inputs `context` (the fixed
date/centre/slot), `item` (edit mode) and `initial` (paste), and outputs `saved` /
`cancelled`. Query params `?trainingCenterPkid=&weekOf=` are honoured over the saved state.

### Angular model

`core/models/featured-promo-item.model.ts` — `FeaturedPromoItem`, `FeaturedPromoItemRequest`,
`FeaturedPromoItemQuery`, `TrainingCenterLookup`, `Promotion2Lookup`,
`FeaturedPromoSlotContext`, `FeaturedPromoClipboard` + `toClipboard()`, and
`FEATURED_PROMO_SLOT_COUNT = 3`.

### Service

`core/services/featured-promo-item.service.ts`: the standard six plus `moveUp(pkid)` and
`moveDown(pkid)`. `LookupService` gains `getTrainingCenters()` and `getPromotion2s(keyword?)`
(omits the param when blank). `date.util.ts` gains `addDays`, `startOfWeek`,
`formatMonthDay` (`3/16`) and `formatDayLabel` (`3/16 (一)`).

### List component — the weekly grid

- **Tabs**: `p-tabs` over `/api/lookups/training-centers`; label = `Name`, value = `pkid`.
  Defaults to the first tab (台北). An unknown saved pkid falls back to the first tab.
- **Week nav**: `<<` / label `3/16 – 3/22` / `>>` / 本週. Weeks start Monday; the page
  normalises any restored or incoming date with `startOfWeek()` before querying so the API
  and the grid always agree on the seven days.
- **Grid**: a plain CSS grid (not `p-table` — the grouped day headers and the inline form do
  not fit a paginated table). 7 day headers × 3 slot rows, each row: slot number, **+ −
  Edit Copy|Paste Delete**, PromoCode, Topic, Description. Empty cells show the buttons the
  mockup shows for empty rows: `+`/`−` disabled, Edit (opens a blank form), Paste (only when
  the clipboard holds something).
- **+ / −**: call `moveDown` / `moveUp` then reload. Disabled at the bounds and on empty
  cells; a 409 still surfaces as a toast if the grid was stale.
- **Copy / Paste**: the clipboard is an in-memory signal holding `{ promotionPkid, promoCode,
  topic, description }` — content, never the key. Paste opens the inline form in new mode
  pre-filled; the actual write is an ordinary `POST`.
- **Inline form**: at most one open at a time; switching tab or week closes it. Delete does
  not need the form.

### Form component

| Field | Control | Notes |
|-------|---------|-------|
| 促銷代碼 PromoCode | `p-autocomplete` | `forceSelection`, `dropdown`, server search via `getPromotion2s(query)`; binds the whole `Promotion2Lookup` so the pkid travels with the code |
| 主題 Topic | `input[pInputText]` | Required, non-blank, maxlength 100 |
| 說明 Description | `textarea[pTextarea]` | Required, non-blank, maxlength 300 |

Header shows the fixed key: `3/16 (一) 台北 第 1 格`. Selecting a promotion fills 主題／說明
**only if blank**. `required` plus `pattern(/\S/)` — `Validators.required` alone accepts
whitespace, which the API would trim to `''` and reject.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.promoCode}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `featured-promo-item-list-filters` | `{ trainingCenterPkid, weekOf }` (weekOf is the Monday) |

No `-sort` / `-page` keys: the grid has a fixed order and no paginator.

### Sidebar placement

New group **首頁 Home** (`pi pi-home`), placed first, holding
**上稿作業 FeaturedPromoItem** (`pi pi-calendar`, `/featured-promo-items`).

---

## Tests

### Backend — `src/CMS.API.Tests/`

- `FeaturedPromoItemApiFactory.cs` — seeds the week of Monday 2026-03-16 for 台北: a full
  Monday (slots 1–3, the swap cases), a Tuesday with slot 1 only (move into an empty slot),
  a Sunday 03-22 row (inside the week), rows on 03-15 and 03-23 (just outside), and a 新竹
  row on 03-16 (the centre filter).
- `FakeFeaturedPromoItemRepository.cs` — mirrors the half-open week filter, the centre
  filter, the three-key ordering, the JOINed labels, the uniqueness rule and the swap.
- `FeaturedPromoItemsControllerTests.cs` (35 tests): weekOf on a Wednesday, a Sunday, the
  Monday and the next Monday; `StartOfWeek` over all seven days; centre filter alone and
  combined; full-table ordering; date-only serialization; create 201 / 409 / 400s; update
  200 / self-no-conflict / 409 / 400 / 404; delete; move-down swap, move-up swap, move into
  empty, both boundary 409s, 404.
- `LookupsControllerTests.cs` — `training-centers` ordered by DisplayOrder with the label
  on the wire; `promotion2s` newest-first, case-insensitive contains-match, trim, no match,
  the 20-row cap after ordering, and `label`/`topic`/`description` on the wire (raw JSON —
  `Label` is computed).

### Frontend — Karma + Jasmine

- `featured-promo-item.service.spec.ts` — URL/verb per method, `move-up`/`move-down` POST
  with a null body, 409 surfaces from create and move, `toClipboard()` shape.
- `lookup.service.spec.ts` — `training-centers`; `promotion2s` with a trimmed keyword and
  without one.
- `date.util.spec.ts` — `addDays`, `startOfWeek` (all seven days + Sunday-goes-back), the
  two label formats.
- `featured-promo-item-list.spec.ts` (24 tests) — tabs, week headers and 21 rows, cell
  placement, default/saved/incoming week and centre, week nav and label, inline form open
  in edit and new mode, saved/cancelled, copy → paste visibility and pre-fill, move calls
  and disabled states, 409 toast, delete confirm → reload, query failure.
- `featured-promo-item-form.spec.ts` (14 tests) — context label, search, blank-fill on
  select without overwriting, required blocks (including whitespace), create request shape
  and `saved`, 409 toast, paste pre-fill, edit patch and PUT, cancel.
- `app.spec.ts` — 首頁 Home first, its entry first, exact href list updated.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/FeaturedPromoItem.cs`, `FeaturedPromoItemRequest.cs`, `FeaturedPromoItemQuery.cs` | Create |
| `src/CMS.API/Models/LookupItem.cs` | Modify — `TrainingCenterLookup`, `Promotion2Lookup` |
| `src/CMS.API/Repositories/IFeaturedPromoItemRepository.cs`, `FeaturedPromoItemRepository.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs`, `LookupRepository.cs` | Modify — two lookups |
| `src/CMS.API/Controllers/FeaturedPromoItemsController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — two routes |
| `src/CMS.API/Program.cs` | Modify — register the repository |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/featured-promo-item.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/featured-promo-item.service.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify |
| `src/CMS.NG/src/app/core/utils/date.util.ts` | Modify |
| `src/CMS.NG/src/app/features/featured-promo-items/featured-promo-item-list/` | Create |
| `src/CMS.NG/src/app/features/featured-promo-items/featured-promo-item-form/` | Create |
| `src/CMS.NG/src/app/app.routes.ts`, `app.ts` | Modify |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/FeaturedPromoItemApiFactory.cs`, `FakeFeaturedPromoItemRepository.cs`, `FeaturedPromoItemsControllerTests.cs` | Create |
| `src/CMS.API.Tests/FakeLookupRepository.cs`, `LookupApiFactory.cs`, `LookupsControllerTests.cs` | Modify |
| `src/CMS.NG/.../featured-promo-item.service.spec.ts`, `featured-promo-item-list.spec.ts`, `featured-promo-item-form.spec.ts` | Create |
| `src/CMS.NG/.../lookup.service.spec.ts`, `date.util.spec.ts`, `app.spec.ts` | Modify |
