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
| `spec/{sub-system}/`| Real per-table build specs — `admin/PublishStatus.md`, `course/CourseGroup.md` |
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

### Three things the schema will not tell you — check the live DB first

Read-only probes against `.\SQLEXPRESS` are cheap and have caught a real bug in every
feature so far. Run them before writing the repository, not after.

- **A table that is an FK target needs a delete guard.** No table here declares
  `ON DELETE`, so deleting a referenced row raises an FK violation — a `500`, which the
  rule above forbids. Carry usage-count subqueries in every SELECT and gate `DELETE`
  behind an `IsInUseAsync`, returning `409`. Both `PublishStatus` and `CourseGroup` do
  this; copy either.
- **Do not assume a "name" column is unique.** `CourseGroup.Description` has no `UNIQUE`
  constraint and the live table holds duplicates (215 rows, 213 distinct). Adding the
  usual duplicate check there would contradict the schema *and* make the existing twin
  rows uneditable — each would 409 against its own duplicate. Verify with
  `SELECT COUNT(DISTINCT col), COUNT(*)` before writing a `*ExistsAsync`.
- **Not every `pkid` is `IDENTITY`.** `PublishStatus.pkid` is a plain `tinyint`: the
  client supplies it on create, `INSERT` writes it explicitly, there is no
  `SCOPE_IDENTITY()`, and a duplicate is a `409`. Where the PK *is* IDENTITY, cast
  `SCOPE_IDENTITY()` to the column's own type (`smallint` for `CourseGroup`, not `int`).

An FK-target PK is also **immutable** — never write it in `UPDATE`, and disable the
control in the edit form. Same hazard as `AppRole.RoleId`.

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

One factory + one fake per feature (`AppRoleApiFactory`, `PublishStatusApiFactory`,
`CourseGroupApiFactory`, plus `LookupApiFactory` for `LookupsController`). **Construct a
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

## Gaps between the `/crud` skill and this codebase

The skill's step list is not fully implementable here yet. Do not silently skip these —
say so in the report.

- **RowAudit does not exist.** The skill asks for a `RowAuditWriter` injected into every
  repository and a `RowAuditBadgeComponent` in the detail/form toolbars. `admin.sql` has
  a `RowAudit` *table*, but there is no C# writer and no Angular component, and no
  implemented feature uses either. Building that infrastructure is separate work; until
  then, follow the AppRole shape and note the omission.
- **Primary-Foreign link buttons** need the child feature to exist first. `PublishStatus`
  and `CourseGroup` both ship usage *counts* as plain numbers because `/courses`,
  `/promotion2s` and `/partner-course-groups` are dead routes. The specs record the
  routes and query-param names as the contract to build against.
- **The junction heuristic can be wrong** — see above.

## Adding a feature

1. Read the DDL, then **probe the live DB** for row counts, distinct-vs-total on any
   candidate natural key, and which tables actually reference this one. See
   *Three things the schema will not tell you*.
2. Write `spec/{sub-system}/{Table}.md` from `spec/feature-spec.template.md`.
   `spec/sample1.spec.md` (Course, FK+N-N heavy) and `spec/sample2.spec.md` (SkillTrain,
   simpler) show the depth expected; `spec/admin/PublishStatus.md` and
   `spec/course/CourseGroup.md` are real worked examples. Record every judgment call and
   its reason — that is what the spec is for.
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
