# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

## Summary

`CourseGroup` is a flat reference table naming a course series (課程系列) — e.g.
「SharePoint系列課程」,「PMI®專案管理認證系列」. It has just two columns: an IDENTITY
`pkid` and a `Description`. It has **no outbound foreign keys**; it is purely an FK
*target*, referenced by `Course.CourseGroup_pkid` and `PartnerCourseGroup.CourseGroup_pkid`.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint** IDENTITY(1,1) |
| Foreign Keys | None |
| Required Fields | `Description` |
| N-N Relationships | **N/A — see the PartnerCourseGroup analysis below; it is not a junction** |
| Primary-Foreign Links | `Course.CourseGroup_pkid` (nullable), `PartnerCourseGroup.CourseGroup_pkid` (NOT NULL) |
| Query Filters | keyword (Description) |
| Default Sort | `Description ASC` |

### Live data (read-only probe, 2026-09-07)

| Measure | Value |
|---------|-------|
| `CourseGroup` rows | **215** |
| Distinct `Description` values | **213** — `Description` is NOT unique |
| Longest `Description` | 45 chars (column is `nvarchar(100)`) |
| `Course` rows with a group | 1084 |
| `Course` rows with `CourseGroup_pkid IS NULL` | **0** |
| `PartnerCourseGroup` rows | 146 |
| `Promotion2` rows pointing at a `PartnerCourseGroup` | **387** |

Three of these numbers change the generated code — see the next three sections.

---

## PartnerCourseGroup is NOT an N-N junction

The `/crud` heuristic is "junction tables whose name contains TABLE's name or its PK",
and `PartnerCourseGroup` matches that name pattern with exactly two FK columns
(`Partner_pkid`, `CourseGroup_pkid`). **Treating it as a junction would be wrong**, for
three independent reasons:

```sql
CREATE TABLE [dbo].[PartnerCourseGroup](
    [pkid] [int] IDENTITY(1,1) NOT NULL,   -- its own surrogate key
    [Partner_pkid] [smallint] NOT NULL,
    [CourseGroup_pkid] [smallint] NOT NULL,
    [DisplayOrder] [int] NOT NULL,          -- its own payload
    [Description] [nvarchar](100) NOT NULL, -- its own payload
    CONSTRAINT [PK_PartnerCourseGroup] PRIMARY KEY CLUSTERED ([pkid] ASC)
)
```

1. It has its **own IDENTITY `pkid`**, not a composite PK over the two FKs. A real
   junction in this schema (`AppUserRole`, `CourseInCertification`) keys on the pair.
2. It carries **its own payload** — `DisplayOrder` and a `Description` distinct from the
   CourseGroup's own.
3. **`Promotion2.RelatedPartnerCourseGroup_pkid` is an FK to `PartnerCourseGroup.pkid`**
   (`FK_Promotion2_PartnerCourseGroup`), and **387 live `Promotion2` rows** point at it.

Point 3 is decisive. The standard N-N sync pattern is delete-then-reinsert:

```sql
DELETE FROM PartnerCourseGroup WHERE CourseGroup_pkid = @Pkid;  -- would destroy pkids
-- re-INSERT ... IDENTITY assigns brand-new pkids
```

Every re-inserted row gets a **new** `pkid`, so those 387 `Promotion2` references would
be orphaned or silently repointed at the wrong row. `PartnerCourseGroup` is an entity in
its own right and gets a Primary-Foreign link, not an inline multi-select.

**N-N Relationships: N/A.**

---

## `Description` is not unique — no duplicate check

215 rows carry only 213 distinct `Description` values, and there is no `UNIQUE`
constraint on the column. So, unlike `AppRole.RoleId` and `PublishStatus.pkid`:

- **No `DescriptionExistsAsync`, and no 409 on create or update.**
- Adding one would also make the two existing duplicate rows uneditable — any save would
  409 against its own twin.

`pkid` is IDENTITY, so there is no client-supplied-key conflict to guard either. This
feature has **no 409 path on create/update at all**; the only 409 is the delete guard below.

---

## Usage counts and the delete guard

`CourseGroup` is an FK target with no `ON DELETE` action, so deleting a referenced row
raises an FK violation — a 500, which CLAUDE.md rules out for foreseeable conflicts.
Same shape as the `PublishStatus` guard.

- Every SELECT carries two correlated subqueries:
  - `CourseCount` — `(SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = g.pkid)`
  - `PartnerCourseGroupCount` — `(SELECT COUNT(*) FROM PartnerCourseGroup p WHERE p.CourseGroup_pkid = g.pkid)`
- `DELETE` calls `IsInUseAsync` first; non-zero on either count returns **409** with a
  `ProblemDetails` naming both counts.

Note `Course.CourseGroup_pkid` is **nullable** but zero live rows use the null — the
column is nullable in schema only. The guard still blocks on it; the API never nulls out
a course's group to force a delete through.

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程系列分類 (Course / PartnerCourseGroup 共用)

### Chinese Column Names

- pkid: 主代碼
- Description: 群組名稱

`Description` is labelled **群組名稱** rather than the literal 描述: it is the name of the
series and the option label every FK dropdown renders, exactly as with
`PublishStatus.Description` (狀態名稱). Contrast `AppRole.Description`, genuine free text.

---

## Required Fields

Required (NOT NULL, excluding the IDENTITY PK):
- `Description` — nvarchar(100)

Optional (nullable): none.

There are no computed columns, no `nchar` columns, no date/time columns, and no defaults.

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns, so there is no outbound navigation.

**N/A**

---

## Primary-Foreign Links

Two tables reference `CourseGroup.pkid`:

- **Course** (`Course.CourseGroup_pkid`, nullable, FK `FK_Course_CourseGroup`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link target: `/courses?courseGroupPkid={pkid}`
  - Query param the Course list accepts: `courseGroupPkid`

- **PartnerCourseGroup** (`PartnerCourseGroup.CourseGroup_pkid`, NOT NULL, FK `FK_PartnerCourseGroup_CourseGroup`)
  - Column header: 對應廠商課程群組
  - Button label: 查看廠商課程群組 (icon: `pi pi-sitemap`)
  - Link target: `/partner-course-groups?courseGroupPkid={pkid}`
  - Query param the PartnerCourseGroup list accepts: `courseGroupPkid`

**Deferred:** neither the Course nor the PartnerCourseGroup feature exists yet, so
`/courses` and `/partner-course-groups` are dead routes. This build ships the **counts but
not the link buttons** — `courseCount` and `partnerCourseGroupCount` render as plain
numbers in the list and detail pages. The routes and param names above are the contract to
build against when those features land. (Same call as `PublishStatus`; see
`spec/admin/PublishStatus.md`.)

---

## N-N Relationships

**N/A** — see *PartnerCourseGroup is NOT an N-N junction* above. No transaction is needed
anywhere in this repository; every method opens a connection and runs one or two statements.

---

## Query Filters

- **keyword**: string
  - LIKE on `Description` only — it is the only non-key column.

No FK filters (no FK columns), no bool filters (no `bit` columns), no date-range filters
(no date/datetime columns). The filter drawer holds a single input.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** | `CourseGroupLookup[]` — `{ pkid, description, label }`, ordered by `Description ASC` |

`label` is a computed C# property returning `Description` verbatim, mirroring
`PublishStatusLookup.Label` so the Angular `optionLabel="label"` binding stays uniform.
This feature does not consume the endpoint itself — it is published for the future Course
and PartnerCourseGroup forms, which both need it.

**Ordering note:** `spec/sample1.spec.md` suggests ordering this dropdown by `pkid ASC`.
With **215 options** that is not browsable, so this spec orders by `Description ASC`
instead. The samples are style/depth references per `CLAUDE.md`, not binding contracts —
but reconcile the two when the Course feature is actually built.

**Size note:** 215 options is past the ~100 threshold in `spec/code-gen.convention.md`, so
consumers must set `[filter]="true"` and `[virtualScroll]="true" [virtualScrollItemSize]="43"`.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all, `Description ASC` |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/course-groups/{id:int}` | Get by pkid |
| `POST` | `/api/course-groups` | Create — **no 409 path**, `Description` is not unique |
| `PUT` | `/api/course-groups` | Update (pkid from body) |
| `DELETE` | `/api/course-groups/{id:int}` | Delete — 409 if referenced by Course/PartnerCourseGroup |
| `GET` | `/api/lookups/course-groups` | Slim lookup list (on `LookupsController`) |

Route param is `{id:int}` while the C# PK is `short`: `:int` is the routing constraint and
the action signature takes `short id`, so binding narrows it. A value above 32767 fails
binding and yields 400.

No auth attributes — this API has no authentication configured yet, consistent with
`AppRolesController` and `PublishStatusesController`.

### Status codes

| Situation | Response |
|-----------|----------|
| Create/Update with blank `Description` | 400 (DataAnnotations) |
| Create/Update with `Description` over 100 chars | 400 (DataAnnotations) |
| Update with `Pkid` <= 0 | 400 `ValidationProblem` — 更新時必須提供主代碼 |
| Update/Delete unknown `pkid` | 404 |
| Delete while referenced | 409 `ProblemDetails` — 群組使用中 |
| Duplicate `Description` | **200/201 — allowed by design** |

---

## Backend Notes

### Models

```csharp
// Models/CourseGroup.cs
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數.</summary>
    public int PartnerCourseGroupCount { get; set; }
}

// Models/CourseGroupRequest.cs
public class CourseGroupRequest
{
    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public short Pkid { get; set; }

    [Required(ErrorMessage = "群組名稱為必填")]
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
}

// Models/CourseGroupQuery.cs
public class CourseGroupQuery
{
    /// <summary>關鍵字 — LIKE 比對 Description.</summary>
    public string? Keyword { get; set; }
}

// Models/LookupItem.cs — appended
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Label => Description;
}
```

`smallint` → `short`. `pkid` is IDENTITY so it is ignored on create, unlike
`PublishStatus.pkid`.

### SQL — SELECT

No JOINs and no multi-map (no FK columns). No `nchar` columns, so no `RTRIM()`. The only
alias needed is `pkid AS Pkid`.

```sql
SELECT g.pkid AS Pkid, g.Description,
       (SELECT COUNT(*) FROM Course c WHERE c.CourseGroup_pkid = g.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup p WHERE p.CourseGroup_pkid = g.pkid) AS PartnerCourseGroupCount
FROM CourseGroup g
```

`GetAllAsync` appends `ORDER BY g.Description ASC`; `QueryAsync` appends the `WHERE` then
the same `ORDER BY`; `GetByIdAsync` appends `WHERE g.pkid = @Pkid`.

### SQL — INSERT

`pkid` is IDENTITY, so it is excluded and read back.

```sql
INSERT INTO CourseGroup (Description) VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

Cast to `smallint`, not `int` — the PK column is `smallint`.

### SQL — UPDATE

```sql
UPDATE CourseGroup SET Description = @Description WHERE pkid = @Pkid;
```

Returns `false` when rows-affected is 0 so the controller can answer 404.

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid;
```

Guarded by `IsInUseAsync`:

```sql
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course              WHERE CourseGroup_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM PartnerCourseGroup  WHERE CourseGroup_pkid = @Pkid)
            THEN 1 ELSE 0 END;
```

### Repository interface

```csharp
public interface ICourseGroupRepository
{
    Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken cancellationToken = default);
    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);
    Task<short> CreateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>是否已被 Course 或 PartnerCourseGroup 參照 — 刪除前須檢查.</summary>
    Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default);
}
```

No `*ExistsAsync` — there is no uniqueness rule to enforce.

### Register in Program.cs

```csharp
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
```

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/course-groups` | `CourseGroupList` | 課程群組 CourseGroup |
| `/course-groups/new` | `CourseGroupForm` | 新增課程群組 |
| `/course-groups/:id/edit` | `CourseGroupForm` | 編輯課程群組 |
| `/course-groups/:id` | `CourseGroupDetail` | 課程群組明細 |

Lazy `loadComponent` in `app.routes.ts`, `/new` **before** `/:id`.

### Angular model

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
  courseCount: number;
  partnerCourseGroupCount: number;
}

export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

export interface CourseGroupQuery {
  keyword?: string | null;
}

export const EMPTY_COURSE_GROUP_QUERY: CourseGroupQuery = { keyword: null };

export interface CourseGroupLookup {
  pkid: number;
  description: string;
  label: string;
}
```

### Service

`core/services/course-group.service.ts`, base URL
`${environment.apiBaseUrl}/course-groups`, the standard six methods.
`LookupService` gains `getCourseGroups(): Observable<CourseGroupLookup[]>`.

### List component

Columns: 主代碼 / 群組名稱 / 對應課程 / 對應廠商課程群組 / 操作.

- The two counts are right-aligned (`cms-cell--number`). No link buttons yet — see
  *Primary-Foreign Links*.
- All columns sortable; default `description ASC` (`sortField: 'description', sortOrder: 1`).
- With 215 rows the paginator matters: default `rows: 20`, options `[10, 20, 50, 100]`.
- Filter drawer holds a single 關鍵字 input — no selects, so no `appendTo="body"` needed.
- Row actions: 檢視 / 編輯 / 刪除.

### Detail component

`cms-detail-grid` over 主代碼 and 群組名稱, then a 使用狀況 card with the two counts and,
when both are zero, the note 尚未被任何課程或廠商課程群組使用。

### Form component

Reactive Forms, no `forkJoin` — this form loads no lookups.

| Field | Control | Notes |
|-------|---------|-------|
| 主代碼 | read-only text, edit mode only | IDENTITY — never an input. Shown as a disabled display row so the user can see what they are editing |
| 群組名稱 | `input[pInputText]` | Required, maxlength 100 |

`pkid` is IDENTITY, so unlike the `PublishStatus` form there is **no pkid input control**
in add mode and nothing to disable in edit mode.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

On 409 the toast reads 此課程群組已被課程或廠商課程群組使用，無法刪除。

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-group-list-filters` | Last query filter values |
| `course-group-list-sort` | `{ sortField, sortOrder }` |
| `course-group-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates *into* this list yet.

### Date Handling / Sub-panels

**N/A** — no date columns, no child entities edited inline.

### Sidebar placement

**New group** 課程管理 Course, added after the existing 系統管理 Admin group in the
`navGroups` signal in `app.ts`:

```ts
{
  label: '課程管理 Course',
  icon: 'pi pi-book',
  expanded: true,
  items: [{ label: '課程群組 CourseGroup', icon: 'pi pi-tags', route: '/course-groups' }],
}
```

`app.html` needs no change — it renders groups generically.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Same shape as `PublishStatusesControllerTests`: `WebApplicationFactory<Program>` hosting
the real pipeline with only the repository swapped.

- `CourseGroupApiFactory.cs` — seeds 3 groups; one carries Course/PartnerCourseGroup usage
  so the delete guard has something to block on, and **two share a `Description`** so the
  no-duplicate-check rule is actually exercised.
- `FakeCourseGroupRepository.cs` — in-memory, mirroring the SQL: keyword LIKE on
  Description, `Description ASC` ordering, IDENTITY-style pkid assignment, usage counts.
- `CourseGroupsControllerTests.cs`:
  - `GetAll` ordered by Description, carrying both usage counts
  - `Query` by keyword; empty filter returns all; no match returns empty
  - `GetById` found / 404
  - `Create` returns 201 with `Location` and a server-assigned pkid
  - **`Create` with a Description that already exists returns 201, not 409**
  - **`Update` to a Description another row already uses returns 200, not 409**
  - `Create`/`Update` with blank Description → 400
  - `Update` with `Pkid` = 0 → 400 and the repository is not called
  - `Update` unknown pkid → 404
  - `Delete` unused → 204, then 404
  - `Delete` referenced → 409, row survives
- `LookupsControllerTests.cs` — extend with `GET /api/lookups/course-groups`: ordered by
  Description, `label` present on the wire (assert raw JSON — `Label` is computed, so a
  DTO round-trip would assert nothing).

### Frontend — Karma + Jasmine

Standard providers: `provideRouter([])`, `provideNoopAnimations()`,
`providePrimeNG({ theme: { preset: Aura } })`, `MessageService`, `ConfirmationService`,
plus jasmine spies. Assert against `data-testid`. Clear `sessionStorage` around list specs.

- `course-group.service.spec.ts` — each method hits the right URL/verb; `query()` POSTs the
  filter body; `update()` PUTs to the collection with pkid in the body; 409 on delete surfaces.
- `course-group-list.spec.ts` — loads on init; one row per record; counts render; keyword
  filter reaches `query()` and persists; saved filters restore; clear resets; sort/page
  persist; delete confirms then reloads; 409 on delete shows the in-use toast.
- `course-group-detail.spec.ts` — loads by route param; renders both fields and both
  counts; unused note; not-found state.
- `course-group-form.spec.ts` — add mode: title, no pkid control, required-field block,
  creates and navigates. Edit mode: title, loads by pkid, patches Description, updates via
  PUT with pkid in the body, cancel returns to detail.
- `app.spec.ts` — extend to assert the new 課程管理 Course group and its
  課程群組 CourseGroup entry linking to `/course-groups`.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/CourseGroup.cs` | Create |
| `src/CMS.API/Models/CourseGroupRequest.cs` | Create |
| `src/CMS.API/Models/CourseGroupQuery.cs` | Create |
| `src/CMS.API/Models/LookupItem.cs` | Modify — add `CourseGroupLookup` |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseGroupRepository.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetCourseGroupsAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/CourseGroupsController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `course-groups` |
| `src/CMS.API/Program.cs` | Modify — register the repository |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/course-group.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/course-group.service.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getCourseGroups` |
| `src/CMS.NG/src/app/features/course-groups/course-group-list/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-detail/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-form/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — **new** 課程管理 Course nav group |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/CourseGroupApiFactory.cs` | Create |
| `src/CMS.API.Tests/FakeCourseGroupRepository.cs` | Create |
| `src/CMS.API.Tests/CourseGroupsControllerTests.cs` | Create |
| `src/CMS.API.Tests/FakeLookupRepository.cs` | Modify — seed course groups |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify — cover `course-groups` |
| `src/CMS.NG/.../core/services/course-group.service.spec.ts` | Create |
| `src/CMS.NG/.../course-group-list/course-group-list.spec.ts` | Create |
| `src/CMS.NG/.../course-group-detail/course-group-detail.spec.ts` | Create |
| `src/CMS.NG/.../course-group-form/course-group-form.spec.ts` | Create |
| `src/CMS.NG/src/app/app.spec.ts` | Modify — assert the new nav group |
