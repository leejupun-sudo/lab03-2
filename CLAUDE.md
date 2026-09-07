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

### Testing the API

`CMS.API.Tests` hosts the real pipeline with `WebApplicationFactory<Program>` and swaps
only the repository for `FakeAppRoleRepository`. This exercises routing, model binding,
DataAnnotations validation and JSON casing without a database. `Program.cs` ends with
`public partial class Program;` to make that possible — keep it.

Construct a fresh `AppRoleApiFactory` per test; the fake holds mutable state.

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

## Adding a feature

1. Fill in `spec/feature-spec.template.md` for the table; `spec/sample1.spec.md`
   (Course, FK+N-N heavy) and `spec/sample2.spec.md` (SkillTrain, simpler) show the depth expected.
2. Backend: models, `I{Table}Repository` + implementation, controller, then **register
   the repository in `Program.cs`** — a missing registration only fails at request time.
3. Frontend: service in `core/services/`, three components under `features/`, a lazy
   route, and a nav entry in the `navGroups` signal in `app.ts`.
4. Add tests on both sides; the AppRole specs are the reference shape.
