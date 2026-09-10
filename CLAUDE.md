# CLAUDE.md

Guidance for Claude Code in this repository. This file holds only what every session
needs. Longer material lives in `docs/claude/` — read the file for the task **before**
starting it; do not guess at its contents:

| Read when you are…                                            | File                              |
| ------------------------------------------------------------- | --------------------------------- |
| adding or scaffolding a feature, or running `/crud`           | `docs/claude/adding-a-feature.md` |
| writing a repository, delete guard, or `*ExistsAsync`         | `docs/claude/schema-traps.md`     |
| writing or changing xUnit / Karma tests                       | `docs/claude/testing.md`          |
| changing an existing feature (its non-obvious decisions)      | `docs/claude/features.md`         |

## What this is

A CMS admin app generated **from the SQL Server schema**, not from an ORM model.
`database/*.sql` is the source of truth for every entity; `spec/code-gen.convention.md`
is the source of truth for how a table becomes code.

| Path                 | Contents                                                          |
| -------------------- | ----------------------------------------------------------------- |
| `database/*.sql`     | Table DDL — `auth.sql`, `admin.sql`, `course.sql`, `promotion.sql` |
| `spec/`              | Codegen convention, feature-spec template, two sample specs, UI mockups |
| `spec/{sub-system}/` | Per-table build specs (`auth/`, `admin/`, `course/`, `promotion/`) |
| `custom/{Table}/`    | Customer-supplied UI specs + mockups a build spec is written *from* |
| `docs/claude/`       | Reference notes for Claude (table above)                           |
| `src/CMS.API`        | .NET 9 Web API, Dapper (no EF), port 5000                          |
| `src/CMS.API.Tests`  | xUnit endpoint tests                                               |
| `src/CMS.NG`         | Angular 20 standalone + PrimeNG 20, port 4200                      |

`README.md` is for humans running the app. Implemented features: AppRole, AppUser,
PublishStatus, CourseGroup, Partner, Course, FeaturedPromoItem. Plus three non-CRUD ones:
`POST /api/auth/login` (spec `spec/auth/Login.md`); JWT authorization end-to-end — every endpoint
but login requires a bearer token, and Angular has a login page, an HTTP interceptor, a route
guard and a role-gated sidebar (spec `spec/auth/Authorization.md`); and 我的帳號 My Profile —
`PUT /api/auth/profile` plus the `/my-profile` page, where a signed-in user renames themselves
(spec `spec/auth/MyProfile.md`).

## Environment gotchas (Windows)

Properties of this machine, not the repo — they cost time if rediscovered.

- **Node is not on PATH.** Prefix: `$env:PATH = "C:\Program Files\nodejs;$env:PATH"`.
- **`Start-Process npx` fails** ("%1 is not a valid Win32 application"). Run the CLI via
  node: `Start-Process "C:\Program Files\nodejs\node.exe" -ArgumentList "node_modules\@angular\cli\bin\ng.js","serve"`.
- **Headless Karma needs** `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`.
- **`gh` is not on PATH**: `C:\Program Files\GitHub CLI\gh.exe` (authed as `leejupun-sudo`,
  `repo` + `workflow`).
- **Two .NET SDKs (9 and 10).** `src/global.json` pins 9 — keep it (classic `.sln`, `net9.0`).
- **`python` on PATH is the WindowsApps stub** — exits silently. Use Edit or PowerShell.
- **Bash heredocs mangle backslashes** (`\\SQLEXPRESS` → `\SQLEXPRESS`). Use the Write tool
  for JSON/SQL with escapes.
- **Bash tool output is capped (~30 KB)** — use the Read tool for large files.
- **`dotnet` output is zh-TW** — expected.
- **`sqlcmd` mangles Chinese** (and `-u`/`-o` trip over `-E`). Numeric probes:
  `sqlcmd -S .\SQLEXPRESS -d CMS -E -C -h -1 -W`. Readable `nvarchar`: PowerShell
  `New-Object System.Data.SqlClient.SqlConnection("Server=.\SQLEXPRESS;Database=CMS;Integrated Security=true;TrustServerCertificate=true")`.
- **Kill `dotnet run` when done** — a surviving `CMS.API.exe` locks the output and the next
  build fails with MSB3027.

## Commands

```powershell
cd src; dotnet build CMS.sln; dotnet test CMS.sln          # backend
cd src\CMS.NG                                                # frontend (PATH prefix above)
npx ng build                                                 # production
npx ng test --watch=false --browsers=ChromeHeadless          # single CI run
npm start                                                    # dev server, port 4200
```

The API talks to a **real local SQL Server** (`.\SQLEXPRESS`, database `CMS`, compat level
100) with live data. Read-only probes are fine and expected; **do not INSERT/UPDATE/DELETE**
to "verify" an endpoint without asking — the xUnit suite covers writes with in-memory fakes.

## Backend conventions

- **Dapper only.** No EF, no migrations. Repositories take `IDbConnectionFactory` and open a
  connection per method; methods are async, take a `CancellationToken`, and pass it through
  `CommandDefinition`. Multi-statement writes use one transaction.
- **Routes** are kebab-plural (`/api/app-roles`, `/api/lookups/app-users`). `PUT` takes the
  pkid from the body, no route param. Register every repository in `Program.cs`.
- Alias FK columns in SELECT (`c.Partner_pkid AS PartnerPkid`); JOIN FK *labels* onto the row
  as flat columns so lists need no lookup calls. `nchar(n)` needs `RTRIM()`. `DateOnly` /
  `TimeOnly` use the handlers in `Data/DapperTypeHandlers.cs`.
- **Foreseeable conflicts are `409` + `ProblemDetails`, never `500`**: duplicate natural keys,
  taken unique-index slots, and deleting an FK target (`IsInUseAsync` guard with usage
  counts in every SELECT). An FK-target PK is immutable — never in `UPDATE`.
- N-N junctions sync delete-then-reinsert inside the parent transaction — but confirm the
  table *is* a junction first (`docs/claude/schema-traps.md`).
- **Every controller needs a token.** `app.MapControllers().RequireAuthorization()` covers the
  whole surface; `AuthController.Login` holds the only `[AllowAnonymous]` — **on the action, not
  the class**, or it would re-open `PUT /api/auth/profile` beside it. An endpoint that acts on
  "the current user" takes the account from `User.FindFirstValue("userId")`, never from the body,
  and its request DTO simply has no property for one. The bearer signing key is
  re-read from `SysConfig.appConfig.symmetricSecurityKey` **per request**, never from
  appsettings. Details and traps: `docs/claude/features.md`.
- **系統管理 needs the Admin role on top of that token.** `[Authorize(Roles = AppRoles.Admin)]`
  on `AppUsersController`, `AppRolesController`, `PublishStatusesController`, and on the
  `app-users` / `app-roles` **actions** of `LookupsController` (the rest of that class stays open —
  the course forms need it). Content features are deliberately ungated. **Anything that assigns a
  role must be gated in the same change as what the role protects**: `AppRoleRequest.UserIds`
  rewrites `AppUserRole`, so an ungated `PUT /api/app-roles` is a one-request self-grant of Admin.
- **The account is re-read on every authenticated request** (`Security/ActiveAccountEvents.cs`),
  so deactivating or deleting a user takes effect immediately instead of whenever their 24-hour
  token expires. Missing or `IsActive = 0` → `401` (the credential is finished), never `403`.
  Costs one clustered-PK seek per request; do not remove it to save that.
- **Probe the live DB before writing a repository.** The DDL omits unique indexes,
  unconstrained references, cascades and non-IDENTITY keys; every feature so far hit one.
  Checklist and cases: `docs/claude/schema-traps.md`.

## Frontend conventions

- **Standalone components only**; signals for state, Reactive Forms for forms. Feature
  layout `features/{table-plural}/{table}-list|-detail|-form/`; lazy `loadComponent` routes
  in `app.routes.ts` (`/new` before `/:id`); nav entries in the `navGroups` signal in `app.ts`.
- Import config through `@env` (aliases: `@env`, `@app/*`, `@core/*`, `@features/*`).
  **No dev-server proxy** — `environment.development.ts` points at `http://localhost:5000/api`.
- List pages persist `{entity}-list-filters`, `-sort`, `-page` to session storage and honour
  incoming query params over the saved filter.
- PrimeNG: overlays need `appendTo="body"`; `[filter]` past ~10 options, `[virtualScroll]`
  past ~100; tri-state filters use `p-select`, not a checkbox. Autocomplete endpoints (capped
  server search) must not be bound to a `p-select`.
- **Inline cell editing is hand-rolled, not `pEditableColumn`** (Course list is the one
  page with it). That directive opens on a *single* click and its only validity check is a
  synchronous `.ng-invalid` scan, so it cannot express dblclick-to-open or an async
  validated save. Pattern: dblclick opens, blur commits, an invalid value keeps the cell
  open, and the row is never mutated until the write resolves — so closing the editor *is*
  the revert. An inline save that PUTs a parent with junctions must re-read the full row
  first (`docs/claude/features.md`).
- Auth: the profile lives in **session storage** (`cms-auth`), never local storage; one
  interceptor attaches `Authorization: Bearer`, handles 401 (clearing all of session storage and
  returning to `/login`) and 403 (a 權限不足 toast — the session stays, since signing in again
  fixes nothing); one `authGuard` sits on the pathless parent wrapping every route, so new routes
  are covered automatically, and an `adminGuard` sits on a second pathless parent around the
  系統管理 branch. Both guards and the hidden sidebar group are **ergonomics, not the boundary** —
  browser-side roles come from an unverified JWT decode; the API's `[Authorize(Roles)]` is what
  enforces it. Default landing is `/featured-promo-items` (in `app.routes.ts` **and**
  `login.ts` `DEFAULT_LANDING` — keep them equal), because 角色 is Admin-only now. The header user
  chip links to `/my-profile`, a header link and deliberately *not* a sidebar entry. See
  `docs/claude/features.md`.
- **A CommonJS runtime dependency** (the first is `qrcode`) must be listed in
  `angular.json` under `allowedCommonJsDependencies`, or every build warns.
- Dates: `date` columns travel as `yyyy-MM-dd`; convert with `core/utils/date.util.ts`
  (local components, never `toISOString()`).
- Shared primitives (`.cms-card`, `.cms-page-header`, `.cms-field`, `.cms-detail-grid`) live
  in `src/styles.scss`; theme preset in `app.config.ts`. Optional blank text is sent as `null`.
- Delete confirmations: ``確定要刪除主代碼 <b>${item.pkid}</b>「${item.<nameField>}」？``
- Mockups (`spec/*.png`, `custom/`) are style references; **the schema wins on content**
  (nullability, uniqueness), and deviations from the house page shape are recorded in the spec.

## gstack

Skill pack from [garrytan/gstack](https://github.com/garrytan/gstack) (MIT), installed at
`~/.claude/skills/gstack`.

- **All web browsing goes through the `/browse` skill from gstack.** Do not use the
  `mcp__claude-in-chrome__*` tools directly.

Available skills:

`/office-hours`, `/plan-ceo-review`, `/plan-eng-review`, `/plan-design-review`,
`/design-consultation`, `/design-shotgun`, `/design-html`, `/review`, `/ship`,
`/land-and-deploy`, `/canary`, `/benchmark`, `/browse`, `/connect-chrome`, `/qa`, `/qa-only`,
`/design-review`, `/setup-browser-cookies`, `/setup-deploy`, `/setup-gbrain`, `/retro`,
`/investigate`, `/document-release`, `/document-generate`, `/codex`, `/cso`, `/autoplan`,
`/plan-devex-review`, `/devex-review`, `/careful`, `/freeze`, `/guard`, `/unfreeze`,
`/gstack-upgrade`, `/learn`.

## Skill routing

When the user's request matches an available skill, invoke it via the Skill tool. When in doubt, invoke the skill.

Key routing rules:
- Product ideas/brainstorming → invoke /office-hours
- Strategy/scope → invoke /plan-ceo-review
- Architecture → invoke /plan-eng-review
- Design system/plan review → invoke /design-consultation or /plan-design-review
- Full review pipeline → invoke /autoplan
- Bugs/errors → invoke /investigate
- QA/testing site behavior → invoke /qa or /qa-only
- Code review/diff check → invoke /review
- Visual polish → invoke /design-review
- Ship/deploy/PR → invoke /ship or /land-and-deploy
- Save progress → invoke /context-save
- Resume context → invoke /context-restore
- Author a backlog-ready spec/issue → invoke /spec
