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

## Run

```powershell
# API — Swagger UI at http://localhost:5000/swagger
cd src\CMS.API
dotnet run

# Frontend (separate terminal) — no dev-server proxy, calls the API via environment.ts
cd src\CMS.NG
npm start
```

## Test

```powershell
cd src
dotnet test CMS.sln          # 19 xUnit tests

cd CMS.NG
npm test                     # 37 Karma + Jasmine tests
npx ng test --watch=false --browsers=ChromeHeadless   # single CI run
```

## Configuration

- **Connection string** — `src/CMS.API/appsettings.json` → `ConnectionStrings:CMS`
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

## Feature: 角色 AppRole

Sidebar: **系統管理 Admin → 角色 AppRole**

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
