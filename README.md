# CMS

Full-stack CMS scaffolded from the SQL Server schema in `database/*.sql`, following
`spec/code-gen.convention.md`.

| Layer    | Project              | Stack                                          | Local URL               |
| -------- | -------------------- | ---------------------------------------------- | ----------------------- |
| Backend  | `src/CMS.API`        | .NET 9 Web API, Dapper (no EF), Swashbuckle 7.2 | http://localhost:5000   |
| Tests    | `src/CMS.API.Tests`  | xUnit + `WebApplicationFactory`                 | —                       |
| Frontend | `src/CMS.NG`         | Angular 20 standalone, PrimeNG 20               | http://localhost:4200   |

## Prerequisites

- .NET SDK 9 (`src/global.json` pins `9.0.316`, rollForward `latestFeature`)
- Node.js 20.19+/22.12+/24+ and npm
- SQL Server Express with the `CMS` database (`database/*.sql`)
- A `SysConfig` row with `configKey = 'appConfig'` whose JSON carries a `symmetricSecurityKey`
  of **at least 32 bytes** (UTF-8). It signs and validates every access token, so without it
  `POST /api/auth/login` fails and nothing else in the app is reachable — see
  `src/CMS.API/Security/JwtSigningKey.cs`.

## Run

```powershell
# API — Swagger UI at http://localhost:5000/swagger
cd src\CMS.API
dotnet run

# Frontend (separate terminal) — no dev-server proxy, calls the API via environment.ts
cd src\CMS.NG
npm start
```

Open http://localhost:4200 and you land on **`/login`** — the app has no anonymous pages.

## Sign in

Every endpoint except `POST /api/auth/login` requires a bearer token, and 系統管理 requires the
`Admin` role on top of it. That applies to Swagger, curl and any generated client too, not just
the Angular app.

| Item             | Detail                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Accounts         | Rows in the `AppUser` table. Sign in with `UserId` (case-insensitive) + password |
| Seed account     | `admin@example.com`, holds the `Admin` role. Its default password is recorded in `spec/auth/Login.md` |
| Password storage | `PasswordHash` = lowercase hex `SHA256(UTF8(password))` — see `src/CMS.API/Security/PasswordHasher.cs` |
| Token lifetime   | 24 hours, HMAC-SHA256, signed with `SysConfig.appConfig.symmetricSecurityKey` |
| Browser storage  | **Session** storage under `cms-auth` — closing the tab signs you out       |
| Wrong credential | `401` with one generic 帳號或密碼錯誤。 for every cause (no account, wrong password, disabled account) |
| Missing role     | `403`, and the app shows a 權限不足 toast without signing you out          |

Deactivating an account (unticking 啟用) or deleting it takes effect on that person's **next
request** — the account is re-read from the database on every authenticated call.

**Calling the API by hand:**

```powershell
# 1. Get a token
$r = Invoke-RestMethod http://localhost:5000/api/auth/login -Method Post `
     -ContentType 'application/json' `
     -Body '{"userId":"admin@example.com","password":"<password>"}'

# 2. Send it on every other call
Invoke-RestMethod http://localhost:5000/api/courses -Headers @{ Authorization = "Bearer $($r.accessToken)" }
```

In Swagger UI, paste the same token into the **授權 / Authorize** box once and it rides along on
every subsequent request.

## Test

```powershell
cd src
dotnet test CMS.sln          # 339 xUnit tests

cd CMS.NG
npm test                     # 468 Karma + Jasmine tests
npx ng test --watch=false --browsers=ChromeHeadless   # single CI run
```

## Configuration

- **Connection string** — `src/CMS.API/appsettings.json` → `ConnectionStrings:CMS`
- **Token signing key** — the `SysConfig` row `configKey = 'appConfig'`, JSON property
  `symmetricSecurityKey`. Read from the database **per request**, never from appsettings, so
  editing the row rotates the key with no restart — and signs out everyone currently working.
- **CORS** — any `localhost` / loopback origin is allowed (see `Program.cs`)
- **API base URL** — `src/CMS.NG/src/environments/`
  - `environment.ts` (production build): `/api`
  - `environment.development.ts` (dev build, swapped in via `fileReplacements`): `http://localhost:5000/api`
  - Imported through the `@env` short-hand path: `import { environment } from '@env';`

### TypeScript path aliases (`src/CMS.NG/tsconfig.json`)

| Alias         | Target                        |
| ------------- | ----------------------------- |
| `@env`        | `src/environments/environment` |
| `@env/*`      | `src/environments/*`          |
| `@app/*`      | `src/app/*`                   |
| `@core/*`     | `src/app/core/*`              |
| `@features/*` | `src/app/features/*`          |

## Features

Each row is list + detail + form unless noted. 系統管理 rows need the `Admin` role; the rest need
only a signed-in user. The build spec is the contract — the schema wins over the mockups.

| Sidebar group | Feature | Routes | API | Build spec |
| ------------- | ------- | ------ | --- | ---------- |
| 首頁 Home | 上稿作業 FeaturedPromoItem | `/featured-promo-items` (one route — 新增／編輯 happen inline in the weekly grid) | `/api/featured-promo-items` | `spec/promotion/FeaturedPromoItem.md` |
| 系統管理 Admin | 使用者 AppUser | `/app-users` | `/api/app-users` | `spec/auth/AppUser.md` |
| 系統管理 Admin | 角色 AppRole | `/app-roles` | `/api/app-roles` | the section below |
| 系統管理 Admin | 發布狀態 PublishStatus | `/publish-statuses` | `/api/publish-statuses` | `spec/admin/PublishStatus.md` |
| 課程管理 Course | 課程 Course | `/courses` | `/api/courses` | `spec/course/Course.md` |
| 課程管理 Course | 合作廠商 Partner | `/partners` | `/api/partners` | `spec/course/Partner.md` |
| 課程管理 Course | 課程群組 CourseGroup | `/course-groups` | `/api/course-groups` | `spec/course/CourseGroup.md` |
| — (header chip) | 我的帳號 My Profile | `/my-profile` | `PUT /api/auth/profile` | `spec/auth/MyProfile.md` |
| — (public) | 登入 Login | `/login` | `POST /api/auth/login` | `spec/auth/Login.md` |

The list route is the entry point; each feature also has `/new`, `/:id` and `/:id/edit` (see
`src/CMS.NG/src/app/app.routes.ts`). Every list route is `POST {base}/query` server-side for
filtered search. Token and role rules for the whole surface: `spec/auth/Authorization.md`.

Two pages deviate from the house shape, both deliberately: 上稿作業 is a weekly grid edited in
place, and 課程 supports double-click inline cell editing on the list with a QR code on the
detail page.

## Feature: 角色 AppRole (worked example)

Sidebar: **系統管理 Admin → 角色 AppRole** — needs the `Admin` role

| Page   | Route                    | Notes                                                          |
| ------ | ------------------------ | -------------------------------------------------------------- |
| List   | `/app-roles`             | Sortable + paginated `p-table`, filter drawer, delete confirm   |
| View   | `/app-roles/:id`         | Read-only detail + assigned users                              |
| Edit   | `/app-roles/:id/edit`    | Reactive form; `RoleId` disabled (immutable natural key)        |
| Add    | `/app-roles/new`         | Reactive form; `PermissionLevel` defaults to 100                |

### API

| Method   | Route                     | Notes                                    |
| -------- | ------------------------- | ---------------------------------------- |
| `GET`    | `/api/app-roles`          | All roles, `ORDER BY RoleId ASC`         |
| `POST`   | `/api/app-roles/query`    | Filtered search (`AppRoleQuery` body)    |
| `GET`    | `/api/app-roles/{id}`     | Single role by `pkid`, includes `userIds` |
| `POST`   | `/api/app-roles`          | Create — `409` on duplicate `RoleId`     |
| `PUT`    | `/api/app-roles`          | Update — `pkid` from body, no route param |
| `DELETE` | `/api/app-roles/{id}`     | Delete role and its `AppUserRole` rows   |
| `GET`    | `/api/lookups/app-users`  | Slim `AppUser` list for the multi-select |

All seven need a bearer token **and** the `Admin` role — `[Authorize(Roles = "Admin")]` sits on
`AppRolesController` and on the `app-users` / `app-roles` actions of `LookupsController`. The rest
of `LookupsController` stays open to any signed-in user, because the course forms need it.

**Query filters:** `keyword` (LIKE on `RoleId` / `RoleName` / `Description`),
`permissionLevelFrom`, `permissionLevelTo` (inclusive range).

### Schema notes

`AppRole` has an `int IDENTITY` column `pkid` **and** a clustered primary key on
`RoleId nvarchar(200)`, which `AppUserRole` references as a foreign key. The API therefore:

- addresses rows by `pkid` (the surrogate key shown in the 主代碼 column);
- treats `RoleId` as immutable after creation, so `UPDATE` never writes it and the edit
  form disables the field;
- keeps the `AppUserRole` N-N in sync with delete-then-reinsert inside the same transaction.

`Description` is `NULL`-able in the schema, so it is optional in the form and sent as `null`
when blank.

### Session storage keys (list page)

`app-role-list-filters`, `app-role-list-sort`, `app-role-list-page`

## Adding the next feature

1. Write the build spec from `spec/feature-spec.template.md`.
2. Backend: `Models/{Table}.cs` + `{Table}Request.cs` + `{Table}Query.cs`,
   `Repositories/I{Table}Repository.cs` + implementation, `Controllers/{TablePlural}Controller.cs`,
   then register the repository in `Program.cs`.
3. Frontend: `features/{table-plural}/{table}-list|-detail|-form/`, a service in
   `core/services/`, a route in `app.routes.ts`, and a nav entry in `app.ts` → `navGroups`.
4. Decide the route's gate. New routes go under the pathless `authGuard` parent in
   `app.routes.ts`, so a signed-in user reaches them by construction; a 系統管理 route goes inside
   the nested `adminGuard` parent **and** its controller needs `[Authorize(Roles = "Admin")]` —
   the guard and the hidden sidebar entry are ergonomics, the API is the boundary.

Longer notes for Claude Code sessions (schema traps, testing, per-feature decisions) live in
`docs/claude/` and are indexed from `CLAUDE.md`.
