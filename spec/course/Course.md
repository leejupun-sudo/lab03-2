# Build Spec for Course

- database schema: `.\database\course.sql`

## Summary

`Course` is the 課程 master record — the central entity of the system. Each row is one
training course sold under a `Partner` brand, optionally filed in a `CourseGroup`, with a
`PublishStatus`, a shelf window (`ScheduleOn` / `ScheduleOff`), pricing, and eight free-text
description blocks. It is the parent of two true N-N junctions (certifications, job
categories) and of three child tables that block delete.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY(1,1) |
| Natural Key | `CourseId` varchar(50) — **`UNIQUE` index in the live DB, absent from the DDL** |
| Foreign Keys | `Partner_pkid` → `Partner.pkid` (NOT NULL); `CourseGroup_pkid` → `CourseGroup.pkid` (nullable, **ON DELETE CASCADE**); `PublishStatus_pkid` → `PublishStatus.pkid` (NOT NULL) |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseInCertification` (↔ `Certification`), `CourseJobCategories` (↔ `JobCategory`) — both true junctions |
| Primary-Foreign Links | `CourseFAQ`, `CourseRelatedLink`, `HotCourse` (FK, NO_ACTION); **`CourseRecomm` (by `CourseId`, no FK)** |
| Query Filters | keyword (`Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`); `Partner_pkid`; `CourseGroup_pkid`; `PublishStatus_pkid`; `ScheduleOn` range; `ScheduleOff` range; `CanRepeat` tri-state |
| Default Sort | `CourseId ASC` |

### Live data (read-only probe, 2026-09-07)

| Measure | Value |
|---------|-------|
| `Course` rows | **1084** |
| Distinct `CourseId` | **1084** — unique, backed by `IX_Course_UniqueCourseId` (case-insensitive collation) |
| Distinct `ProdCourseId` | 1000 (77 duplicate groups) — **not** unique |
| Distinct `FriendlyUrl` | 929 (125 duplicate groups) — **not** unique; longest value is **100/100** |
| Distinct `Title` | 931 (124 duplicate groups) — **not** unique |
| `OfficialTitle IS NULL` | 15 |
| `CourseGroup_pkid IS NULL` | **0** — nullable in schema, never null in data |
| `OtherInfo IS NULL` | **1084** — the column is unused |
| `Outline` containing HTML tags | 112 |
| Distinct `DisplayOrder` | 65 (range 0–1000; 201 rows at `1`, 156 at `0`) |
| `CanRepeat = 1` | 149 |
| `PublishStatus` split | 草稿 7 / 上架中 436 / 已下架 641 |
| `ScheduleOn` range | 2011-10-19 – 2026-06-03 |
| `ScheduleOff` range | 2013-12-17 – 2099-12-31 |
| `ScheduleOff < ScheduleOn` | **1** (pkid 1980 `NINS-1`) |
| `ScheduleOff = ScheduleOn + 10y` | 2 (pkid 3328, 3332 — the newest rows) |
| `Hour` / `ListPrice` / `LearningCredit` | 0–560 / 0–195000 / 0.0–58.0 (387 rows carry a `.5`-style fraction) |
| Max lengths | `Title` 78/200, `OfficialTitle` 100/300, `CourseId` 18/50, `ProdCourseId` 18/50, `Outline` 4413, `TowardCertOrExam` 4413, `Note` 2413/4000 |
| `CourseInCertification` rows | 113 (94 courses; max 5 per course) |
| `CourseJobCategories` rows | 1048 (751 courses; max 7 per course) |
| `CourseFAQ` / `CourseRelatedLink` / `HotCourse` rows | 20 (3 courses) / 711 (273 courses) / 7 (7 courses) |
| `CourseRecomm` rows | **3118**, referencing 1026 courses by `CourseId`; **895 rows point at a `CourseId` that no longer exists** |
| Courses referenced by no enforced FK | 808 |
| `Certification` / `JobCategory` rows | 39 (titles all distinct, `nchar`) / 18 |

Seven of these change the generated code. Each gets a section below.

---

## `CourseId` is unique — by an index the DDL does not declare

`course.sql` declares only `PK_Course`. The live table also carries:

```
IX_Course_UniqueCourseId   UNIQUE NONCLUSTERED (CourseId)
```

So a duplicate `CourseId` is not an application rule here, it is a **database constraint**
that would surface as a `500` on raw INSERT. The repository gets a `CourseIdExistsAsync`
and the controller returns `409 ProblemDetails` on create. The collation is
`Chinese_Taiwan_Stroke_CI_AS`, so the check is case-insensitive, matching the index.

`ProdCourseId`, `FriendlyUrl` and `Title` all fail the distinct-vs-total probe (77, 125 and
124 duplicate groups) and get **no** duplicate check — a check would make hundreds of live
rows uneditable.

---

## `CourseId` is immutable after creation

`CourseRecomm` references courses by `CourseId` *value* — a `varchar(50)` pair with no FK,
no cascade, 3118 rows. Renaming a `CourseId` would silently detach every recommendation
pointing at it. This is the `AppRole.RoleId` shape: the edit form disables the control and
`UPDATE` never writes the column. A typo in a fresh row is fixed by deleting it (a new row
is unreferenced) and re-creating it.

**Judgment call, reversible at review.** The data shows the reference is already poorly
maintained (895 of 3118 rows are orphans), so one could argue the rule protects nothing.
The counter-argument is that "already broken" is not a reason to break it further, and the
house rule (`CLAUDE.md`: referenced keys are immutable) is applied uniformly.

---

## Delete guard — three enforced references and one that is not

`sys.foreign_keys` lists five constraints referencing `Course.pkid`:

| Constraint | Table | ON DELETE | Effect |
|------------|-------|-----------|--------|
| `FK_CourseFAQ_Course` | `CourseFAQ` | NO_ACTION | **blocks delete** |
| `FK_CourseRelatedLink_Course` | `CourseRelatedLink` | NO_ACTION | **blocks delete** |
| `FK_HotCourse_Course` | `HotCourse` | NO_ACTION | **blocks delete** |
| `FK_CourseInCertification_Course` | `CourseInCertification` | CASCADE | junction rows go with the course |
| `FK_CourseJobCategories_Course` | `CourseJobCategories` | CASCADE | junction rows go with the course |

Plus one the constraint list cannot show: **`CourseRecomm`** keys on `CourseId` with no
FK at all. Following the `Seminar → Partner` precedent, it is counted and it blocks. Every
SELECT carries four counts:

| Property | Source | FK enforced? |
|----------|--------|--------------|
| `FaqCount` | `CourseFAQ.Course_pkid` | Yes |
| `RelatedLinkCount` | `CourseRelatedLink.Course_pkid` | Yes |
| `HotCourseCount` | `HotCourse.Course_pkid` | Yes |
| `RecommCount` | `CourseRecomm.CourseId = c.CourseId OR CourseRecomm.RecommCourseId = c.CourseId` | **No** |

`IsInUseAsync` returns true on any non-zero count; `DELETE` answers `409`. The junction
rows are **not** counted — they cascade, and the course "owns" them.

**Consequence worth stating plainly:** 808 courses are deletable by FK alone, but only
about 58 once `CourseRecomm` is included. That is the data, not a bug. The alternative —
deleting the recommendation rows inside the same transaction — is a data-loss decision
this build does not take silently; it is the obvious thing to reconsider when a
`CourseRecomm` feature exists to manage them.

---

## `FK_Course_CourseGroup` cascades — the one outbound hazard

```sql
ALTER TABLE [dbo].[Course] ADD CONSTRAINT [FK_Course_CourseGroup]
    FOREIGN KEY([CourseGroup_pkid]) REFERENCES [dbo].[CourseGroup] ([pkid])
    ON DELETE CASCADE
```

Deleting a `CourseGroup` row at the SQL level deletes **every course in it**. Nothing in
this feature does that, and the `CourseGroup` feature's application guard already returns
`409` while `CourseCount > 0` — but `spec/course/CourseGroup.md` does not mention the
cascade, and any direct SQL that bypasses the API would find it. Recorded here so the
guard is never "simplified" away.

`CourseGroup_pkid` is nullable in the schema and the form treats it as optional
(`showClear`, sends `null`), even though zero live rows are null. The schema wins.

---

## Two true N-N junctions

Both pass the junction test from `docs/claude/schema-traps.md`: composite PK on the FK pair, no surrogate
key, no payload, nothing FKs to them, and both cascade from `Course`.

| Junction | Columns | Partner entity | Live max per course |
|----------|---------|----------------|---------------------|
| `CourseInCertification` | `(Course_pkid, Certification_pkid)` | `Certification` (39 rows) | 5 |
| `CourseJobCategories` | `(Course_pkid, JobCategory_pkid)` | `JobCategory` (18 rows) | 7 |

Sync is delete-then-reinsert inside **one transaction** with the parent INSERT/UPDATE.
This is the first feature in the repo that needs a transaction.

---

## Dates

- **`ScheduleOff` auto-defaults to `ScheduleOn + 10 years` in add mode only.** Only 2 of
  1084 rows follow this rule — but they are the two newest rows (2026-01-16), so it is the
  current convention, not a historical one. `sample1.spec.md` applies it in edit mode too;
  this build does not, because editing `ScheduleOn` on an existing course must not clobber
  a deliberately set `ScheduleOff`.
- **`ScheduleOff >= ScheduleOn` is enforced in the form only.** There is no `CHECK`, and
  one live row (pkid 1980 `NINS-1`, 已下架) violates it. The API accepts what SQL accepts;
  the form refuses to save an inverted range, so that row will need its dates corrected
  the next time someone edits it. That is a fix, not a regression.
- `date` columns map to `DateOnly` via the handler already registered in `Program.cs`.
  The Angular side converts ISO `yyyy-MM-dd` ↔ `Date` with local components, never
  `toISOString()` (UTC+8 off-by-one). Helpers live in `core/utils/date.util.ts` (new).

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料 — 每筆為一門在合作廠商品牌下販售的課程

### Chinese Column Names

Labels supplied with the `/crud` invocation take precedence over `sample1.spec.md` and are
used everywhere (list, detail, form, filter) for consistency.

- pkid: 主代碼
- DisplayOrder: 顯示順序
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- FriendlyUrl: 友善網址
- Partner_pkid: 原廠
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- CanRepeat: 允許重聽
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- (junction) CourseInCertification: 對應認證
- (junction) CourseJobCategories: 職務類別

---

## Required Fields

Required (NOT NULL, excluding the IDENTITY PK):

- `Title` nvarchar(200)
- `CourseId` varchar(50) — unique, immutable
- `ProdCourseId` varchar(50)
- `FriendlyUrl` nvarchar(100) — live max is exactly 100
- `DisplayOrder` int
- `Partner_pkid` smallint
- `PublishStatus_pkid` tinyint
- `ScheduleOn` date
- `ScheduleOff` date
- `Hour` smallint (DEFAULT 0)
- `ListPrice` decimal(9,0) (DEFAULT 0)
- `LearningCredit` decimal(9,1) (DEFAULT 0)
- `CanRepeat` bit (DEFAULT 0)

Optional (nullable):

- `OfficialTitle` nvarchar(300)
- `CourseGroup_pkid` smallint
- `Material` nvarchar(500), `Target` nvarchar(500)
- `Objective`, `Prerequisites`, `Note`, `OtherInfo` nvarchar(4000)
- `Outline`, `TowardCertOrExam` nvarchar(max)

Blank optional text is stored as `NULL` — the columns hold no empty strings today.

No `nchar` columns on `Course` itself (`Certification.Title` is `nchar(100)` and needs
`RTRIM()` in the lookup). No computed columns.

---

## Foreign Keys

- **Partner_pkid** → `Partner.pkid` (NOT NULL)
  - Alias `c.Partner_pkid AS PartnerPkid`; JOIN carries `p.Name AS PartnerName`.
  - Lookup `GET /api/lookups/partners` (exists). Label is already `Name (AppKey)` — three
    partners share a name. 66 options: `[filter]`, no `[virtualScroll]`.
- **CourseGroup_pkid** → `CourseGroup.pkid` (nullable — 「無」 via `showClear`)
  - Alias `CourseGroupPkid`; LEFT JOIN carries `g.Description AS CourseGroupDescription`.
  - Lookup `GET /api/lookups/course-groups` (exists), ordered `Description ASC`. 215
    options: `[filter]` **and** `[virtualScroll]`.
  - `sample1` suggested `pkid ASC` here; the shipped lookup orders by `Description` because
    pkid order across 215 reference rows is meaningless. Reconciled in favour of the
    shipped endpoint.
- **PublishStatus_pkid** → `PublishStatus.pkid` (NOT NULL)
  - Alias `PublishStatusPkid`; JOIN carries `s.Description AS PublishStatusDescription`.
  - Lookup `GET /api/lookups/publish-statuses` (exists), 3 options.

**Resolved labels travel on the row, not via a client-side lookup.** The invocation asked
for `partner.name` / `courseGroup.description` / `publishStatus.description` resolved by
JOIN. The three description columns are flat aliased properties on `Course`
(`PartnerName`, `CourseGroupDescription`, `PublishStatusDescription`) rather than
multi-mapped nav objects — one round trip, no `splitOn`, and the list needs no lookup
calls to render. The lookups are still loaded, but only for the filter drawer and form.

---

## Foreign-Primary Links

In list, detail and form pages, the resolved label links to the parent's detail page:

- **Partner_pkid** → `/partners/{partnerPkid}`
- **CourseGroup_pkid** → `/course-groups/{courseGroupPkid}` (only when not null)
- **PublishStatus_pkid** → `/publish-statuses/{publishStatusPkid}`

All three routes exist, so these ship as real `routerLink`s (the first feature where the
outbound links are live).

---

## Primary-Foreign Links

- **CourseFAQ** (`Course_pkid`, NO_ACTION) — 對應課程問答, `pi pi-question-circle`,
  `/course-faqs?coursePkid={pkid}`
- **CourseRelatedLink** (`Course_pkid`, NO_ACTION) — 對應相關連結, `pi pi-link`,
  `/course-related-links?coursePkid={pkid}`
- **HotCourse** (`Course_pkid`, NO_ACTION) — 對應熱門課程, `pi pi-star`,
  `/hot-courses?coursePkid={pkid}`
- **CourseRecomm** (`CourseId` / `RecommCourseId`, **no FK**) — 對應推薦課程,
  `pi pi-thumbs-up`, `/course-recomms?courseId={courseId}` (param is the *string* CourseId,
  because that is the column the table keys on)

`sample1` also lists `ClassSection`; no such table exists in `database/*.sql`, so it is
dropped.

**Deferred:** all four routes are dead. Counts render as numbers in the detail page
(and as one summed **使用中** figure nowhere — the list is already 14 columns wide, so
usage appears only in detail). Same call as `Partner`.

---

## N-N Relationships

### CourseInCertification — Course ↔ Certification

- Request field `CertificationPkids: List<int>`; `Course.CertificationPkids` populated on
  `GetByIdAsync` only (list rows do not carry them — 1084 × 2 extra queries is not worth
  it for columns the list does not show).
- Form: `p-multiselect`, `optionLabel="label"`, `optionValue="pkid"`, `appendTo="body"`,
  `[filter]`, `[maxSelectedLabels]="9999"`. 39 options — no virtual scroll.
- Detail: chips of the resolved labels, resolved client-side from the same lookup.
- Sync:
  ```sql
  DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid;
  INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @CertificationPkid); -- per id
  ```

### CourseJobCategories — Course ↔ JobCategory

- Request field `JobCategoryPkids: List<short>`; same load/display/sync pattern.
- 18 options — `[filter]`, no virtual scroll.
- Sync against `CourseJobCategories (Course_pkid, JobCategory_pkid)`.

Both syncs run in the **same transaction** as the parent INSERT/UPDATE. Distinct ids are
enforced before insert (composite PK would otherwise raise on a repeated id).

---

## Query Filters

- **keyword**: LIKE on `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`.
  The eight description blocks are excluded (`nvarchar(4000)`/`max`).
- **partnerPkid** (`short?`): exact match; `GET /api/lookups/partners`.
- **courseGroupPkid** (`short?`): exact match; `GET /api/lookups/course-groups`.
- **publishStatusPkid** (`byte?`): exact match; `GET /api/lookups/publish-statuses`.
- **scheduleOnFrom / scheduleOnTo** (`DateOnly?`): inclusive range on `ScheduleOn`.
- **scheduleOffFrom / scheduleOffTo** (`DateOnly?`): inclusive range on `ScheduleOff`.
- **canRepeat** (`bool?`): tri-state via `p-select` (null = no filter), as in `PublishStatus`.

All three selects live in a drawer → `appendTo="body"`.

### Incoming query params

The three parent specs recorded these contracts; the list honours them, overriding any
saved filter on arrival:

| Param | Set by |
|-------|--------|
| `partnerPkid` | `spec/course/Partner.md` |
| `courseGroupPkid` | `spec/course/CourseGroup.md` |
| `publishStatusPkid` | `spec/admin/PublishStatus.md` |

The link *buttons* on those three detail pages are still not built — that is a follow-up
on each of those features, now unblocked.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | `PartnerLookup[]` |
| `GET /api/lookups/course-groups` | Exists | `CourseGroupLookup[]` |
| `GET /api/lookups/publish-statuses` | Exists | `PublishStatusLookup[]` |
| `GET /api/lookups/certifications` | **New** | `CertificationLookup[]` — `{ pkid, title, partnerName, label }`, `Title` **RTRIM'd** (`nchar(100)`), label `Title (PartnerName)`, ordered `Partner.DisplayOrder, Partner.Name, Title` |
| `GET /api/lookups/job-categories` | **New** | `JobCategoryLookup[]` — `{ pkid, description, label }`, ordered `pkid ASC` (no `DisplayOrder` column; pkid order is the house order) |
| `GET /api/lookups/courses` | **New** | `CourseLookup[]` — `{ pkid, courseId, title, label }`, label `CourseId Title`, ordered `CourseId ASC`. Published for the future `CourseFAQ` / `CourseRelatedLink` / `HotCourse` forms. **1084 options → consumers need `[filter]` + `[virtualScroll]`.** |

Certification label carries the partner name because titles like `FCP-PCS` are opaque on
their own and the list is ordered by partner; all 39 titles are distinct, so the partner
suffix is context, not disambiguation.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all, `CourseId ASC`. No junction ids. |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id:int}` | Get by pkid — **includes** `certificationPkids`, `jobCategoryPkids` |
| `POST` | `/api/courses` | Create — 409 on duplicate `CourseId` |
| `PUT` | `/api/courses` | Update (pkid from body); `CourseId` in body is ignored |
| `DELETE` | `/api/courses/{id:int}` | 409 if any of the four counts is non-zero |
| `POST` | `/api/courses/{id:int}/copy` | Copy — see below |

### Copy action

`POST /api/courses/{id}/copy`, body `{ "newCourseId": "..." }` → `201` with the new
`Course`, `Location` pointing at it.

- `404` if the source does not exist; `400` if `newCourseId` is blank or > 50 chars;
  `409` if `newCourseId` is already taken.
- Copies every scalar column verbatim (including `PublishStatus_pkid`, `FriendlyUrl`,
  `DisplayOrder`) and both junction sets, in one transaction.
- Kept from `sample1` because the data supports it: 124 duplicate `Title` groups and
  triplets like `.NET Framework核心程式設計` ×3 are what cloning-then-tweaking produces.
  Nothing else from `sample1`'s extras (QR code, print, sub-panels, ClassSection button)
  is built — the tables behind them do not exist.

### Status codes

| Situation | Response |
|-----------|----------|
| Any required field blank / over max length | 400 (DataAnnotations) |
| `DisplayOrder` outside 0–9999, `Hour` outside 0–32767, `ListPrice` outside 0–999999999, `LearningCredit` outside 0–99999999.9 | 400 |
| Update with `Pkid` <= 0 | 400 `ValidationProblem` |
| Update / Delete / Copy unknown pkid | 404 |
| Create or Copy with a `CourseId` that exists (case-insensitive) | **409 — 簡介代碼重複** |
| Delete while any count > 0 | 409 — 課程使用中 |
| Duplicate `Title` / `ProdCourseId` / `FriendlyUrl` | allowed |
| `ScheduleOff < ScheduleOn` | allowed by the API (no `CHECK`); the form blocks it |

Range attributes are application-level narrowings to the live data (as with
`Partner.DisplayOrder`); SQL would accept negative values.

---

## Backend Notes

### Models

```csharp
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = "";
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = "";
    public string ProdCourseId { get; set; } = "";
    public string FriendlyUrl { get; set; } = "";
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public string PartnerName { get; set; } = "";               // JOIN Partner
    public short? CourseGroupPkid { get; set; }
    public string? CourseGroupDescription { get; set; }         // LEFT JOIN CourseGroup
    public byte PublishStatusPkid { get; set; }
    public string PublishStatusDescription { get; set; } = "";  // JOIN PublishStatus
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }
    public int FaqCount { get; set; }
    public int RelatedLinkCount { get; set; }
    public int HotCourseCount { get; set; }
    public int RecommCount { get; set; }                        // CourseRecomm — no FK
    public List<int> CertificationPkids { get; set; } = [];     // GetById only
    public List<short> JobCategoryPkids { get; set; } = [];     // GetById only
}

public class CourseRequest
{
    public int Pkid { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = "";
    [MaxLength(300)] public string? OfficialTitle { get; set; }
    [Required, MaxLength(50)] public string CourseId { get; set; } = "";   // ignored on update
    [Required, MaxLength(50)] public string ProdCourseId { get; set; } = "";
    [Required, MaxLength(100)] public string FriendlyUrl { get; set; } = "";
    [Range(0, 9999)] public int DisplayOrder { get; set; }
    [Range(1, short.MaxValue)] public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    [Range(1, byte.MaxValue)] public byte PublishStatusPkid { get; set; }
    [Required] public DateOnly? ScheduleOn { get; set; }
    [Required] public DateOnly? ScheduleOff { get; set; }
    [Range(0, short.MaxValue)] public short Hour { get; set; }
    [Range(0, 999_999_999)] public decimal ListPrice { get; set; }
    [Range(0, 99_999_999.9)] public decimal LearningCredit { get; set; }
    [MaxLength(500)] public string? Material { get; set; }
    [MaxLength(4000)] public string? Objective { get; set; }
    [MaxLength(500)] public string? Target { get; set; }
    [MaxLength(4000)] public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    [MaxLength(4000)] public string? Note { get; set; }
    [MaxLength(4000)] public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}

public class CourseQuery
{
    public string? Keyword { get; set; }
    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }
    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
    public bool? CanRepeat { get; set; }
}

public class CourseCopyRequest
{
    [Required, MaxLength(50)] public string NewCourseId { get; set; } = "";
}
```

`ScheduleOn` / `ScheduleOff` are `DateOnly?` with `[Required]` so a missing date is a 400
rather than silently binding to `0001-01-01`. `PartnerPkid` / `PublishStatusPkid` use
`[Range(1, …)]` for the same reason — `0` is "not supplied".

### SQL — SELECT

```sql
SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder,
       c.Partner_pkid       AS PartnerPkid,       p.Name        AS PartnerName,
       c.CourseGroup_pkid   AS CourseGroupPkid,   g.Description AS CourseGroupDescription,
       c.PublishStatus_pkid AS PublishStatusPkid, s.Description AS PublishStatusDescription,
       c.ScheduleOn, c.ScheduleOff, c.[Hour], c.ListPrice, c.LearningCredit,
       c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline, c.TowardCertOrExam,
       c.Note, c.OtherInfo, c.CanRepeat,
       (SELECT COUNT(*) FROM CourseFAQ         f WHERE f.Course_pkid = c.pkid) AS FaqCount,
       (SELECT COUNT(*) FROM CourseRelatedLink l WHERE l.Course_pkid = c.pkid) AS RelatedLinkCount,
       (SELECT COUNT(*) FROM HotCourse         h WHERE h.Course_pkid = c.pkid) AS HotCourseCount,
       (SELECT COUNT(*) FROM CourseRecomm      r WHERE r.CourseId = c.CourseId
                                                    OR r.RecommCourseId = c.CourseId) AS RecommCount
FROM Course c
JOIN      Partner       p ON p.pkid = c.Partner_pkid
LEFT JOIN CourseGroup   g ON g.pkid = c.CourseGroup_pkid
JOIN      PublishStatus s ON s.pkid = c.PublishStatus_pkid
```

`ORDER BY c.CourseId ASC` — unique, so no tie-break is needed. `DisplayOrder` was
rejected as the default: 65 distinct values across 1084 rows make it a per-partner
ordinal, meaningless globally.

`GetByIdAsync` runs two extra queries on the same connection for the junction ids.

### SQL — INSERT (transaction)

```sql
INSERT INTO Course (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
    Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, [Hour],
    ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
    TowardCertOrExam, Note, OtherInfo, CanRepeat)
VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
    @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
    @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
    @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);
-- then the two junction syncs, then COMMIT
```

### SQL — UPDATE (transaction)

Every writable column **except `CourseId`**, `WHERE pkid = @Pkid`; rows-affected 0 → 404
before any junction work. Then the two junction syncs.

### SQL — COPY (transaction)

```sql
INSERT INTO Course (…same column list…)
SELECT Title, OfficialTitle, @NewCourseId, ProdCourseId, FriendlyUrl, DisplayOrder, …, CanRepeat
FROM Course WHERE pkid = @SourcePkid;
SELECT CAST(SCOPE_IDENTITY() AS int);
INSERT INTO CourseInCertification (Course_pkid, Certification_pkid)
    SELECT @NewPkid, Certification_pkid FROM CourseInCertification WHERE Course_pkid = @SourcePkid;
INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid)
    SELECT @NewPkid, JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @SourcePkid;
```

### SQL — guards

```sql
-- CourseIdExistsAsync (index is case-insensitive; so is this)
SELECT CASE WHEN EXISTS (SELECT 1 FROM Course WHERE CourseId = @CourseId
                          AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)) THEN 1 ELSE 0 END;

-- IsInUseAsync
SELECT CASE WHEN EXISTS (SELECT 1 FROM CourseFAQ         WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM CourseRelatedLink WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM HotCourse         WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM CourseRecomm r JOIN Course c ON c.pkid = @Pkid
                         WHERE r.CourseId = c.CourseId OR r.RecommCourseId = c.CourseId)
            THEN 1 ELSE 0 END;
```

### Repository interface

```csharp
public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken ct = default);
    Task<Course?> GetByIdAsync(int pkid, CancellationToken ct = default);
    Task<int> CreateAsync(CourseRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);
    Task<int?> CopyAsync(int sourcePkid, string newCourseId, CancellationToken ct = default); // null = source missing
    Task<bool> CourseIdExistsAsync(string courseId, int? excludePkid = null, CancellationToken ct = default);
    Task<bool> IsInUseAsync(int pkid, CancellationToken ct = default);
}
```

Register: `builder.Services.AddScoped<ICourseRepository, CourseRepository>();`

### Special Column Notes

- `[Hour]` is bracketed in SQL — `HOUR` is a T-SQL datepart name; bracketing avoids any
  ambiguity even though it is not reserved.
- `LearningCredit` is `decimal(9,1)`; 387 rows carry a fraction. The form input allows one
  decimal place. `ListPrice` is `decimal(9,0)` — integer input.
- `Outline` / `TowardCertOrExam` are `nvarchar(max)` with no `[MaxLength]`; 112 outlines
  contain HTML. Rendered as **text** (`white-space: pre-wrap`), never `[innerHTML]`.
- `OtherInfo` is null on all 1084 rows. It stays in the form as an optional textarea (the
  column exists; the schema wins) but is placed last.
- `DEFAULT` constraints on `Hour` / `ListPrice` / `LearningCredit` / `CanRepeat` match C#
  defaults (0 / 0 / 0 / false); the INSERT always supplies them explicitly.

### RowAudit

Not implemented — no `RowAuditWriter` exists in the codebase (see *Gaps* in `docs/claude/adding-a-feature.md`).
The `RowAudit` table has 0 rows.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/courses` | `CourseList` | 課程 Course |
| `/courses/new` | `CourseForm` | 新增課程 |
| `/courses/:id/edit` | `CourseForm` | 編輯課程 |
| `/courses/:id` | `CourseDetail` | 課程明細 |

### Angular model

`Course`, `CourseRequest`, `CourseQuery` (+ `EMPTY_COURSE_QUERY`), `CourseCopyRequest`,
`CourseLookup`, `CertificationLookup`, `JobCategoryLookup` in `core/models/course.model.ts`
(certification and job-category lookups are only consumed here, so they live alongside).
Dates are ISO `string`s on the wire.

### date.util.ts (new)

`toIsoDate(d: Date): string` using local components; `parseIsoDate(s: string): Date`
constructing `new Date(y, m-1, d)`; `addYears(d, n)`.

### List component

Columns, in the order supplied with the invocation:

主代碼 · 顯示順序 · 簡介代碼 · 科目代碼 · 課程名稱 · 原廠 · 課程群組 · 上架狀態 · 上架日期 ·
下架日期 · 時數 · 定價 · 點數 · 允許重聽 · 操作

- 原廠 / 課程群組 / 上架狀態 render the JOINed label and `routerLink` to the parent detail.
- 14 data columns: the table wrapper scrolls horizontally (`overflow-x: auto`); the page
  body never does.
- Default sort `courseId ASC`; 1084 rows → default `rows: 20`, options `[10, 20, 50, 100]`.
- Filter drawer: 關鍵字, 原廠 (`p-select`, filter), 課程群組 (`p-select`, filter + virtual
  scroll), 上架狀態 (`p-select`), 上架日期 from/to, 下架日期 from/to (`p-datepicker`),
  允許重聽 (tri-state `p-select`). Lookups via `forkJoin` on init; saved filters restored
  after lookups load; incoming `partnerPkid` / `courseGroupPkid` / `publishStatusPkid`
  query params override the saved filter.
- Row actions: 檢視 / 編輯 / **複製** / 刪除. 複製 opens a `p-dialog` showing
  `courseId title` and one input for the new 簡介代碼; on success navigates to the new
  course's detail page. 409 → 簡介代碼「X」已存在。

### Detail component

Three cards: 基本資料 (identity, FK labels as links, dates, numbers, 允許重聽), 課程內容
(the eight text blocks, `pre-wrap`, `—` when null), 關聯 (認證 chips, 職務類別 chips, then
the four usage counts with the 尚未被任何… note when all are zero).

### Form component

Reactive Forms; `forkJoin` of partners, course-groups, publish-statuses, certifications,
job-categories (+ the course in edit mode). Sticky toolbar with 取消 / 儲存.

| Field | Control | Notes |
|-------|---------|-------|
| 主代碼 | read-only, edit only | IDENTITY |
| 簡介代碼 | `input[pInputText]` | required, max 50, **disabled in edit** (CourseRecomm keys on it) |
| 科目代碼 | `input[pInputText]` | required, max 50 |
| 課程名稱 | `input[pInputText]` | required, max 200 |
| 官方課程名稱 | `input[pInputText]` | optional, max 300 |
| 友善網址 | `input[pInputText]` | required, max 100 |
| 原廠 | `p-select` | required, filter, appendTo body |
| 課程群組 | `p-select` | optional, showClear, filter + virtualScroll |
| 上架狀態 | `p-select` | required |
| 上架日期 | `p-datepicker` | required; in add mode sets 下架日期 = +10y (`{ emitEvent: false }`) |
| 下架日期 | `p-datepicker` | required; group validator `scheduleOff >= scheduleOn` |
| 顯示順序 | `p-inputnumber` | 0–9999, default 0 |
| 時數 | `p-inputnumber` | 0–32767, default 0 |
| 定價 | `p-inputnumber` | integer, 0–999999999, default 0 |
| 點數 | `p-inputnumber` | 1 decimal, default 0 |
| 允許重聽 | `p-checkbox` (binary) | default false |
| 對應認證 | `p-multiselect` | chips wrap |
| 職務類別 | `p-multiselect` | chips wrap |
| 教材 … 其他資訊 | `textarea[pTextarea]` | optional; blank → `null` |

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId}」？
```

409 toast: 此課程已被課程問答、相關連結、熱門課程或推薦課程使用，無法刪除。

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-list-filters` | `CourseQuery` (dates as ISO strings) |
| `course-list-sort` | `{ sortField, sortOrder }` |
| `course-list-page` | `{ first, rows }` |

### Sidebar placement

Existing group 課程管理 Course, **first** entry (the master entity leads its group):

```ts
{ label: '課程 Course', icon: 'pi pi-book', route: '/courses' },
{ label: '合作廠商 Partner', … },
{ label: '課程群組 CourseGroup', … },
```

`app.spec.ts` asserts an exact href list — updated in the same commit.

---

## Tests

### Backend (`CMS.API.Tests`)

- `FakeCourseRepository` mirrors the SQL: keyword across five columns, exact FK matches,
  inclusive date ranges, tri-state `CanRepeat`, `CourseId ASC` ordering, case-insensitive
  `CourseId` uniqueness, junction ids returned only from `GetByIdAsync`, `CourseId` frozen
  on update, copy cloning both junction sets, the four counts and the guard.
- `CourseApiFactory` seeds: a referenced course (FAQ + links), a course referenced **only
  by `CourseRecomm`** (the no-FK case), an unreferenced course with a null `CourseGroup`,
  and a course with certifications + job categories; two rows share a `Title` and a
  `FriendlyUrl`.
- `CoursesControllerTests`: ordering, every filter, `GetById` with junction ids,
  `GetAll` without them, 400s (blank required, overlong `FriendlyUrl` 101, `PartnerPkid` 0,
  missing date, negative `Hour`), duplicate `CourseId` 409 (exact and case-variant),
  duplicate `Title` 201, update ignores `CourseId` change, update syncs junctions, delete
  204 / 409 (FK) / 409 (Recomm only) / 404, copy 201 with junctions / 409 / 404 / 400.
- `LookupsControllerTests`: certifications (RTRIM'd, composed label on the wire, partner
  order), job-categories (pkid order), courses (label on the wire).

### Frontend (`CMS.NG`)

- `course.service.spec.ts` — every method's URL/verb, copy body, 409 surfacing.
- `lookup.service.spec.ts` — the three new endpoints.
- `date.util.spec.ts` — local-component ISO round-trip (no UTC shift), `addYears`.
- `course-list.spec.ts` — rows/columns, JOINed labels rendered, filter persistence,
  incoming `partnerPkid` param overrides saved filter, delete + 409 wording, copy dialog
  calls the service and navigates.
- `course-detail.spec.ts` — fields, links, chips resolved from lookups, null renders `—`,
  unused note only when all four counts are zero (Recomm-only is *not* unused).
- `course-form.spec.ts` — add title / no pkid; `CourseId` enabled in add, disabled in
  edit; +10y auto-default in add only; `scheduleOff < scheduleOn` blocks save; blank
  textareas → `null`; junction ids in the request; 409 message; cancel targets.
- `app.spec.ts` — href list gains `/courses` first in the group.

---

## Files

| Area | Path | Action |
|------|------|--------|
| Spec | `spec/course/Course.md` | new |
| API | `Models/Course.cs`, `CourseRequest.cs`, `CourseQuery.cs`, `CourseCopyRequest.cs` | new |
| API | `Models/LookupItem.cs` | add `CertificationLookup`, `JobCategoryLookup`, `CourseLookup` |
| API | `Repositories/ICourseRepository.cs`, `CourseRepository.cs` | new |
| API | `Repositories/ILookupRepository.cs`, `LookupRepository.cs` | add three lookups |
| API | `Controllers/CoursesController.cs` | new |
| API | `Controllers/LookupsController.cs` | add three routes |
| API | `Program.cs` | register `ICourseRepository` |
| Tests | `FakeCourseRepository.cs`, `CourseApiFactory.cs`, `CoursesControllerTests.cs` | new |
| Tests | `FakeLookupRepository.cs`, `LookupApiFactory.cs`, `LookupsControllerTests.cs` | extend |
| NG | `core/models/course.model.ts`, `core/services/course.service.ts` (+spec) | new |
| NG | `core/utils/date.util.ts` (+spec) | new |
| NG | `core/services/lookup.service.ts` (+spec) | extend |
| NG | `features/courses/course-list/*`, `course-detail/*`, `course-form/*` (+specs) | new |
| NG | `app.routes.ts`, `app.ts`, `app.spec.ts` | extend |
| Docs | `CLAUDE.md` | Course feature section + the hidden-UNIQUE-index and CASCADE traps |
