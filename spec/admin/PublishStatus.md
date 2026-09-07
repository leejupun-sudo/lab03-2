# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

## Summary

`PublishStatus` is a small, closed reference table (3 rows in the live DB) describing the
lifecycle state of publishable content. It carries a `Description` label plus three mutually
descriptive `bit` flags. It has **no outbound foreign keys**; instead it is an FK *target* —
both `Course.PublishStatus_pkid` and `Promotion2.PublishStatus_pkid` reference
`PublishStatus.pkid`, so this feature must also expose a lookup endpoint.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint NOT NULL — NOT IDENTITY**; the value is supplied by the client on create |
| Foreign Keys | None |
| Required Fields | `pkid`, `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` |
| Query Filters | keyword (Description), IsDraft, IsPublished, IsDiscontinued |
| Default Sort | `pkid ASC` |

### Live data (read-only probe, 2026-09-07)

| pkid | Description | IsDraft | IsPublished | IsDiscontinued |
|------|-------------|---------|-------------|----------------|
| 1 | 草稿 | 1 | 0 | 0 |
| 2 | 上架中 | 0 | 1 | 0 |
| 3 | 已下架 | 0 | 0 | 1 |

The three flags happen to be mutually exclusive in the seeded data, but **no CHECK constraint
enforces that**. The schema is the source of truth: treat them as three independent booleans,
do not add exclusivity validation.

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼表 (Course / Promotion2 共用)

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態名稱
- IsDraft: 草稿
- IsPublished: 已上架
- IsDiscontinued: 已下架

`Description` is labelled **狀態名稱** rather than the literal 描述: it is the human-readable
name of the status and is the option label every FK dropdown renders. (Contrast `AppRole.Description`,
which is a genuine free-text 描述.)

---

## Required Fields

Every column is NOT NULL. There are no nullable columns and no computed columns.

Required (NOT NULL):
- `pkid` — tinyint, **client-supplied** (see *Primary Key* below)
- `Description` — nvarchar(50)
- `IsDraft` — bit
- `IsPublished` — bit
- `IsDiscontinued` — bit

Optional (nullable): none.

---

## Primary Key — client-supplied tinyint

This is the one non-obvious thing about this table and the main way generated code goes wrong.

- `pkid` is `tinyint NOT NULL` with **no `IDENTITY`** — unlike `AppRole.pkid`. The caller
  chooses the value on create.
- Consequences:
  - `INSERT` writes `pkid` explicitly. There is **no `SELECT CAST(SCOPE_IDENTITY() AS int)`** —
    `CreateAsync` returns `request.Pkid`.
  - Creating with an already-used `pkid` returns **409**, not 500 (per CLAUDE.md).
  - `pkid` is **immutable after creation** — `UPDATE` never writes it, and the edit form
    disables the control. Changing it would orphan `Course` / `Promotion2` rows, exactly as
    `AppRole.RoleId` would orphan `AppUserRole`.
  - C# type is `byte`. `PublishStatusRequest.Pkid` uses `[Range(1, 255)]`: `0` is reserved as
    the "not supplied" sentinel, matching the `request.Pkid <= 0` convention in
    `AppRolesController.Update`. SQL would accept `0`, but no row uses it and the sentinel is
    worth more than the one lost value — this is a deliberate narrowing, documented here.
- The PK constraint is named `PK_PublishingStatus` (note: *Publishing*, not *Publish*) — a
  naming inconsistency in the DDL. It does not affect generated code; do not "fix" it.

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns, so there is no outbound navigation.

**N/A**

---

## Primary-Foreign Links

Two tables reference `PublishStatus.pkid`:

- **Course** (`Course.PublishStatus_pkid`, FK `FK_Course_PublishStatus`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link target: `/courses?publishStatusPkid={pkid}`
  - Query param the Course list accepts: `publishStatusPkid`

- **Promotion2** (`Promotion2.PublishStatus_pkid`, FK `FK_Promotion2_PublishStatus`)
  - Column header: 對應活動
  - Button label: 查看活動 (icon: `pi pi-megaphone`)
  - Link target: `/promotion2s?publishStatusPkid={pkid}`
  - Query param the Promotion2 list accepts: `publishStatusPkid`

**Deferred:** neither the Course nor the Promotion2 feature exists yet, so `/courses` and
`/promotion2s` are dead routes today. This build therefore ships the **counts but not the
link buttons** — `courseCount` and `promotion2Count` render as plain numbers in the list and
detail pages. Add the buttons when those features land; the routes and param names above are
the contract to build against.

---

## N-N Relationships

No junction table references `PublishStatus`.

**N/A**

---

## Usage Counts and the Delete Guard

Because `PublishStatus` is an FK target with no `ON DELETE` action, deleting a row that
`Course` or `Promotion2` still references raises a SQL FK violation. Unguarded that surfaces
as a **500**, which CLAUDE.md explicitly rules out for foreseeable conflicts.

- Every SELECT carries two correlated subqueries:
  - `CourseCount` — `(SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = s.pkid)`
  - `Promotion2Count` — `(SELECT COUNT(*) FROM Promotion2 p WHERE p.PublishStatus_pkid = s.pkid)`
- `DELETE` first calls `IsInUseAsync`; if either count is non-zero the controller returns
  **409** with a `ProblemDetails` naming the blocking counts.
- Both `Course` and `Promotion2` were confirmed present in the live `CMS` database, so the
  subqueries resolve. They live in other sub-systems (`course.sql`, `promotion.sql`) — this is
  the one place the admin feature reaches across sub-system boundaries, and it is deliberate.

---

## Query Filters

- **keyword**: string
  - LIKE on `Description` only — it is the sole string column.

- **IsDraft**: bool?
  - Exact match on `IsDraft`. Tri-state: null = no filter, true = only drafts, false = only non-drafts.

- **IsPublished**: bool?
  - Exact match on `IsPublished`. Tri-state as above.

- **IsDiscontinued**: bool?
  - Exact match on `IsDiscontinued`. Tri-state as above.

No FK filters (no FK columns) and no date-range filters (no date/datetime columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** | `PublishStatusLookup[]` — `{ pkid, description, label }`, ordered by `pkid ASC` |

`label` is a computed C# property returning `Description` verbatim, mirroring the shape of
`AppUserLookup.Label` so the Angular `optionLabel="label"` binding is uniform across dropdowns.
This feature does not consume the endpoint itself — it is published for the future Course and
Promotion2 forms, both of which need it (see `spec/sample1.spec.md`).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all, `pkid ASC` |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publish-statuses/{id:int}` | Get by pkid |
| `POST` | `/api/publish-statuses` | Create — 409 if `pkid` already exists |
| `PUT` | `/api/publish-statuses` | Update (pkid from body; pkid itself never written) |
| `DELETE` | `/api/publish-statuses/{id:int}` | Delete — 409 if referenced by Course/Promotion2 |
| `GET` | `/api/lookups/publish-statuses` | Slim lookup list (on `LookupsController`) |

Route param is `{id:int}` even though the C# PK is `byte`: `:int` is the routing constraint,
and the action signature takes `byte id`, so model binding narrows it. A value above 255
fails binding and yields 400.

No auth attributes — this API has no authentication configured yet, consistent with `AppRolesController`.

### Status codes

| Situation | Response |
|-----------|----------|
| Create with duplicate `pkid` | 409 `ProblemDetails` — 主代碼重複 |
| Create/Update with blank `Description` | 400 (DataAnnotations) |
| Create with `Pkid` outside 1–255 | 400 (DataAnnotations `[Range]`) |
| Update with `Pkid` = 0 | 400 `ValidationProblem` — 更新時必須提供主代碼 |
| Update/Delete unknown `pkid` | 404 |
| Delete while referenced | 409 `ProblemDetails` — 狀態使用中 |

---

## Backend Notes

### Models

```csharp
// Models/PublishStatus.cs
public class PublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的活動數 — Promotion2 參照筆數.</summary>
    public int Promotion2Count { get; set; }
}

// Models/PublishStatusRequest.cs
public class PublishStatusRequest
{
    /// <summary>主代碼 — 新增時必填 (非 IDENTITY), 更新時不可變更.</summary>
    [Range(1, 255, ErrorMessage = "主代碼必須介於 1 到 255")]
    public byte Pkid { get; set; }

    [Required(ErrorMessage = "狀態名稱為必填")]
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// Models/PublishStatusQuery.cs
public class PublishStatusQuery
{
    /// <summary>關鍵字 — LIKE 比對 Description.</summary>
    public string? Keyword { get; set; }

    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}

// Models/LookupItem.cs — appended
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Label => Description;
}
```

`PublishStatusRequest` has no `Pkid`-excluded variant: unlike every IDENTITY table in this
repo, `Pkid` is a genuine input on create.

### SQL — SELECT

No JOINs and no multi-map — `PublishStatus` has no FK nav objects. No `nchar` columns, so no
`RTRIM()`. Column names already match the C# property names, so no aliases are needed beyond
`pkid AS Pkid`.

```sql
SELECT s.pkid AS Pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued,
       (SELECT COUNT(*) FROM Course c WHERE c.PublishStatus_pkid = s.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Promotion2 p WHERE p.PublishStatus_pkid = s.pkid) AS Promotion2Count
FROM PublishStatus s
```

`GetAllAsync` appends `ORDER BY s.pkid ASC`; `QueryAsync` appends the `WHERE` clause then the
same `ORDER BY`; `GetByIdAsync` appends `WHERE s.pkid = @Pkid`.

### SQL — INSERT

`pkid` **is** in the column list; there is no `SCOPE_IDENTITY()`.

```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

`CreateAsync` returns `request.Pkid`.

### SQL — UPDATE

`pkid` is excluded — it is the immutable FK target.

```sql
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

`UpdateAsync` returns `false` when the row does not exist (rows-affected = 0) so the controller
can answer 404.

### SQL — DELETE

```sql
DELETE FROM PublishStatus WHERE pkid = @Pkid;
```

Guarded by `IsInUseAsync` before it runs:

```sql
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course      WHERE PublishStatus_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM Promotion2  WHERE PublishStatus_pkid = @Pkid)
            THEN 1 ELSE 0 END;
```

### Repository interface

```csharp
public interface IPublishStatusRepository
{
    Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken cancellationToken = default);
    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken cancellationToken = default);
    Task<byte> CreateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>主代碼是否已被使用 (新增前檢查).</summary>
    Task<bool> PkidExistsAsync(byte pkid, CancellationToken cancellationToken = default);

    /// <summary>是否已被 Course 或 Promotion2 參照 (刪除前檢查).</summary>
    Task<bool> IsInUseAsync(byte pkid, CancellationToken cancellationToken = default);
}
```

### N-N Sync Pattern

**N/A** — no junction tables, so no transaction is needed anywhere in this repository. Every
method opens a connection, runs one or two statements, and closes.

### Special Column Notes

- No `nchar(n)` columns → no `RTRIM()`.
- No `date` / `time` columns → the `DateOnly` / `TimeOnly` handlers are irrelevant here.
- No computed columns, no column-name typos, no `_pkid` FK columns needing aliases.
- `tinyint` → `byte`. This is the first `byte` PK in the codebase; `IDbConnectionFactory`
  and Dapper handle it without a type handler.
- No `DEFAULT` constraints on this table.

### Register in Program.cs

```csharp
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
```

A missing registration compiles fine and only fails at request time — do not skip it.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/publish-statuses` | `PublishStatusList` | 發布狀態 PublishStatus |
| `/publish-statuses/new` | `PublishStatusForm` | 新增發布狀態 |
| `/publish-statuses/:id/edit` | `PublishStatusForm` | 編輯發布狀態 |
| `/publish-statuses/:id` | `PublishStatusDetail` | 發布狀態明細 |

Register lazily via `loadComponent` in `app.routes.ts`, with `/new` **before** `/:id`.

### Angular model

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  courseCount: number;
  promotion2Count: number;
}

export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusQuery {
  keyword?: string | null;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}

export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = {
  keyword: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null,
};
```

`pkid` is `number` in TS — TypeScript has no `byte`. The API rejects out-of-range values with 400.

### Service

`core/services/publish-status.service.ts`, base URL `${environment.apiBaseUrl}/publish-statuses`,
the standard six methods. `getById`/`delete` still route through `encodeURIComponent(pkid)` for
consistency with `AppRoleService`, though a numeric pkid never needs escaping.

`LookupService` gains `getPublishStatuses(): Observable<PublishStatusLookup[]>`.

### List component

Columns: 主代碼 / 狀態名稱 / 草稿 / 已上架 / 已下架 / 對應課程 / 對應活動 / 操作.

- The three `bit` columns render as `p-tag` — `是` (success) / `否` (secondary) — not raw booleans.
- 對應課程 and 對應活動 render `courseCount` / `promotion2Count` right-aligned (`cms-cell--number`).
  See *Primary-Foreign Links* — no link buttons until those features exist.
- All columns sortable; default `pkid ASC`.
- Filter drawer: 關鍵字 `input[pInputText]`, then three tri-state filters. Use
  `p-select` with explicit `[{label:'全部',value:null},{label:'是',value:true},{label:'否',value:false}]`
  options and `appendTo="body"` — a `p-checkbox` cannot express the third state.
- Row action buttons: 檢視 / 編輯 / 刪除, matching the AppRole list.

### Detail component

`cms-detail-grid` over all five columns, flags as 是/否 `p-tag`. A second card 使用狀況 shows
the two counts and, when both are zero, the note 尚未被任何課程或活動使用.

### Form component

Reactive Forms, no `forkJoin` needed — this form loads no lookups. In edit mode it loads the
single record; in new mode it loads nothing.

| Field | Control | Notes |
|-------|---------|-------|
| 主代碼 | `p-inputnumber` `[min]="1" [max]="255" [useGrouping]="false"` | Required. **Disabled in edit mode** (immutable FK target), with a hint explaining why |
| 狀態名稱 | `input[pInputText]` | Required, maxlength 50 |
| 草稿 | `p-checkbox [binary]="true"` | Defaults to `false` |
| 已上架 | `p-checkbox [binary]="true"` | Defaults to `false` |
| 已下架 | `p-checkbox [binary]="true"` | Defaults to `false` |

The three flags are independent — no cross-field validator (see *Live data* above).

On 409 from create, the error toast reads 主代碼「{pkid}」已存在。

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

On 409 the toast reads 此狀態已被課程或活動使用，無法刪除。

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `publish-status-list-filters` | Last query filter values |
| `publish-status-list-sort` | `{ sortField, sortOrder }` |
| `publish-status-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates *into* this list yet.

### Date Handling

**N/A** — no date or datetime columns.

### Sub-panels (edit mode only)

**N/A**

### Sidebar placement

Existing group **系統管理 Admin** (created by the AppRole feature), appended after 角色 AppRole:

```ts
{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }
```

Added to the `navGroups` signal in `app.ts`; `app.html` needs no change — it renders groups generically.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Mirrors the AppRole shape: `WebApplicationFactory<Program>` hosting the real pipeline with only
the repository swapped, so routing, model binding, DataAnnotations and JSON casing are exercised.

- `PublishStatusApiFactory.cs` — seeds 草稿 / 上架中 / 已下架, with `Course`/`Promotion2` usage
  counts attached to pkid 2 so the delete guard has something to block on.
- `FakePublishStatusRepository.cs` — in-memory, mirroring the SQL semantics: keyword LIKE on
  Description, tri-state bool filters, pkid uniqueness, immutable pkid on update, usage counts.
- `PublishStatusesControllerTests.cs`:
  - `GetAll` returns all rows ordered by pkid, carrying both usage counts
  - `Query` by keyword; by each bool flag; tri-state `false` matches non-flagged rows; empty
    filter returns all; no match returns empty
  - `GetById` found / 404
  - `Create` returns 201 with `Location`, persists the **client-supplied pkid**
  - `Create` with duplicate pkid → 409
  - `Create` with blank Description → 400
  - `Create` with `Pkid` = 0 → 400 (`[Range]` lower bound)
  - `Update` changes Description and flags, returns the updated row
  - `Update` never changes pkid
  - `Update` with `Pkid` = 0 → 400 and the repository is not called
  - `Update` unknown pkid → 404
  - `Delete` unused row → 204, then 404 on re-delete
  - `Delete` row referenced by Course/Promotion2 → 409 and the row survives
- `LookupsControllerTests.cs` — `GET /api/lookups/publish-statuses` returns the rows ordered
  by pkid with `label` equal to `description`.

Construct a fresh factory per test — the fake holds mutable state.

### Frontend — Karma + Jasmine

Standard providers: `provideRouter([])`, `provideNoopAnimations()`,
`providePrimeNG({ theme: { preset: Aura } })`, `MessageService`, `ConfirmationService`, plus
jasmine spies for the services. Assert against `data-testid`. Clear `sessionStorage` around
the list specs.

- `publish-status.service.spec.ts` — each method hits the right URL and verb; `query()` POSTs
  the filter body; `update()` PUTs to the collection with pkid in the body; errors surface.
- `publish-status-list.spec.ts` — loads on init; one row per record; flags render 是/否; usage
  counts render; drawer filters reach `query()` and persist; saved filters restore; clear
  resets; sort/page persist; delete confirms then reloads; 409 on delete shows the in-use toast.
- `publish-status-detail.spec.ts` — loads by route param; renders all five fields; renders the
  usage counts; not-found state.
- `publish-status-form.spec.ts` — add mode: title, pkid enabled, required-field block, creates
  with the supplied pkid, 409 toast. Edit mode: title, loads by pkid, patches every field,
  **pkid control disabled**, updates via PUT with pkid in the body, cancel returns to detail.
- `app.spec.ts` — extend to assert the 發布狀態 PublishStatus nav entry links to `/publish-statuses`.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/PublishStatus.cs` | Create |
| `src/CMS.API/Models/PublishStatusRequest.cs` | Create |
| `src/CMS.API/Models/PublishStatusQuery.cs` | Create |
| `src/CMS.API/Models/LookupItem.cs` | Modify — add `PublishStatusLookup` |
| `src/CMS.API/Repositories/IPublishStatusRepository.cs` | Create |
| `src/CMS.API/Repositories/PublishStatusRepository.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetPublishStatusesAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/PublishStatusesController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `publish-statuses` |
| `src/CMS.API/Program.cs` | Modify — register the repository |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/publish-status.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/publish-status.service.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getPublishStatuses` |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-list/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-detail/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-form/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — nav entry under 系統管理 Admin |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/PublishStatusApiFactory.cs` | Create |
| `src/CMS.API.Tests/FakePublishStatusRepository.cs` | Create |
| `src/CMS.API.Tests/PublishStatusesControllerTests.cs` | Create |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Create |
| `src/CMS.NG/.../core/services/publish-status.service.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-list/publish-status-list.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-detail/publish-status-detail.spec.ts` | Create |
| `src/CMS.NG/.../publish-status-form/publish-status-form.spec.ts` | Create |
| `src/CMS.NG/src/app/app.spec.ts` | Modify — assert the new nav entry |
