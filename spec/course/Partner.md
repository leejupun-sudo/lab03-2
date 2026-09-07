# Build Spec for Partner

- database schema: `.\database\course.sql` (the identical `Partner` DDL also appears in
  `.\database\promotion.sql`; they are the same table, dumped twice)

## Summary

`Partner` is the 合作廠商 reference table — the vendor / brand a course belongs to
(Microsoft, Cisco, CompTIA, ISACA, 恆逸 …). It carries a short application key, three
separate display names for three different pages, a sort order and an optional logo
filename. It has **no outbound foreign keys**; it is purely an FK *target*, and the most
heavily referenced one built so far.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint** IDENTITY(1,1) |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | **N/A — `PartnerCourseGroup` is not a junction; see below** |
| Primary-Foreign Links | `Course`, `Certification`, `PartnerCourseGroup`, `Promotion2.RelatedPartner_pkid`, **`Seminar` (no FK constraint)** |
| Query Filters | keyword (`Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`) |
| Default Sort | `DisplayOrder ASC, Name ASC, pkid ASC` |

### Live data (read-only probe, 2026-09-07)

| Measure | Value |
|---------|-------|
| `Partner` rows | **66** |
| Distinct `Name` | **64** — `Name` is NOT unique |
| Distinct `AppKey` | **66** — unique in the data, but there is **no `UNIQUE` index** |
| Distinct `NameOnPartnerMenu` | 65 |
| Distinct `NameOnCourseDetailPage` | 63 |
| Distinct `DisplayOrder` | **36** — 23 rows share `9999` |
| `DisplayOrder` range | 0 – 9999 |
| `ImageFilename IS NULL` | 4 (62 non-null); **17 of those carry no file extension** |
| Max lengths | `Name` 23/50, `AppKey` **10/10**, `NameOnPartnerMenu` 43/200, `NameOnCourseDetailPage` 21/50, `ImageFilename` 14/50 |
| `Course` rows | 1084 (`Partner_pkid` NOT NULL — every course has one) |
| `Certification` rows | 39 |
| `PartnerCourseGroup` rows | 146 |
| `Promotion2` rows with `RelatedPartner_pkid` | 847 |
| `Seminar` rows with `Partner_pkid` | **364** |
| Partners referenced by none of the four declared FKs | **7** |

Five of these numbers change the generated code. Each gets a section below.

---

## `Seminar` references `Partner` with no FK constraint

This is the finding that most affects correctness, and neither `course.sql` nor
`promotion.sql` states it.

```sql
CREATE TABLE [dbo].[Seminar](
    [pkid] [int] IDENTITY(1,1) NOT NULL,
    [Title] [nvarchar](200) NOT NULL,
    [Partner_pkid] [smallint] NULL,   -- points at Partner.pkid; NO FOREIGN KEY declared
    ...
)
```

Verified live:

```sql
SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.Seminar');
-- 0
```

`sys.foreign_keys` lists exactly four constraints referencing `Partner`
(`FK_Course_Partner`, `FK_Certification_Partner`, `FK_PartnerCourseGroup_Partner`,
`FK_Promotion2_Partner` — all `NO_ACTION`). `Seminar` is not among them, yet **364
`Seminar` rows carry a `Partner_pkid`**, and all 364 resolve to a live `Partner` row
(0 orphans today). The relationship is real and the data is consistent; only the
constraint is missing.

**Why it matters.** Partner `122 標案或包班專用` is referenced by *none* of the four
declared FKs — so SQL Server would happily delete it — but **34 `Seminar` rows point at
it**. A delete guard built from the DDL alone would return `204`, silently orphaning
those 34 rows. SQL will not stop it, because there is no constraint to violate.

**Decision:** `SeminarCount` is carried in every SELECT and `IsInUseAsync` blocks on it
exactly like the four enforced references. This is the one usage count in this repository
that the database will *not* enforce on its own; do not drop it as redundant.

---

## `Name` is not unique — but `AppKey` is

Two adjacent columns, two opposite answers. Both were checked with the
`COUNT(DISTINCT col)` vs `COUNT(*)` probe that `CLAUDE.md` requires.

| Column | Distinct / Total | `UNIQUE` index? | Duplicate check? |
|--------|------------------|-----------------|------------------|
| `Name` | **64 / 66** | No | **No** |
| `AppKey` | **66 / 66** | No | **Yes — 409** |

`Name` fails the test outright: 「國際標準課程」 occurs three times. Adding a
`NameExistsAsync` would repeat the `CourseGroup.Description` mistake — it would contradict
the schema *and* make those three rows uneditable, each 409-ing against its own twins.

`AppKey` passes. It is the application-level short code (`ISO`, `CompTIA`, `blockchain`),
is fully distinct across all 66 rows, and behaves as a natural key. So this feature does
get an `AppKeyExistsAsync` and a 409 on create/update.

**This is an application-level rule, not a database constraint.** Nothing stops another
client inserting a duplicate `AppKey`, and if one ever appears the API will not 500 — it
will simply let both rows keep 409-ing against each other on save, the same failure mode
avoided for `CourseGroup`. Re-run the distinct probe before assuming this rule still holds.

`AppKey` is `varchar(10)` and the longest live value is **exactly 10 characters**
(`blockchain`), so the column is at capacity; `[MaxLength(10)]` is a real constraint here,
not a formality. It is `varchar`, not `nchar` — **no `RTRIM()`**.

---

## `PartnerCourseGroup` is NOT an N-N junction

Same trap as `CourseGroup`, and it fires harder here because `PartnerCourseGroup` is named
after *this* table.

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

It has its own IDENTITY `pkid` rather than a composite key over the FK pair, it carries its
own payload, and `Promotion2.RelatedPartnerCourseGroup_pkid` is an FK to its `pkid` with
**387 live rows** behind it. Delete-then-reinsert would hand every one of the 146 rows a
new pkid and orphan those 387 references.

`PartnerCourseGroup` is an entity in its own right and gets a Primary-Foreign link, not an
inline multi-select. See `spec/course/CourseGroup.md` for the full argument.

**N-N Relationships: N/A.** No transaction is needed anywhere in this feature.

---

## Usage counts and the delete guard

`Partner` is an FK target with no `ON DELETE` action on any of the four declared
constraints, so deleting a referenced row raises an FK violation — a 500, which
`CLAUDE.md` rules out for foreseeable conflicts. Same shape as the `PublishStatus` and
`CourseGroup` guards, but with **five** counts instead of two.

Every SELECT carries five correlated subqueries:

| Property | Subquery source | FK enforced? |
|----------|-----------------|--------------|
| `CourseCount` | `Course.Partner_pkid` | Yes |
| `CertificationCount` | `Certification.Partner_pkid` | Yes |
| `PartnerCourseGroupCount` | `PartnerCourseGroup.Partner_pkid` | Yes |
| `Promotion2Count` | `Promotion2.RelatedPartner_pkid` | Yes |
| `SeminarCount` | `Seminar.Partner_pkid` | **No — see above** |

`DELETE` calls `IsInUseAsync` first; a non-zero on **any** of the five returns 409 with a
`ProblemDetails` naming all five counts. The guard is not theoretical: 59 of the 66 rows
are referenced by at least one enforced FK, and of the 7 that are not, one is held only by
`Seminar`.

---

## Localization

### Chinese Table Name

- Partner: 合作廠商
- Description: 課程所屬的廠商 / 品牌 (Course、Certification、PartnerCourseGroup、Promotion2、Seminar 共用)

### Chinese Column Names

- pkid: 主代碼
- Name: 廠商名稱
- AppKey: 應用代碼
- NameOnPartnerMenu: 廠商選單顯示名稱
- NameOnCourseDetailPage: 課程明細頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖檔名稱

`Name` is labelled 廠商名稱, matching `sample1.spec.md`, which labels
`Course.Partner_pkid` as 合作廠商. The two `NameOn…` columns keep their page context in the
label because all three names are genuinely different values on 15 of the 66 rows — for
example pkid 133 is `Office2` / 商業應用系列 / `Office`. Collapsing them to
「選單名稱」/「明細名稱」 would lose which page each drives.

---

## Required Fields

Required (NOT NULL, excluding the IDENTITY PK):

- `Name` — `nvarchar(50)`
- `AppKey` — `varchar(10)`
- `NameOnPartnerMenu` — `nvarchar(200)`
- `NameOnCourseDetailPage` — `nvarchar(50)`
- `DisplayOrder` — `int`

Optional (nullable):

- `ImageFilename` — `varchar(50)`

There are no computed columns, no `nchar` columns, no date/time columns, no `bit` columns
and no `DEFAULT` constraints on this table.

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns, so there is no outbound navigation.

**N/A**

---

## Primary-Foreign Links

Five tables reference `Partner.pkid`.

- **Course** (`Course.Partner_pkid`, NOT NULL, `FK_Course_Partner`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link target: `/courses?partnerPkid={pkid}`

- **Certification** (`Certification.Partner_pkid`, NOT NULL, `FK_Certification_Partner`)
  - Column header: 對應認證
  - Button label: 查看認證 (icon: `pi pi-verified`)
  - Link target: `/certifications?partnerPkid={pkid}`

- **PartnerCourseGroup** (`PartnerCourseGroup.Partner_pkid`, NOT NULL, `FK_PartnerCourseGroup_Partner`)
  - Column header: 對應廠商課程群組
  - Button label: 查看廠商課程群組 (icon: `pi pi-sitemap`)
  - Link target: `/partner-course-groups?partnerPkid={pkid}`

- **Promotion2** (`Promotion2.RelatedPartner_pkid`, **nullable**, `FK_Promotion2_Partner`)
  - Column header: 對應促銷活動
  - Button label: 查看促銷活動 (icon: `pi pi-megaphone`)
  - Link target: `/promotion2s?relatedPartnerPkid={pkid}`
  - Note the param is `relatedPartnerPkid`, not `partnerPkid` — the column is
    `RelatedPartner_pkid` and `Promotion2` may later gain other partner-shaped columns.

- **Seminar** (`Seminar.Partner_pkid`, nullable, **no FK constraint**)
  - Column header: 對應研討會
  - Button label: 查看研討會 (icon: `pi pi-users`)
  - Link target: `/seminars?partnerPkid={pkid}`

**Deferred:** none of `/courses`, `/certifications`, `/partner-course-groups`,
`/promotion2s`, `/seminars` exists yet. This build ships the **counts but not the link
buttons** — the five counts render as plain numbers in the list and detail pages. The
routes and param names above are the contract to build against when those features land.
Same call as `PublishStatus` and `CourseGroup`.

---

## N-N Relationships

**N/A** — see *`PartnerCourseGroup` is NOT an N-N junction* above.

---

## Query Filters

- **keyword**: string
  - LIKE on `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`.
  - `ImageFilename` is deliberately excluded: it is an asset path, not an identifying
    name, and the template restricts keyword search to short identifying columns.

There are no FK filters (no FK columns), no bool filters (no `bit` columns), and no
date-range filters (no date/datetime columns). The filter drawer holds a single input, so
no `appendTo="body"` is needed anywhere in this feature.

**Considered and rejected:** a `DisplayOrder` range filter and a "has logo" tri-state. Both
are inventions beyond `spec/feature-spec.template.md`, which derives filters from FK, bool
and date columns only. With 66 rows the keyword box plus column sorting covers the same
ground.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | `PartnerLookup[]` — `{ pkid, name, appKey, label }`, ordered by `DisplayOrder ASC, Name ASC, pkid ASC` |

This feature does not consume the endpoint itself — it is published for the future
`Course`, `Certification`, `PartnerCourseGroup`, `Promotion2` and `Seminar` forms, all five
of which need it.

**Label choice.** `sample1.spec.md` specifies `Option label = Name` for the Partner
dropdown. Taken literally that produces three identical 「國際標準課程」 options with no way
to tell them apart. So:

```csharp
public string Label => $"{Name} ({AppKey})";
```

This mirrors the established `AppUserLookup.Label => $"{UserName} ({UserId})"` in
`Models/LookupItem.cs`, and `AppKey` is the one column proven distinct across all 66 rows.
`sample1` is a style/depth reference per `CLAUDE.md`, not a binding contract — but
reconcile the two when the Course feature is actually built.

**Ordering.** `DisplayOrder ASC, Name ASC, pkid ASC`, following `sample1`'s
`DisplayOrder ASC` and adding the tie-breaks the live data demands: 23 rows share
`DisplayOrder = 9999`, and `Name` is not unique either, so both extra keys are needed
before the order is deterministic.

**Size note.** 66 options is past the 10-option threshold in
`spec/code-gen.convention.md`, so consumers set `[filter]="true"`. It is **below** the ~100
threshold, so `[virtualScroll]` is *not* needed — unlike `course-groups`.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all, `DisplayOrder ASC, Name ASC, pkid ASC` |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id:int}` | Get by pkid |
| `POST` | `/api/partners` | Create — 409 on duplicate `AppKey` |
| `PUT` | `/api/partners` | Update (pkid from body) — 409 on duplicate `AppKey` |
| `DELETE` | `/api/partners/{id:int}` | Delete — 409 if referenced by any of the five tables |
| `GET` | `/api/lookups/partners` | Slim lookup list (on `LookupsController`) |

Route param is `{id:int}` while the C# PK is `short`: `:int` is the routing constraint and
the action signature takes `short id`, so binding narrows it. A value above 32767 fails
binding and yields 400. Identical to `CourseGroup`.

No auth attributes — this API has no authentication configured yet, consistent with
`AppRolesController`, `PublishStatusesController` and `CourseGroupsController`.

### Status codes

| Situation | Response |
|-----------|----------|
| Create/Update with any required field blank | 400 (DataAnnotations) |
| Create/Update over a column's max length | 400 (DataAnnotations) |
| Create/Update with `DisplayOrder` outside 0–9999 | 400 — `[Range(0, 9999)]`, see below |
| Update with `Pkid` <= 0 | 400 `ValidationProblem` — 更新時必須提供主代碼 |
| Update/Delete unknown `pkid` | 404 |
| Create/Update with an `AppKey` another row holds | **409 `ProblemDetails` — 應用代碼重複** |
| Delete while referenced | 409 `ProblemDetails` — 廠商使用中 |
| Duplicate `Name` | **200/201 — allowed by design** |

`DisplayOrder` is a plain `int` in SQL with no `CHECK`. `[Range(0, 9999)]` narrows it to
the live range (min 0, max 9999) the same way `PublishStatus.pkid` narrows `tinyint` to
`[Range(1, 255)]` — a deliberate application-level narrowing, documented so it is not
mistaken for a schema fact. `9999` is the de-facto "park at the end" sentinel, used by 23
rows; the form's placeholder says so.

---

## Backend Notes

### Models

```csharp
// Models/Partner.cs
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }

    /// <summary>使用中的課程數 — Course 參照筆數.</summary>
    public int CourseCount { get; set; }

    /// <summary>使用中的認證數 — Certification 參照筆數.</summary>
    public int CertificationCount { get; set; }

    /// <summary>使用中的廠商課程群組數 — PartnerCourseGroup 參照筆數.</summary>
    public int PartnerCourseGroupCount { get; set; }

    /// <summary>使用中的促銷活動數 — Promotion2.RelatedPartner_pkid 參照筆數.</summary>
    public int Promotion2Count { get; set; }

    /// <summary>使用中的研討會數 — Seminar.Partner_pkid 參照筆數 (無 FK 約束).</summary>
    public int SeminarCount { get; set; }
}

// Models/PartnerRequest.cs
public class PartnerRequest
{
    /// <summary>主代碼 — 新增時忽略 (IDENTITY), 更新時必填.</summary>
    public short Pkid { get; set; }

    [Required(ErrorMessage = "廠商名稱為必填")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "應用代碼為必填")]
    [MaxLength(10)]
    public string AppKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "廠商選單顯示名稱為必填")]
    [MaxLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required(ErrorMessage = "課程明細頁顯示名稱為必填")]
    [MaxLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    [Range(0, 9999, ErrorMessage = "顯示順序須介於 0 與 9999 之間")]
    public int DisplayOrder { get; set; }

    [MaxLength(50)]
    public string? ImageFilename { get; set; }
}

// Models/PartnerQuery.cs
public class PartnerQuery
{
    /// <summary>關鍵字 — LIKE 比對 Name / AppKey / NameOnPartnerMenu / NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }
}

// Models/LookupItem.cs — appended
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string Label => $"{Name} ({AppKey})";
}
```

`smallint` → `short`; `pkid` is IDENTITY so it is ignored on create, unlike
`PublishStatus.pkid`. `ImageFilename` is the only nullable column and maps to `string?`.

### `ImageFilename` carries no format guarantee

62 of the 66 rows are non-null, and **17 of those contain no `.` at all** — `Splunk`,
`TOGAF`, `Blue Coat`, `恆逸`, `政府補助課程`, `轉職培訓` … The remaining 45 split
`.svg` (30), `.png` (14), `.jpg` (1). There is no empty string in the column, only NULLs.

So: **no extension validation, no regex, no content-type inference.** It is a
`[MaxLength(50)]` free-text field. The form sends `null` when blank (never `""`), matching
how `AppRole.Description` is handled, and the detail page renders the raw value as text
rather than attempting an `<img src>` — there is no base path in configuration and 17
values would 404 anyway.

### SQL — SELECT

No JOINs and no multi-map (no FK columns). No `nchar` columns, so no `RTRIM()`. The only
alias needed is `pkid AS Pkid`.

```sql
SELECT p.pkid AS Pkid, p.Name, p.AppKey, p.NameOnPartnerMenu,
       p.NameOnCourseDetailPage, p.DisplayOrder, p.ImageFilename,
       (SELECT COUNT(*) FROM Course              c WHERE c.Partner_pkid        = p.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Certification       t WHERE t.Partner_pkid        = p.pkid) AS CertificationCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup  g WHERE g.Partner_pkid        = p.pkid) AS PartnerCourseGroupCount,
       (SELECT COUNT(*) FROM Promotion2          r WHERE r.RelatedPartner_pkid = p.pkid) AS Promotion2Count,
       (SELECT COUNT(*) FROM Seminar             s WHERE s.Partner_pkid        = p.pkid) AS SeminarCount
FROM Partner p
```

`GetAllAsync` appends `ORDER BY p.DisplayOrder ASC, p.Name ASC, p.pkid ASC`; `QueryAsync`
appends the `WHERE` then the same `ORDER BY`; `GetByIdAsync` appends `WHERE p.pkid = @Pkid`.

**Three sort keys, all load-bearing.** `DisplayOrder` alone leaves the 23 rows at `9999`
unordered; adding `Name` is not enough either, because `Name` is not unique — the three
「國際標準課程」 rows would still tie and could swap between requests, breaking paging.
`pkid` closes the ordering.

The keyword `WHERE`:

```sql
WHERE (@Keyword IS NULL
       OR p.Name                   LIKE '%' + @Keyword + '%'
       OR p.AppKey                 LIKE '%' + @Keyword + '%'
       OR p.NameOnPartnerMenu      LIKE '%' + @Keyword + '%'
       OR p.NameOnCourseDetailPage LIKE '%' + @Keyword + '%')
```

### SQL — INSERT

`pkid` is IDENTITY, so it is excluded and read back.

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

Cast to `smallint`, not `int` — the PK column is `smallint`.

### SQL — UPDATE

```sql
UPDATE Partner
SET Name = @Name, AppKey = @AppKey, NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage, DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

`pkid` is **never** written — it is an FK target for five tables. Returns `false` when
rows-affected is 0 so the controller can answer 404.

### SQL — duplicate `AppKey` check

```sql
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Partner WHERE AppKey = @AppKey AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)
) THEN 1 ELSE 0 END;
```

`@ExcludePkid` is null on create and the row's own pkid on update, so a row never
conflicts with itself.

### SQL — DELETE

```sql
DELETE FROM Partner WHERE pkid = @Pkid;
```

Guarded by `IsInUseAsync`:

```sql
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course             WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM Certification      WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM PartnerCourseGroup WHERE Partner_pkid        = @Pkid)
              OR EXISTS (SELECT 1 FROM Promotion2         WHERE RelatedPartner_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM Seminar            WHERE Partner_pkid        = @Pkid)
            THEN 1 ELSE 0 END;
```

The `Seminar` clause is the one SQL Server would not have enforced.

### Repository interface

```csharp
public interface IPartnerRepository
{
    Task<IEnumerable<Partner>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken cancellationToken = default);
    Task<Partner?> GetByIdAsync(short pkid, CancellationToken cancellationToken = default);
    Task<short> CreateAsync(PartnerRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PartnerRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(short pkid, CancellationToken cancellationToken = default);

    /// <summary>AppKey 是否已被其他資料列使用 — 應用層唯一性規則, 資料庫並未約束.</summary>
    Task<bool> AppKeyExistsAsync(string appKey, short? excludePkid = null, CancellationToken cancellationToken = default);

    /// <summary>是否已被 Course / Certification / PartnerCourseGroup / Promotion2 / Seminar 參照.</summary>
    Task<bool> IsInUseAsync(short pkid, CancellationToken cancellationToken = default);
}
```

### Register in Program.cs

```csharp
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
```

### RowAudit

Not implemented — see *Gaps between the `/crud` skill and this codebase* in `CLAUDE.md`.
No `RowAuditWriter` exists, so none is injected, exactly as in the three shipped features.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/partners` | `PartnerList` | 合作廠商 Partner |
| `/partners/new` | `PartnerForm` | 新增合作廠商 |
| `/partners/:id/edit` | `PartnerForm` | 編輯合作廠商 |
| `/partners/:id` | `PartnerDetail` | 合作廠商明細 |

Lazy `loadComponent` in `app.routes.ts`, `/new` **before** `/:id`.

### Angular model

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
  courseCount: number;
  certificationCount: number;
  partnerCourseGroupCount: number;
  promotion2Count: number;
  seminarCount: number;
}

export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerQuery {
  keyword?: string | null;
}

export const EMPTY_PARTNER_QUERY: PartnerQuery = { keyword: null };

export interface PartnerLookup {
  pkid: number;
  name: string;
  appKey: string;
  label: string;
}
```

### Service

`core/services/partner.service.ts`, base URL `${environment.apiBaseUrl}/partners`, the
standard six methods. `LookupService` gains `getPartners(): Observable<PartnerLookup[]>`.

### List component

Columns: 主代碼 / 廠商名稱 / 應用代碼 / 顯示順序 / 使用中 / 操作.

- Five usage counts in one column would be unreadable. The list shows a single
  **使用中** column holding the *total* (`courseCount + certificationCount +
  partnerCourseGroupCount + promotion2Count + seminarCount`), right-aligned
  (`cms-cell--number`); the detail page breaks it down. The two `NameOn…` columns are
  omitted from the table for width but remain keyword-searchable and appear in detail.
- All shown columns sortable; default `displayOrder ASC` (`sortField: 'displayOrder',
  sortOrder: 1`). The `Name ASC, pkid ASC` tie-breaks are server-side; PrimeNG's client sort
  is single-key, which is acceptable for the default view since rows arrive pre-ordered.
- 66 rows: default `rows: 20`, options `[10, 20, 50, 100]`.
- Filter drawer holds a single 關鍵字 input — no selects, so no `appendTo="body"`.
- Row actions: 檢視 / 編輯 / 刪除.

### Detail component

`cms-detail-grid` over 主代碼 / 廠商名稱 / 應用代碼 / 廠商選單顯示名稱 /
課程明細頁顯示名稱 / 顯示順序 / 圖檔名稱 (— when null), then a 使用狀況 card listing all
five counts separately and, when all are zero, the note
尚未被任何課程、認證、廠商課程群組、促銷活動或研討會使用。

### Form component

Reactive Forms, no `forkJoin` — this form loads no lookups.

| Field | Control | Notes |
|-------|---------|-------|
| 主代碼 | read-only text, edit mode only | IDENTITY — never an input |
| 廠商名稱 | `input[pInputText]` | Required, maxlength 50 |
| 應用代碼 | `input[pInputText]` | Required, maxlength 10. Hint: 需唯一 |
| 廠商選單顯示名稱 | `input[pInputText]` | Required, maxlength 200 |
| 課程明細頁顯示名稱 | `input[pInputText]` | Required, maxlength 50 |
| 顯示順序 | `p-inputnumber` | Required, 0–9999. Placeholder note: 9999 表示排在最後 |
| 圖檔名稱 | `input[pInputText]` | Optional, maxlength 50. Sends `null` when blank |

`pkid` is IDENTITY, so there is **no pkid input control** in add mode and nothing to
disable in edit mode — same as the `CourseGroup` form, unlike `PublishStatus`.

**Special form behavior:** none. No auto-defaulting, no conditional visibility, no
`{ emitEvent: false }` subscriptions. `DisplayOrder` does not default from anything — the
add form seeds it to `9999` so a new partner parks at the end of the menu rather than
jumping to position 0, which is what the 23 rows already at 9999 establish as the house
convention.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？
```

On 409 the toast reads 此廠商已被課程、認證、廠商課程群組、促銷活動或研討會使用，無法刪除。
On a duplicate-`AppKey` 409 the form toast reads 應用代碼「{appKey}」已被其他廠商使用。

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `partner-list-filters` | Last query filter values |
| `partner-list-sort` | `{ sortField, sortOrder }` |
| `partner-list-page` | `{ first, rows }` |

No incoming cross-entity query params — nothing navigates *into* this list yet.

### Date Handling / Sub-panels

**N/A** — no date columns, no child entities edited inline.

### Sidebar placement

**Existing group** 課程管理 Course (created by the `CourseGroup` build), in the `navGroups`
signal in `app.ts`. Placed **before** 課程群組 CourseGroup, since Partner is the broader
parent concept:

```ts
{
  label: '課程管理 Course',
  icon: 'pi pi-book',
  expanded: true,
  items: [
    { label: '合作廠商 Partner', icon: 'pi pi-building', route: '/partners' },
    { label: '課程群組 CourseGroup', icon: 'pi pi-tags', route: '/course-groups' },
  ],
}
```

`app.html` needs no change — it renders groups generically.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Same shape as `CourseGroupsControllerTests`: `WebApplicationFactory<Program>` hosting the
real pipeline with only the repository swapped. **A fresh factory per test** — the fakes
hold mutable state.

- `PartnerApiFactory.cs` — seeds 4 partners: one referenced by Course only, one referenced
  **by `Seminar` only** (so the unenforced guard is actually exercised), one wholly unused
  and deletable, and **two sharing a `Name`** so the no-duplicate-check-on-Name rule is
  proven. All four carry distinct `AppKey`s, and `DisplayOrder` values that collide so the
  `Name ASC` tie-break is observable.
- `FakePartnerRepository.cs` — in-memory, mirroring the SQL: keyword LIKE across the four
  columns, `DisplayOrder ASC, Name ASC, pkid ASC` ordering, IDENTITY-style pkid assignment, all five
  usage counts, and `AppKeyExistsAsync` honouring `excludePkid`.
- `PartnersControllerTests.cs`:
  - `GetAll` ordered by `DisplayOrder`, then `Name`, then `pkid`, carrying all five usage counts
  - `Query` by keyword matches `AppKey` and `NameOnPartnerMenu`, not just `Name`; empty
    filter returns all; no match returns empty
  - `GetById` found / 404
  - `Create` returns 201 with `Location` and a server-assigned pkid
  - **`Create` with a `Name` that already exists returns 201, not 409**
  - **`Create` with an `AppKey` that already exists returns 409**
  - **`Update` keeping the row's own `AppKey` returns 200** (no self-conflict)
  - `Update` taking another row's `AppKey` returns 409
  - `Create`/`Update` with any blank required field → 400
  - `Create` with `DisplayOrder` = 10000 → 400
  - `Update` with `Pkid` = 0 → 400 and the repository is not called
  - `Update` unknown pkid → 404
  - `Delete` unused → 204, then 404
  - `Delete` referenced by Course → 409, row survives
  - **`Delete` referenced only by `Seminar` → 409**, row survives
  - `ImageFilename` round-trips `null`
- `LookupsControllerTests.cs` — extend with `GET /api/lookups/partners`: ordered by
  `DisplayOrder`, `Name` then `pkid`, and `label` present on the wire as `Name (AppKey)`. Assert
  the **raw JSON** via `JsonDocument` — `Label` is a computed property, so a DTO
  round-trip would recompute it client-side and prove nothing.

### Frontend — Karma + Jasmine

Standard providers: `provideRouter([])`, `provideNoopAnimations()`,
`providePrimeNG({ theme: { preset: Aura } })`, `MessageService`, `ConfirmationService`,
plus jasmine spies. Assert against `data-testid`. Clear `sessionStorage` around list specs.

- `partner.service.spec.ts` — each method hits the right URL/verb; `query()` POSTs the
  filter body; `update()` PUTs to the collection with pkid in the body; 409 on delete
  surfaces.
- `partner-list.spec.ts` — loads on init; one row per record; the summed 使用中 total
  renders; keyword filter reaches `query()` and persists; saved filters restore; clear
  resets; sort/page persist; delete confirms then reloads; 409 on delete shows the in-use
  toast.
- `partner-detail.spec.ts` — loads by route param; renders all seven fields and all five
  counts separately; null `imageFilename` renders 「—」; all-zero note; not-found state.
- `partner-form.spec.ts` — add mode: title, no pkid control, `displayOrder` seeded to 9999,
  required-field block, blank `imageFilename` submits `null`, creates and navigates, 409
  shows the duplicate-AppKey toast. Edit mode: title, loads by pkid, patches every field,
  updates via PUT with pkid in the body, cancel returns to detail.
- `app.spec.ts` — extend to assert 合作廠商 Partner appears in the 課程管理 Course group and
  links to `/partners`.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/Partner.cs` | Create |
| `src/CMS.API/Models/PartnerRequest.cs` | Create |
| `src/CMS.API/Models/PartnerQuery.cs` | Create |
| `src/CMS.API/Models/LookupItem.cs` | Modify — add `PartnerLookup` |
| `src/CMS.API/Repositories/IPartnerRepository.cs` | Create |
| `src/CMS.API/Repositories/PartnerRepository.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetPartnersAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/PartnersController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `partners` |
| `src/CMS.API/Program.cs` | Modify — register the repository |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/partner.model.ts` | Create |
| `src/CMS.NG/src/app/core/services/partner.service.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getPartners` |
| `src/CMS.NG/src/app/features/partners/partner-list/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/partners/partner-detail/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/features/partners/partner-form/` (`.ts`/`.html`/`.scss`) | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — add to the 課程管理 Course nav group |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/PartnerApiFactory.cs` | Create |
| `src/CMS.API.Tests/FakePartnerRepository.cs` | Create |
| `src/CMS.API.Tests/PartnersControllerTests.cs` | Create |
| `src/CMS.API.Tests/FakeLookupRepository.cs` | Modify — seed partners |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify — cover `partners` |
| `src/CMS.NG/.../core/services/partner.service.spec.ts` | Create |
| `src/CMS.NG/.../partner-list/partner-list.spec.ts` | Create |
| `src/CMS.NG/.../partner-detail/partner-detail.spec.ts` | Create |
| `src/CMS.NG/.../partner-form/partner-form.spec.ts` | Create |
| `src/CMS.NG/src/app/app.spec.ts` | Modify — assert the new nav entry |
