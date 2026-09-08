# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A CMS admin app generated **from the SQL Server schema**, not from an ORM model.
`database/*.sql` is the source of truth for every entity; `spec/code-gen.convention.md`
is the source of truth for how a table becomes code. Read both before adding a feature.

| Path                | Contents                                                        |
| ------------------- | --------------------------------------------------------------- |
| `database/*.sql`    | Table DDL — `auth.sql`, `admin.sql`, `course.sql`, `promotion.sql` |
| `spec/`             | Codegen convention, feature-spec template, two worked spec samples, UI mockups |
| `spec/{sub-system}/`| Real per-table build specs — `auth/AppUser.md`, `admin/PublishStatus.md`, `course/CourseGroup.md`, `course/Partner.md`, `course/Course.md` |
| `src/CMS.API`       | .NET 9 Web API, Dapper (no EF), port 5000                        |
| `src/CMS.API.Tests` | xUnit endpoint tests                                             |
| `src/CMS.NG`        | Angular 20 standalone + PrimeNG 20, port 4200                     |

`README.md` documents how to run and configure things for a human; this file covers
what to know before changing code.

## Environment gotchas (Windows)

These cost time if rediscovered — they are properties of this machine, not the repo.

- **Node is not on PATH.** Prefix commands: `$env:PATH = "C:\Program Files\nodejs;$env:PATH"`.
- **`Start-Process npx` fails** with "%1 is not a valid Win32 application". To run the
  dev server detached, invoke the CLI through node:
  `Start-Process "C:\Program Files\nodejs\node.exe" -ArgumentList "node_modules\@angular\cli\bin\ng.js","serve"`.
- **Headless Karma needs `$env:CHROME_BIN`** = `C:\Program Files\Google\Chrome\Application\chrome.exe`.
- **`gh` is not on PATH** but is installed at `C:\Program Files\GitHub CLI\gh.exe` — invoke it
  by full path. It is authenticated as `leejupun-sudo` with `repo` + `workflow` scopes.
- **Two .NET SDKs are installed (9.0.316 and 10.0.302).** `src/global.json` pins 9 so
  `dotnet new sln` yields a classic `.sln` (SDK 10 defaults to `.slnx`) and everything
  targets `net9.0`. Don't remove it.
- **Bash heredocs mangle backslash escapes.** Writing `appsettings.json` via
  `cat <<'EOF'` collapsed `\\SQLEXPRESS` to `\SQLEXPRESS` and produced invalid JSON.
  Use the Write tool for JSON/SQL content containing escapes.
- Chinese output from `dotnet` is expected — the CLI is localised zh-TW.
- **`python` on PATH is the WindowsApps stub** — it exits silently doing nothing, so a
  heredoc script "succeeds" without running. Use the Edit tool or PowerShell instead.
- **`sqlcmd` mangles Chinese on the terminal** (and `-u`/`-o` trip over `-E`). For probes
  that need readable `nvarchar` values, use `System.Data.SqlClient` from PowerShell:
  `New-Object System.Data.SqlClient.SqlConnection("Server=.\SQLEXPRESS;Database=CMS;Integrated Security=true;TrustServerCertificate=true")`.
  Numeric probes are fine through `sqlcmd -S .\SQLEXPRESS -d CMS -E -C -h -1 -W`.

## Commands

```powershell
# Backend
cd src; dotnet build CMS.sln; dotnet test CMS.sln

# Frontend (with the PATH prefix above)
cd src\CMS.NG
npx ng build                                          # production
npx ng test --watch=false --browsers=ChromeHeadless   # single CI run
npm start                                             # dev server, port 4200
```

The API talks to a **real local SQL Server** (`.\SQLEXPRESS`, database `CMS`) that
already contains data. Read-only probes are fine; do not run INSERT/UPDATE/DELETE
against it to "verify" an endpoint without asking first — the xUnit suite covers
writes with an in-memory repository instead.

## Backend conventions

- **Dapper only.** No EF, no migrations. Repositories take `IDbConnectionFactory` and
  open a connection per method.
- **Routes** are kebab-plural: `/api/app-roles`, `/api/lookups/app-users`.
  `PUT` takes the pkid from the body and has no route param.
- Repository methods are async and take a `CancellationToken`; pass it through
  Dapper's `CommandDefinition`.
- N-N junctions sync with **delete-then-reinsert inside one transaction**.
- `nchar(n)` columns need `RTRIM()` in every SELECT. `DateOnly`/`TimeOnly` columns rely
  on the handlers in `Data/DapperTypeHandlers.cs`, registered at the top of `Program.cs`.
- Alias FK columns in SELECT (`c.Partner_pkid AS PartnerPkid`) so Dapper maps them.
- Duplicate natural keys return `409` with a `ProblemDetails` body, not `500`.

### Things the schema will not tell you — check the live DB first

Read-only probes against `.\SQLEXPRESS` are cheap and have caught a real bug in every
feature so far. Run them before writing the repository, not after.

- **A table that is an FK target needs a delete guard.** Almost no FK here declares
  `ON DELETE`, so deleting a referenced row raises an FK violation — a `500`, which the
  rule above forbids. Carry usage-count subqueries in every SELECT and gate `DELETE`
  behind an `IsInUseAsync`, returning `409`. `PublishStatus`, `CourseGroup`, `Partner` and
  `Course` all do this; copy any of them (`Partner` is the five-count example). Check
  `delete_referential_action_desc` in `sys.foreign_keys` first: the two `Course` junctions
  **do** cascade and must *not* be counted, or nothing with a certification could ever be
  deleted.
- **A `UNIQUE` index can exist that the DDL never declares.** `course.sql` shows only
  `PK_Course`, but the live table carries `IX_Course_UniqueCourseId`. A duplicate
  `CourseId` is therefore a *database* violation, not just an application rule — without a
  `CourseIdExistsAsync` the INSERT 500s. Query `sys.indexes WHERE is_unique = 1` for the
  table before deciding which columns get a `*ExistsAsync`.
- **An outbound FK can cascade the wrong way.** `FK_Course_CourseGroup` is
  `ON DELETE CASCADE`: deleting a `CourseGroup` row at the SQL level deletes every course
  in it. Only the `CourseGroup` feature's application guard stands between a stray
  `DELETE` and 1084 courses. Never "simplify" that guard away.
- **Do not assume a "name" column is unique.** `CourseGroup.Description` has no `UNIQUE`
  constraint and the live table holds duplicates (215 rows, 213 distinct). Adding the
  usual duplicate check there would contradict the schema *and* make the existing twin
  rows uneditable — each would 409 against its own duplicate. Verify with
  `SELECT COUNT(DISTINCT col), COUNT(*)` before writing a `*ExistsAsync`.
- **Not every `pkid` is `IDENTITY`.** `PublishStatus.pkid` is a plain `tinyint`: the
  client supplies it on create, `INSERT` writes it explicitly, there is no
  `SCOPE_IDENTITY()`, and a duplicate is a `409`. Where the PK *is* IDENTITY, cast
  `SCOPE_IDENTITY()` to the column's own type (`smallint` for `CourseGroup`, not `int`).

- **A reference can exist with no `FOREIGN KEY` behind it.** `Seminar.Partner_pkid` points
  at `Partner.pkid` across 364 live rows, but `sys.foreign_keys` returns **0** constraints
  for `Seminar` — the DDL declares none. A guard built from the `.sql` files alone misses
  it, and deleting a partner referenced only by `Seminar` succeeds, orphaning the rows
  silently. That is not hypothetical: `Partner` 122 is exactly such a row. Enumerate
  references with `sys.foreign_keys`, then also grep the DDL for `{Table}_pkid` columns the
  constraint list does not cover. **The reference may not even be by pkid:** `CourseRecomm`
  points at courses through the `CourseId` *string* (3118 rows, 895 already orphaned), so
  grep for the natural-key column name as well.

An FK-target PK is also **immutable** — never write it in `UPDATE`, and disable the
control in the edit form. Same hazard as `AppRole.RoleId`, and the same reason
`Course.CourseId` is frozen after creation.

The distinct-vs-total probe cuts both ways: on `Partner` it rules a duplicate check *out*
for `Name` (66 rows / 64 distinct) and *in* for `AppKey` (66/66), in the same table.

### Junction table, or entity in its own right?

"Two FK columns and a name containing the parent table" is not enough to call something
an N-N junction. `PartnerCourseGroup` matches that pattern but has its own
`pkid IDENTITY`, its own payload (`DisplayOrder`, `Description`), and — decisively —
`Promotion2` holds an FK to *its* pkid. Delete-then-reinsert would hand every row a new
pkid and orphan 387 live `Promotion2` rows. Before treating a table as a junction, check
that nothing FKs to it and that it has no surrogate key of its own; a true junction here
(`AppUserRole`, `CourseInCertification`) keys on the FK pair.

### Testing the API

`CMS.API.Tests` hosts the real pipeline with `WebApplicationFactory<Program>` and swaps
only the repository under test for an in-memory fake. This exercises routing, model
binding, DataAnnotations validation and JSON casing without a database. `Program.cs` ends
with `public partial class Program;` to make that possible — keep it.

One factory + one fake per feature (`AppRoleApiFactory`, `AppUserApiFactory`,
`PublishStatusApiFactory`, `CourseGroupApiFactory`, `PartnerApiFactory`, `CourseApiFactory`,
plus `LookupApiFactory` for `LookupsController`). `AppUserApiFactory` swaps a second
repository too — `FakeSysConfigRepository`, so the default password is a known constant and
the tests can assert its SHA-256 reached the user fake. **Construct a
fresh factory per test** — the fakes hold mutable state. Seed the fake with data that
actually exercises the rules: a referenced row so the delete guard has something to block,
and duplicate names where duplicates are legal.

A fake must mirror the SQL, including its ordering — otherwise the test passes while the
endpoint returns rows in the wrong order.

**Computed C# properties need a raw-JSON assertion.** `LookupItem`'s `Label` is a
`get`-only expression, so `ReadFromJsonAsync` recomputes it client-side and an equality
check proves nothing. Parse with `JsonDocument` and read `.GetProperty("label")` to prove
the value actually reaches the Angular `optionLabel="label"` binding.

## Frontend conventions

- **Standalone components only**, no NgModules. Signals for component state;
  Reactive Forms for the form pages.
- Feature layout is `features/{table-plural}/{table}-list|-detail|-form/`.
- Routes are lazy (`loadComponent`) and registered in `app.routes.ts`.
- Import config through the `@env` alias, never a relative path into `environments/`.
  Aliases: `@env`, `@env/*`, `@app/*`, `@core/*`, `@features/*` (`tsconfig.json`).
- **No dev-server proxy.** `environment.development.ts` points at
  `http://localhost:5000/api`; the production `environment.ts` uses `/api`.
- List pages persist `{entity}-list-filters`, `-sort`, `-page` to session storage.
- PrimeNG: `p-select`/`p-multiselect` in overlays need `appendTo="body"`; add
  `[virtualScroll]` past ~100 options.
- Shared page primitives (`.cms-card`, `.cms-page-header`, `.cms-field`,
  `.cms-detail-grid`) live in `src/styles.scss`. Reuse them rather than restyling
  per feature; the theme preset is in `app.config.ts`.
- Delete confirmations use the convention wording:
  ``確定要刪除主代碼 <b>${item.pkid}</b>「${item.<nameField>}」？``

### Testing components

Standard TestBed providers for a PrimeNG page:
`provideRouter([])`, `provideNoopAnimations()`, `providePrimeNG({ theme: { preset: Aura } })`,
`MessageService`, `ConfirmationService`, plus jasmine spies for the data services.
Assert against `data-testid` attributes. Clear `sessionStorage` around list-page specs.

## Feature: 角色 AppRole (implemented)

Sidebar **系統管理 Admin → 角色 AppRole**; routes `/app-roles`, `/app-roles/new`,
`/app-roles/:id`, `/app-roles/:id/edit`.

Two schema decisions that are easy to get wrong:

- `AppRole` has **both** `pkid int IDENTITY` and a clustered primary key on
  `RoleId nvarchar(200)`, which `AppUserRole` references by FK. The API addresses rows
  by `pkid` (the 主代碼 column in the mockups).
- **`RoleId` is immutable after creation** — `UPDATE` never writes it (the repository
  re-reads it from the row to sync `AppUserRole`), and the edit form disables the
  control. Changing it would orphan `AppUserRole` rows.

`Description` is nullable in the schema, so the form treats it as optional and sends
`null` when blank, even though the mockup draws a required asterisk. The UI PNGs in
`spec/` are style references; the schema wins on content.

## Feature: 使用者 AppUser (implemented)

Sidebar **系統管理 Admin → 使用者 AppUser** (first in the group); routes under `/app-users`.
Spec: `spec/auth/AppUser.md`.

- Same two-key shape as `AppRole`: `pkid int IDENTITY` is the API address, the clustered PK
  is **`UserId nvarchar(200)`**, which `AppUserRole` references. `UserId` is 409-guarded on
  create (CI collation, so the check is case-insensitive) and **immutable** after — `UPDATE`
  never writes it, the edit form disables it. `UserName` gets no duplicate check.
- **`PasswordHash` never crosses the API.** It is on no model, request, or Angular
  interface, and no `SELECT` reads it. On create the controller reads
  `SysConfig.configValue WHERE configKey = 'appConfig'`, parses the JSON in C# (compat
  level 100 — no `OPENJSON`), takes `defaultPassword`, and stores
  `PasswordHasher.Sha256Hex()` of it: **SHA-256 over UTF-8, lowercase hex**. That format is
  not a choice — it reproduces the one live row's hash exactly, and any other rendering
  would create accounts the existing login path cannot verify. `UPDATE` never touches it.
- `POST /api/app-users/{id}/reset-password` is the only other writer: same default hash,
  `PasswordUpdatedTime = NULL` (the just-created state; `NULL` reads as "still on the default
  password"). It takes no body. The detail page has a 重設密碼 button behind a confirm.
- A missing `appConfig` row or blank `defaultPassword` throws → **500 on create/reset**. This
  is a deployment fault, not a user conflict, and the one place a 500 is the honest answer.
- `AppUserRole` is a real junction (PK on the FK pair, nothing FKs to it) — synced
  delete-then-reinsert inside the parent transaction, keyed on `UserId`, and deleted with
  the user. No 409 delete guard: the user owns its junction rows, exactly as `AppRole` does.
- Lookup `/api/lookups/app-roles` is new; `value` is **`roleId`** (the natural key the
  junction stores), label `RoleName (RoleId)`, and it carries `pkid` so detail tags can link
  to `/app-roles/{pkid}`.
- Query: keyword on `UserId`/`UserName`, `IsActive` tri-state, `RoleId` via `EXISTS` on the
  junction, and a `PasswordUpdatedTime` date range using `< DATEADD(day, 1, @To)` so the
  To day is inclusive on a `datetime` column. Default sort `UserId ASC`.
- Not guarded, recorded in the spec: nothing stops deleting/deactivating the last active
  user, and every endpoint (reset included) is unauthenticated like the rest of the API.

## Feature: 發布狀態 PublishStatus (implemented)

Sidebar **系統管理 Admin → 發布狀態 PublishStatus**; routes under `/publish-statuses`.
Spec: `spec/admin/PublishStatus.md`.

- **`pkid` is `tinyint` with no `IDENTITY`** — the client picks it. `INSERT` writes it,
  there is no `SCOPE_IDENTITY()`, and a duplicate is `409`. `0` is reserved as the
  "not supplied" sentinel via `[Range(1, 255)]`; SQL would accept it, this is a
  deliberate narrowing.
- `Course` and `Promotion2` both FK to it, so `pkid` is immutable and `DELETE` is guarded.
  Every live row is referenced — the guard is not theoretical.
- The three `bit` flags (`IsDraft`/`IsPublished`/`IsDiscontinued`) are **independent**.
  They happen to be mutually exclusive in the data, but no `CHECK` enforces it, so there
  is no cross-field validator. Tri-state filters use `p-select`, not `p-checkbox` — a
  checkbox cannot express "no filter".

## Feature: 課程群組 CourseGroup (implemented)

Sidebar **課程管理 Course → 課程群組 CourseGroup**; routes under `/course-groups`.
Spec: `spec/course/CourseGroup.md`.

- Two columns only: `pkid smallint IDENTITY` and `Description nvarchar(100) NOT NULL`.
- **No duplicate check on `Description`** — see the backend-conventions note above.
  The only `409` in this feature is the delete guard.
- `PartnerCourseGroup` is **not** an N-N junction — see the junction note above.
- Default sort is `Description ASC`: there is no `DisplayOrder` column and pkid order is
  meaningless across 215 reference rows. This contradicts `spec/sample1.spec.md`, which
  suggests `pkid ASC` for the dropdown; reconcile when the Course feature is built.
- 215 lookup options is past the ~100 threshold, so consumers need `[filter]` and
  `[virtualScroll]`.

## Feature: 合作廠商 Partner (implemented)

Sidebar **課程管理 Course → 合作廠商 Partner** (between Course and CourseGroup); routes under `/partners`.
Spec: `spec/course/Partner.md`.

- `pkid smallint IDENTITY`, immutable — the most-referenced FK target in the schema.
- **`Seminar` references it with no FK constraint.** `SeminarCount` is carried in every
  SELECT and blocks `DELETE` alongside the four enforced references (Course, Certification,
  PartnerCourseGroup, Promotion2). See the schema-traps note above; dropping it as
  "redundant" reintroduces the bug.
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
  would otherwise render as indistinguishable dropdown options. This deviates from
  `spec/sample1.spec.md`; reconcile when the Course feature is built. 66 options needs
  `[filter]` but **not** `[virtualScroll]`.
- The list page shows one summed **使用中** column; the detail page breaks the five counts
  out separately. Link buttons are deferred — `/certifications`, `/partner-course-groups`,
  `/promotion2s` and `/seminars` are dead routes. `/courses?partnerPkid={pkid}` is now live
  and honoured by the Course list, but the 查看課程 button on the Partner detail page has
  not been added yet.

## Feature: 課程 Course (implemented)

Sidebar **課程管理 Course → 課程 Course** (first in the group); routes under `/courses`.
Spec: `spec/course/Course.md` — the longest one, because this table has every trap.

- `pkid int IDENTITY`. **`CourseId` is unique by a live `UNIQUE` index the DDL omits**
  (409 on create, case-insensitive like the column's collation) **and immutable after
  creation** — `CourseRecomm` references courses by that string with no FK. `UPDATE` never
  writes it; the edit form disables it; the update path has no duplicate check because the
  value cannot change. `Title` (124 duplicate groups), `ProdCourseId` (77) and
  `FriendlyUrl` (125) get no duplicate check.
- **Delete guard has four counts, two enforced-FK children excluded.** `CourseFAQ`,
  `CourseRelatedLink` and `HotCourse` are `NO_ACTION` and block; `CourseRecomm` has no FK
  and blocks anyway (Seminar precedent); `CourseInCertification` and `CourseJobCategories`
  **cascade** and are deliberately *not* counted. With `CourseRecomm` included only ~58 of
  1084 courses are deletable — that is the data, recorded in the spec as the thing to
  revisit once a `CourseRecomm` feature exists.
- **The two junctions are real** (composite PK on the FK pair, no surrogate, nothing FKs to
  them) and sync delete-then-reinsert inside the same transaction as the parent write — the
  first transaction in the codebase. `GetByIdAsync` alone returns the id lists; the list
  endpoint does not.
- **FK labels ride on the row.** `PartnerName`, `CourseGroupDescription` (LEFT JOIN) and
  `PublishStatusDescription` are flat aliased columns, not multi-mapped nav objects, so the
  list renders with no lookup calls. Lookups load only for the filter drawer and form.
- Default sort `CourseId ASC` — unique, so a single key. `DisplayOrder` is a per-partner
  ordinal (65 distinct over 1084 rows) and was rejected as the default.
- **Dates.** `DateOnly` on the wire as `yyyy-MM-dd`; `core/utils/date.util.ts` converts with
  local components (never `toISOString()`). The add form defaults 下架日期 to 上架日期 + 10y
  — the two newest live rows follow that rule — but only in add mode, so editing 上架日期
  never clobbers a set 下架日期. `ScheduleOff >= ScheduleOn` is enforced **in the form
  only**: no `CHECK`, and pkid 1980 violates it live, so the API accepts what SQL accepts.
- `Outline` holds HTML in 112 rows and is rendered as text (`white-space: pre-wrap`),
  never `[innerHTML]`. `OtherInfo` is null on every row but stays in the form. Blank
  optional text is stored as `NULL`.
- `POST /api/courses/{id}/copy` clones every column and both junction sets under a new
  `CourseId` in one transaction. Kept from `sample1` because the triplicate titles in the
  data are what clone-then-tweak produces. Nothing else from `sample1`'s extras (QR, print,
  sub-panels, `ClassSection`) exists — `ClassSection` is not in the DDL at all.
- New lookups: `/api/lookups/certifications` (39, `nchar` title **RTRIM'd**, label
  `Title (PartnerName)`), `/api/lookups/job-categories` (18, pkid order) and
  `/api/lookups/courses` (1084 — consumers need `[virtualScroll]`).
- The list honours incoming `partnerPkid`, `courseGroupPkid` and `publishStatusPkid`
  query params over the saved filter, per the three parent specs. The four child routes it
  would link *to* (`/course-faqs`, `/course-related-links`, `/hot-courses`,
  `/course-recomms`) are still dead, so counts render as plain numbers.
- Column labels came with the `/crud` invocation and override `sample1`: 簡介代碼
  (CourseId), 科目代碼 (ProdCourseId), 原廠 (Partner), 上架狀態 (PublishStatus), 點數
  (LearningCredit), 允許重聽 (CanRepeat).

## Gaps between the `/crud` skill and this codebase

The skill's step list is not fully implementable here yet. Do not silently skip these —
say so in the report.

- **RowAudit does not exist.** The skill asks for a `RowAuditWriter` injected into every
  repository and a `RowAuditBadgeComponent` in the detail/form toolbars. `admin.sql` has
  a `RowAudit` *table*, but there is no C# writer and no Angular component, and no
  implemented feature uses either. Building that infrastructure is separate work; until
  then, follow the AppRole shape and note the omission.
- **Primary-Foreign link buttons** need the child feature to exist first. `PublishStatus`,
  `CourseGroup`, `Partner` and `Course` all ship usage *counts* as plain numbers because
  `/certifications`, `/promotion2s`, `/partner-course-groups`, `/seminars`, `/course-faqs`,
  `/course-related-links`, `/hot-courses` and `/course-recomms` are dead routes. The specs
  record the routes and query-param names as the contract to build against. `/courses` is
  now live and accepts `partnerPkid` / `courseGroupPkid` / `publishStatusPkid`, so the
  查看課程 buttons on those three detail pages are unblocked but not yet built. `/app-users`
  is live too; the contract for a 查看使用者 button on `AppRoleDetail` is
  `/app-users?roleId={roleId}` (the natural key), which the user list does not yet read.
- **The junction heuristic can be wrong** — see above.

## Adding a feature

1. Read the DDL, then **probe the live DB** for row counts, distinct-vs-total on any
   candidate natural key, unique indexes, which tables actually reference this one, and
   the `ON DELETE` action of every FK in both directions. See *Things the schema will not
   tell you*.
2. Write `spec/{sub-system}/{Table}.md` from `spec/feature-spec.template.md`.
   `spec/sample1.spec.md` (Course, FK+N-N heavy) and `spec/sample2.spec.md` (SkillTrain,
   simpler) show the depth expected; `spec/admin/PublishStatus.md`,
   `spec/course/CourseGroup.md` and `spec/course/Course.md` are real worked examples.
   Record every judgment call and its reason — that is what the spec is for.
3. Backend: models, `I{Table}Repository` + implementation, controller, then **register
   the repository in `Program.cs`** — a missing registration only fails at request time.
   Add a `GET /api/lookups/{plural}` if anything FKs to this table.
4. Frontend: model + service in `core/`, three components under `features/`, four lazy
   routes (`/new` before `/:id`), and a nav entry in the `navGroups` signal in `app.ts`.
5. Add tests on both sides. Adding a nav entry breaks `app.spec.ts` if it asserts an
   exact href list — update it in the same commit.
6. Verify: `dotnet build`, `dotnet test`, `ng build`, `ng test`, then read-only probes of
   the new endpoints against the live DB. **Kill the `dotnet run` process afterwards** —
   a surviving `CMS.API.exe` locks the output file and the next build fails with MSB3027.
