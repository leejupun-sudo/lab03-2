# Implemented features — decisions that are easy to get wrong

Read the section for the feature you are touching. Each feature's full reasoning, with the
live-data numbers, is in its `spec/{sub-system}/{Table}.md`; this file is the short list of
things a change must not break.

## 角色 AppRole

Sidebar **系統管理 Admin → 角色 AppRole**; routes `/app-roles`, `/app-roles/new`,
`/app-roles/:id`, `/app-roles/:id/edit`. No spec file (pre-dates the template).

- `AppRole` has **both** `pkid int IDENTITY` and a clustered primary key on
  `RoleId nvarchar(200)`, which `AppUserRole` references by FK. The API addresses rows by
  `pkid` (the 主代碼 column in the mockups).
- **`RoleId` is immutable after creation** — `UPDATE` never writes it (the repository
  re-reads it from the row to sync `AppUserRole`), and the edit form disables the control.
  Changing it would orphan `AppUserRole` rows.
- `Description` is nullable in the schema, so the form treats it as optional and sends
  `null` when blank, even though the mockup draws a required asterisk. The UI PNGs in
  `spec/` are style references; the schema wins on content.

## 使用者 AppUser

Sidebar **系統管理 Admin → 使用者 AppUser** (first in the group); routes under `/app-users`.
Spec: `spec/auth/AppUser.md`.

- Same two-key shape as `AppRole`: `pkid int IDENTITY` is the API address, the clustered PK
  is **`UserId nvarchar(200)`**, which `AppUserRole` references. `UserId` is 409-guarded on
  create (CI collation, so the check is case-insensitive) and **immutable** after — `UPDATE`
  never writes it, the edit form disables it. `UserName` gets no duplicate check.
- **`PasswordHash` never crosses the API.** It is on no model, request, or Angular
  interface, and no `SELECT` reads it. On create the controller reads
  `SysConfig.configValue WHERE configKey = 'appConfig'`, parses the JSON in C# (compat level
  100 — no `OPENJSON`), takes `defaultPassword`, and stores `PasswordHasher.Sha256Hex()` of
  it: **SHA-256 over UTF-8, lowercase hex**. That format is not a choice — it reproduces the
  one live row's hash exactly, and any other rendering would create accounts the existing
  login path cannot verify. `UPDATE` never touches it.
- `POST /api/app-users/{id}/reset-password` is the only other writer: same default hash,
  `PasswordUpdatedTime = NULL` (the just-created state; `NULL` reads as "still on the default
  password"). It takes no body. The detail page has a 重設密碼 button behind a confirm.
- A missing `appConfig` row or blank `defaultPassword` throws → **500 on create/reset**. This
  is a deployment fault, not a user conflict, and the one place a 500 is the honest answer.
- `AppUserRole` is a real junction (PK on the FK pair, nothing FKs to it) — synced
  delete-then-reinsert inside the parent transaction, keyed on `UserId`, and deleted with
  the user. No 409 delete guard: the user owns its junction rows, exactly as `AppRole` does.
- Lookup `/api/lookups/app-roles`: `value` is **`roleId`** (the natural key the junction
  stores), label `RoleName (RoleId)`, and it carries `pkid` so detail tags can link to
  `/app-roles/{pkid}`.
- Query: keyword on `UserId`/`UserName`, `IsActive` tri-state, `RoleId` via `EXISTS` on the
  junction, and a `PasswordUpdatedTime` date range using `< DATEADD(day, 1, @To)` so the To
  day is inclusive on a `datetime` column. Default sort `UserId ASC`.
- Not guarded, recorded in the spec: nothing stops deleting/deactivating the last active
  user, and every endpoint (reset included) is unauthenticated like the rest of the API.

## 發布狀態 PublishStatus

Sidebar **系統管理 Admin → 發布狀態 PublishStatus**; routes under `/publish-statuses`.
Spec: `spec/admin/PublishStatus.md`.

- **`pkid` is `tinyint` with no `IDENTITY`** — the client picks it. `INSERT` writes it, there
  is no `SCOPE_IDENTITY()`, and a duplicate is `409`. `0` is reserved as the "not supplied"
  sentinel via `[Range(1, 255)]`; SQL would accept it, this is a deliberate narrowing.
- `Course` and `Promotion2` both FK to it, so `pkid` is immutable and `DELETE` is guarded.
  Every live row is referenced — the guard is not theoretical.
- The three `bit` flags (`IsDraft`/`IsPublished`/`IsDiscontinued`) are **independent**. They
  happen to be mutually exclusive in the data, but no `CHECK` enforces it, so there is no
  cross-field validator. Tri-state filters use `p-select`, not `p-checkbox` — a checkbox
  cannot express "no filter".

## 課程群組 CourseGroup

Sidebar **課程管理 Course → 課程群組 CourseGroup**; routes under `/course-groups`.
Spec: `spec/course/CourseGroup.md`.

- Two columns only: `pkid smallint IDENTITY` and `Description nvarchar(100) NOT NULL`.
- **No duplicate check on `Description`** (215 rows / 213 distinct). The only `409` in this
  feature is the delete guard — which is load-bearing: `FK_Course_CourseGroup` cascades.
- `PartnerCourseGroup` is **not** an N-N junction — `docs/claude/schema-traps.md`.
- Default sort is `Description ASC`: there is no `DisplayOrder` column and pkid order is
  meaningless across 215 reference rows. This contradicts `spec/sample1.spec.md`, which
  suggests `pkid ASC` for the dropdown.
- 215 lookup options is past the ~100 threshold, so consumers need `[filter]` and
  `[virtualScroll]`.

## 合作廠商 Partner

Sidebar **課程管理 Course → 合作廠商 Partner** (between Course and CourseGroup); routes under
`/partners`. Spec: `spec/course/Partner.md`.

- `pkid smallint IDENTITY`, immutable — the most-referenced FK target in the schema.
- **`Seminar` references it with no FK constraint.** `SeminarCount` is carried in every
  SELECT and blocks `DELETE` alongside the four enforced references (Course, Certification,
  PartnerCourseGroup, Promotion2). Dropping it as "redundant" reintroduces the bug.
- **`Name` gets no duplicate check; `AppKey` does.** 66 rows / 64 distinct names
  (「國際標準課程」 ×3) versus 66/66 distinct AppKeys. The AppKey 409 is an *application*
  rule — no `UNIQUE` index backs it — so re-run the distinct probe before relying on it.
- Default sort is `DisplayOrder ASC, Name ASC, pkid ASC`. All three keys are needed: 23 rows
  share the `9999` "park at the end" sentinel and `Name` is not unique either, so without
  `pkid` the order is undefined and paging is unstable.
- **`ImageFilename` has no format guarantee** — 17 of the 62 non-null values carry no
  extension at all (`Splunk`, `恆逸`, `轉職培訓`). No regex, no `<img src>`; it renders as
  text and stores `null` when blank.
- `PartnerLookup.Label` is `Name (AppKey)`, not bare `Name` — three rows share a name and
  would otherwise render as indistinguishable dropdown options. 66 options needs `[filter]`
  but **not** `[virtualScroll]`.
- The list page shows one summed **使用中** column; the detail page breaks the five counts
  out separately. Link buttons are deferred (dead child routes); `/courses?partnerPkid=` is
  live but the 查看課程 button has not been added yet.

## 課程 Course

Sidebar **課程管理 Course → 課程 Course** (first in the group); routes under `/courses`.
Spec: `spec/course/Course.md` — the longest one, because this table has every trap.

- `pkid int IDENTITY`. **`CourseId` is unique by a live `UNIQUE` index the DDL omits** (409
  on create, case-insensitive like the column's collation) **and immutable after creation**
  — `CourseRecomm` references courses by that string with no FK. `UPDATE` never writes it;
  the edit form disables it; the update path has no duplicate check because the value cannot
  change. `Title` (124 duplicate groups), `ProdCourseId` (77) and `FriendlyUrl` (125) get no
  duplicate check.
- **Delete guard has four counts, two enforced-FK children excluded.** `CourseFAQ`,
  `CourseRelatedLink` and `HotCourse` are `NO_ACTION` and block; `CourseRecomm` has no FK and
  blocks anyway (Seminar precedent); `CourseInCertification` and `CourseJobCategories`
  **cascade** and are deliberately *not* counted. With `CourseRecomm` included only ~58 of
  1084 courses are deletable — recorded in the spec as the thing to revisit once a
  `CourseRecomm` feature exists.
- **The two junctions are real** (composite PK on the FK pair, no surrogate, nothing FKs to
  them) and sync delete-then-reinsert inside the same transaction as the parent write.
  `GetByIdAsync` alone returns the id lists; the list endpoint does not.
- **FK labels ride on the row.** `PartnerName`, `CourseGroupDescription` (LEFT JOIN) and
  `PublishStatusDescription` are flat aliased columns, not multi-mapped nav objects, so the
  list renders with no lookup calls. Lookups load only for the filter drawer and form.
- Default sort `CourseId ASC` — unique, so a single key. `DisplayOrder` is a per-partner
  ordinal (65 distinct over 1084 rows) and was rejected as the default.
- **Dates.** `DateOnly` on the wire as `yyyy-MM-dd`; `core/utils/date.util.ts` converts with
  local components (never `toISOString()`). The add form defaults 下架日期 to 上架日期 + 10y
  — but only in add mode, so editing 上架日期 never clobbers a set 下架日期.
  `ScheduleOff >= ScheduleOn` is enforced **in the form only**: no `CHECK`, and pkid 1980
  violates it live, so the API accepts what SQL accepts.
- `Outline` holds HTML in 112 rows and is rendered as text (`white-space: pre-wrap`), never
  `[innerHTML]`. `OtherInfo` is null on every row but stays in the form. Blank optional text
  is stored as `NULL`.
- `POST /api/courses/{id}/copy` clones every column and both junction sets under a new
  `CourseId` in one transaction. Of `sample1`'s other extras only the QR code exists;
  print, sub-panels and `ClassSection` do not — `ClassSection` is not in the DDL at all.
- **The 基本資料 QR code is a composited canvas, not a bare matrix.** `QrCodeService`
  (`core/services/qr-code.service.ts`, wrapping the `qrcode` package) draws the matrix and
  then the CourseId as a caption strip beneath it; the page displays that canvas and 下載
  saves the same one as `{CourseId}.png`, so preview and download can never drift. It
  encodes `{environment.publicSiteBaseUrl}/Course/Show/{pkid}/{CourseId}` with the CourseId
  segment **percent-encoded** — 15 live CourseIds carry spaces, parentheses or Chinese, and
  those same characters are why the file name is sanitised. `qrcode` is CommonJS and is
  listed in `angular.json` under `allowedCommonJsDependencies`.
- Lookups: `/api/lookups/certifications` (39, `nchar` title **RTRIM'd**, label
  `Title (PartnerName)`), `/api/lookups/job-categories` (18, pkid order) and
  `/api/lookups/courses` (1084 — consumers need `[virtualScroll]`).
- The list honours incoming `partnerPkid`, `courseGroupPkid` and `publishStatusPkid` query
  params over the saved filter. Its four child routes are dead, so counts are plain numbers.
- Column labels came with the `/crud` invocation and override `sample1`: 簡介代碼
  (CourseId), 科目代碼 (ProdCourseId), 原廠 (Partner), 上架狀態 (PublishStatus), 點數
  (LearningCredit), 允許重聽 (CanRepeat).

## 上稿作業 FeaturedPromoItem

Sidebar **首頁 Home → 上稿作業 FeaturedPromoItem** (first group); one route,
`/featured-promo-items`. Spec: `spec/promotion/FeaturedPromoItem.md`, built from the customer
mockups in `custom/FeaturedPromoItem/`. The first feature shaped by a customer UI spec
instead of the `/crud` template, so it breaks several house patterns on purpose:

- **No detail page, no `/new` or `/:id/edit` routes.** The page is a weekly grid (Monday–
  Sunday × slots 1–3, one `p-tabs` tab per `TrainingCenter`) and 新增／編輯 happen in an
  inline `FeaturedPromoItemForm` rendered in place of the slot row. The form is still a
  standalone component with signal inputs (`context`, `item`, `initial`) and is unit-tested
  on its own. Only `-list-filters` is persisted (`{ trainingCenterPkid, weekOf }`); there is
  no sort or page state.
- **Nothing references this table** — the first feature with **no delete guard**. The one
  409 is the declared unique index on (`ScheduleOn`, `TrainingCenter_pkid`, `Slot`):
  `SlotTakenAsync` runs before INSERT and UPDATE (excluding self).
- **The API owns the week.** `POST /query` takes `weekOf` (any day) and
  `FeaturedPromoItemQuery.StartOfWeek` widens it to Monday..next Monday, half-open. Sunday is
  `DayOfWeek` 0 and must go *back* six days. `date.util.ts` `startOfWeek()` has the same rule
  and both are tested against the same dates — keep them in step.
- **`+` means `move-down`** (slot number goes up), `−` means `move-up` — the customer's
  wording, kept in the route names. The swap parks the neighbour on **slot 0** inside one
  transaction so the unique index is never tripped mid-swap; `[Range(1, 3)]` on the request
  is what keeps 0 unreachable. Boundary moves are 409, and the UI disables those buttons.
- **`Topic`/`Description` are the row's own text** (31 625 of 31 715 rows differ from the
  promotion's). The form fills them from the chosen promotion only when they are blank.
  `Validators.required` alone accepts whitespace, so both also carry `pattern(/\S/)`.
- **`/api/lookups/promotion2s?keyword=` is an autocomplete, not a list**: `TOP 20`, newest
  `ScheduleOn` first, `PromoCode LIKE '%kw%'`, and it carries `topic`/`description` for the
  pre-fill. 1157 live rows — never bind it to a `p-select`. `/api/lookups/training-centers`
  is a normal five-row lookup ordered by `DisplayOrder`; pkid 54 線上研討會 has **0** items
  and still gets a tab.
- Copy/Paste is client-side only: an in-memory clipboard of the content (never the key), and
  Paste opens the inline form pre-filled; the write is an ordinary `POST`.
- `GET /api/featured-promo-items` (all ~31 k rows) exists for parity; the UI never calls it.
