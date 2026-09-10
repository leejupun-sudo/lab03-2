# Build Spec for AppUser

- database schema: `.\database\auth.sql` (the identical `AppUser` / `AppUserRole` DDL also
  appears in `.\database\admin.sql`; they are the same tables, dumped twice)

## Summary

`AppUser` is the 使用者 table — the back-office login account. It is the other half of the
`AppRole` feature: the two share the `AppUserRole` junction, and this table has the same
"two keys" shape as `AppRole` (an `IDENTITY` `pkid` the API addresses rows by, and a
`nvarchar` natural key that is the *clustered primary key* and the FK target).

What makes it different from every feature so far is a column that **must never cross the
API boundary**: `PasswordHash`. It is `NOT NULL`, so the backend has to fill it on create
from a system default, and it must stay untouched on update.

| Item | Detail |
|------|--------|
| Primary Key | `PK_AppUser` is clustered on **`UserId nvarchar(200)`**; `pkid int IDENTITY(1,1)` is the API address |
| Foreign Keys | None outbound |
| Required Fields | `UserId`, `UserName`, `IsActive`, `PasswordHash` (server-filled) |
| N-N Relationships | `AppUserRole (UserId, RoleId)` ↔ `AppRole` |
| Primary-Foreign Links | `AppUserRole.UserId` (junction — owned, not guarded) |
| Query Filters | keyword (`UserId`, `UserName`); `IsActive` tri-state; `RoleId` via `AppUserRole`; `PasswordUpdatedTime` date range |
| Default Sort | `UserId ASC` |

### Live data (read-only probe, 2026-09-08)

| Measure | Value |
|---------|-------|
| `AppUser` rows | **1** (`pkid` 14, `admin@example.com`, `IsActive` 1, `PasswordUpdatedTime` NULL) |
| Distinct `UserId` / `UserName` | 1 / 1 — the distinct-vs-total probe is uninformative at n = 1 |
| Unique indexes on `AppUser` | `PK_AppUser (UserId)` only |
| Column collation | `Chinese_Taiwan_Stroke_CI_AS` — `UserId` uniqueness is **case-insensitive** |
| `PasswordHash` shape | `LEN` = **64**, all `[0-9a-f]` — SHA-256 as lowercase hex |
| `AppUserRole` rows | 1 (`admin@example.com` → `Admin`), 0 orphans |
| `AppRole` rows | 2 (`Admin` / 1, `User` / 100) |
| `sys.foreign_keys` referencing `AppUser` | 1 — `FK_AppUserRole_AppUser`, `NO_ACTION` |
| `sys.foreign_keys` referencing `AppUserRole` | **0** |
| Columns named `*UserId*` / `*UserName*` elsewhere | `AppUserRole.UserId` (FK), `RowAudit.UserName` (text snapshot, no reference) |
| `SysConfig` | 1 row, `configKey = 'appConfig'`, `configValue` = JSON with `defaultPassword` (string), `symmetricSecurityKey` (string), `enforcePasswordPolicy` (bool) |
| Database compatibility level | **100** — `OPENJSON`, `STRING_AGG` are *not* available in SQL |

### The hash format is pinned by the live row

`SHA256(UTF8(defaultPassword))` rendered as **lowercase hex** reproduces the stored
`PasswordHash` of the one live row exactly. Uppercase hex, Base64, and UTF-16 input all
fail to match. The backend therefore uses `Convert.ToHexString(...).ToLowerInvariant()` on
UTF-8 bytes — any other rendering would create accounts whose default password does not
work against whatever verifies logins today.

---

## `PasswordHash` never crosses the API

Rules supplied with the `/crud` invocation, recorded here as the contract:

1. `PasswordHash` is **not** on `AppUser` (response), `AppUserRequest`, or any Angular
   model. No `SELECT` reads it into a DTO; it appears only in the `INSERT` and in the
   reset-password `UPDATE`.
2. **CREATE** reads `SysConfig.configValue WHERE configKey = 'appConfig'`, parses the JSON,
   takes `defaultPassword`, hashes it with SHA-256 (lowercase hex, see above) and writes
   that as `PasswordHash`. `PasswordUpdatedTime` is written as `NULL` — the live row was
   created the same way and carries `NULL`, so `NULL` reads as "still on the default
   password".
3. **UPDATE** never writes `PasswordHash` or `PasswordUpdatedTime`.
4. **`POST /api/app-users/{id}/reset-password`** is the only other writer. It re-derives the
   default hash from `SysConfig` and sets `PasswordUpdatedTime = NULL`, restoring the
   just-created state. It takes no body — there is no way to *supply* a password through
   this API. Judgment call: the alternative of stamping `GETDATE()` would make a reset
   indistinguishable from a user-chosen password; `NULL` keeps "on the default" a visible
   fact. Flip it if the login flow turns out to read the column differently.
5. The parsed config is a narrow `AppConfig { DefaultPassword, EnforcePasswordPolicy }`.
   `symmetricSecurityKey` is deliberately **not** modelled so it cannot leak into a log or
   a DTO by accident.
6. A missing `appConfig` row or a blank `defaultPassword` is a deployment fault, not a user
   conflict: `CreateAsync` / reset throw `InvalidOperationException` (→ 500). This is the
   one place where a 500 is the honest answer.

---

## `UserId` is the natural key — unique, case-insensitive, immutable

Same hazard as `AppRole.RoleId`, and the same treatment:

- `UserIdExistsAsync(userId, excludePkid)` backs a **409 `ProblemDetails`** on create. The
  comparison inherits the column collation, so `Admin@Example.com` collides with
  `admin@example.com` — the fake mirrors that with `OrdinalIgnoreCase`.
- `UPDATE` never writes `UserId`; the repository re-reads it from the row to sync
  `AppUserRole`. The edit form disables the control with a hint. Changing it would orphan
  `AppUserRole` rows (`NO_ACTION` FK — SQL would actually reject the UPDATE with a 500).
- The update path has no duplicate check because the value cannot change.

`UserName` gets **no** duplicate check: no `UNIQUE` index, no constraint, and with one live
row there is no evidence either way. Two people can share a display name.

`UserId` is email-shaped in the one live row but the `AppUserRole` test seeds already in
the codebase (`helen`, `Jenny_Tsao`) show it need not be — **no `[EmailAddress]`** rule.

---

## `AppUserRole` is a real junction, and delete cascades through it

`AppUserRole` carries a `pkid IDENTITY` column, which is the trait that made
`PartnerCourseGroup` *not* a junction. It still qualifies here, on the decisive checks:
its primary key is the FK pair `(UserId, RoleId)`, it has no payload columns, and
**nothing references `AppUserRole`** (0 rows in `sys.foreign_keys`, and its only unique
index is the PK). The `AppRole` feature already treats it delete-then-reinsert; this
feature does the same from the other side, keyed on `UserId`.

`DELETE /api/app-users/{id}` therefore has **no 409 guard**. The only inbound reference is
the junction, which the user *owns*: inside one transaction the repository deletes the
`AppUserRole` rows for the `UserId`, then the `AppUser` row. This is exactly what
`AppRoleRepository.DeleteAsync` does for roles. `RowAudit.UserName` is a text snapshot,
not a reference, and has no writer in this codebase.

Not guarded, and recorded as a known gap: nothing stops deleting or deactivating the last
active user, or the caller's own account — there is no authentication in this API yet, so
"the caller" does not exist.

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 後台登入帳號；透過 `AppUserRole` 指派角色

### Chinese Column Names

- pkid: 主代碼
- UserId: 帳號
- UserName: 姓名
- IsActive: 啟用
- PasswordHash: 密碼雜湊 (backend only — never shown)
- PasswordUpdatedTime: 密碼更新時間
- RoleCount (computed): 角色數
- RoleIds (junction): 角色

`UserId` is labelled 帳號 rather than the mechanical 使用者代碼: it is what the person types
to log in, and every live and seeded value is a login name. Column-name hints were not
supplied with the invocation.

---

## Required Fields

Required (NOT NULL):
- `UserId` — `[Required]`, `[MaxLength(200)]`
- `UserName` — `[Required]`, `[MaxLength(200)]`
- `IsActive` — `bool`, defaults to `true` (`DF_AppUser_IsActive` = 1; the form seeds the same)
- `PasswordHash` — server-filled; not in the request

Optional (nullable):
- `PasswordUpdatedTime` — read-only on the wire; never in the request

---

## Foreign Keys

**N/A** — `AppUser` has no outbound foreign keys.

---

## Foreign-Primary Links

**N/A**

---

## Primary-Foreign Links

| Child | Column | Enforced? | Treatment |
|-------|--------|-----------|-----------|
| `AppUserRole` | `UserId` | Yes (`NO_ACTION`) | Junction — synced on write, deleted with the user; **not** a link button, not a guard |

No link button: the child is a junction with no page of its own. The detail page shows the
assigned roles as tags instead (each is a link to `/app-roles/{pkid}` only if the lookup
carries the pkid — it does, see below).

---

## N-N Relationships

**`AppUserRole (UserId, RoleId)`** ↔ `AppRole.RoleId`

- Related entity: `AppRole`; lookup endpoint **`GET /api/lookups/app-roles`** (new).
- **List**: `RoleCount` — a correlated `COUNT(*)` on `AppUserRole`, same as `UserCount` on
  the role side. `STRING_AGG` would let the list show the names inline, but the database
  runs at compatibility level 100, so it is unavailable; a count is what the SQL can do.
- **Detail**: `RoleIds` (only on `GET /{id}`) resolved to `Label` through the lookup,
  rendered as tags — mirrors `AppRoleDetail.assignedUserLabels`.
- **Form**: `p-multiselect` bound to `roleIds`, `optionValue="roleId"`,
  `[maxSelectedLabels]="9999"`. 2 live roles — no `[filter]` needed, but it is harmless and
  matches the role form, so it stays on.
- Request field: `RoleIds: List<string>`.
- Sync (inside the same transaction as the parent INSERT/UPDATE):
  1. `DELETE FROM AppUserRole WHERE UserId = @UserId`
  2. bulk `INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)` for each
     distinct, trimmed, non-blank id (case-insensitive distinct — the PK collation is CI).

A `RoleId` that does not exist in `AppRole` violates `FK_AppUserRole_AppRole` and 500s.
The form only offers lookup values, so this is unreachable from the UI; it is not guarded
server-side, consistent with the role feature.

---

## Query Filters

- **keyword**: `LIKE '%kw%'` on `UserId` **or** `UserName`. Both are short identifying
  columns.
- **IsActive**: tri-state `bool?` — `null` no filter, `true` / `false` exact match.
  `p-select` with 全部／是／否, never a checkbox.
- **RoleId**: `string?` — `EXISTS (SELECT 1 FROM AppUserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @RoleId)`.
  The N-N analogue of an FK dropdown; populated from `/api/lookups/app-roles`.
- **PasswordUpdatedTime range**: `PasswordUpdatedFrom` / `PasswordUpdatedTo`, both
  `DateOnly?` on the wire as `yyyy-MM-dd`. The column is `datetime`, so the SQL is
  `>= @From` and **`< DATEADD(day, 1, @To)`** to keep the To date inclusive. Rows with a
  `NULL` time never match either bound.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-roles` | **New** | `AppRoleLookup { pkid, roleId, roleName, label }`, `ORDER BY RoleId ASC` |
| `GET /api/lookups/app-users` | Exists | Unchanged; consumed by the `AppRole` feature |

`AppRoleLookup.Label` is **`RoleName (RoleId)`** — `Administrator (Admin)` — the same
shape as `AppUserLookup.Label`. `RoleName` has no uniqueness rule, so the id disambiguates.
`Pkid` rides along so the detail tags can link to `/app-roles/{pkid}`.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-users` | List all, `UserId ASC` |
| `POST` | `/api/app-users/query` | Filtered query (body: `AppUserQuery`) |
| `GET` | `/api/app-users/{id:int}` | Get by pkid, **with `RoleIds`** |
| `POST` | `/api/app-users` | Create — 409 on duplicate `UserId`; hashes the default password |
| `PUT` | `/api/app-users` | Update (pkid from body) — never writes `UserId` / `PasswordHash` |
| `DELETE` | `/api/app-users/{id:int}` | Delete, with its `AppUserRole` rows, in one transaction |
| `POST` | `/api/app-users/{id:int}/reset-password` | **Special** — resets to the default password; no body; returns the row |
| `GET` | `/api/lookups/app-roles` | Slim lookup (on `LookupsController`) |

No auth attributes — consistent with every controller so far. That makes
`reset-password` callable by anyone who can reach port 5000; it is no worse than
`DELETE` being open, and the spec records it as something to revisit when auth lands.

### Status codes

| Situation | Response |
|-----------|----------|
| Create/Update with `UserId` or `UserName` blank, or over 200 chars | 400 (DataAnnotations) |
| Update with `Pkid` <= 0 | 400 `ValidationProblem` — 更新時必須提供主代碼 |
| Get/Update/Delete/Reset unknown `pkid` | 404 |
| Create with a `UserId` another row holds (any case) | **409 `ProblemDetails` — 帳號重複** |
| Duplicate `UserName` | 200/201 — allowed |
| `appConfig` missing or `defaultPassword` blank on create/reset | 500 — deployment fault |

---

## Backend Notes

### Models

```csharp
public class AppUser
{
    public int Pkid { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? PasswordUpdatedTime { get; set; }   // read-only on the wire
    public int RoleCount { get; set; }                   // COUNT(*) on AppUserRole
    public List<string> RoleIds { get; set; } = [];      // GET by id only
    // NO PasswordHash property — by contract.
}

public class AppUserRequest
{
    public int Pkid { get; set; }
    [Required(ErrorMessage = "帳號為必填")] [MaxLength(200)] public string UserId { get; set; } = string.Empty;
    [Required(ErrorMessage = "姓名為必填")] [MaxLength(200)] public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<string> RoleIds { get; set; } = [];
}

public class AppUserQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public string? RoleId { get; set; }
    public DateOnly? PasswordUpdatedFrom { get; set; }
    public DateOnly? PasswordUpdatedTo { get; set; }
}

public class AppConfig            // SysConfig 'appConfig' — narrow on purpose
{
    public string? DefaultPassword { get; set; }
    public bool EnforcePasswordPolicy { get; set; }
}
```

`AppRoleLookup` (in `LookupItem.cs`): `Pkid`, `RoleId`, `RoleName`, `Label => $"{RoleName} ({RoleId})"`.

### SysConfig access and hashing

- `ISysConfigRepository.GetAppConfigAsync(ct)` → `SELECT c.configValue FROM SysConfig c
  WHERE c.configKey = 'appConfig'`, deserialised with `PropertyNameCaseInsensitive`.
  Returns `null` when the row is missing.
- `PasswordHasher.Sha256Hex(string)` — static, `SHA256.HashData(Encoding.UTF8.GetBytes(s))`
  → `Convert.ToHexString(...).ToLowerInvariant()`.
- The **controller** composes them: it fetches the config, derives the hash, and passes it
  to `CreateAsync(request, passwordHash)` / `ResetPasswordAsync(pkid, passwordHash)`. The
  repository never reads `SysConfig` itself, so the xUnit tests can seed a known default
  password in a fake and assert the fake user repository received its SHA-256.

### SQL — SELECT

```sql
SELECT u.pkid AS Pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u
-- PasswordHash is never selected.
```

`GetByIdAsync` adds a second query on the same connection:
`SELECT ur.RoleId FROM AppUserRole ur WHERE ur.UserId = @UserId ORDER BY ur.RoleId ASC`.

### SQL — INSERT

```sql
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, NULL);
SELECT CAST(SCOPE_IDENTITY() AS int);
```
then the junction sync, all inside one transaction.

### SQL — UPDATE

```sql
UPDATE AppUser SET UserName = @UserName, IsActive = @IsActive WHERE pkid = @Pkid;
```
`UserId`, `PasswordHash`, `PasswordUpdatedTime` are never written. The repository first
reads `UserId` by pkid (returning `false` → 404 if absent) and syncs `AppUserRole` with it.

### SQL — reset password

```sql
UPDATE AppUser SET PasswordHash = @PasswordHash, PasswordUpdatedTime = NULL WHERE pkid = @Pkid;
```
Returns `false` → 404 when no row was affected.

### SQL — duplicate `UserId` check

```sql
SELECT COUNT(1) FROM AppUser u
WHERE u.UserId = @UserId AND (@ExcludePkid IS NULL OR u.pkid <> @ExcludePkid)
```
`excludePkid` is kept on the signature for symmetry with `AppRole`, but the controller only
calls it on create.

### SQL — DELETE

```sql
SELECT u.UserId FROM AppUser u WHERE u.pkid = @Pkid;         -- 404 if null
DELETE FROM AppUserRole WHERE UserId = @UserId;
DELETE FROM AppUser WHERE pkid = @Pkid;
```
one transaction.

### Repository interface

```csharp
Task<IEnumerable<AppUser>> GetAllAsync(ct);
Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query, ct);
Task<AppUser?> GetByIdAsync(int pkid, ct);
Task<int> CreateAsync(AppUserRequest request, string passwordHash, ct);
Task<bool> UpdateAsync(AppUserRequest request, ct);
Task<bool> DeleteAsync(int pkid, ct);
Task<bool> ResetPasswordAsync(int pkid, string passwordHash, ct);
Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null, ct);
```

### Register in Program.cs

`IAppUserRepository → AppUserRepository`, `ISysConfigRepository → SysConfigRepository`.

### RowAudit

Not implemented — see *Gaps between the `/crud` skill and this codebase* in `docs/claude/adding-a-feature.md`.

---

## Frontend Notes

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `/app-users` | `AppUserList` | 使用者 AppUser |
| `/app-users/new` | `AppUserForm` | 新增使用者 |
| `/app-users/:id/edit` | `AppUserForm` | 編輯使用者 |
| `/app-users/:id` | `AppUserDetail` | 使用者明細 |

`/new` before `/:id`.

### Angular model — `core/models/app-user.model.ts`

The file already exists with `AppUserLookup`; extend it with `AppUser`, `AppUserRequest`,
`AppUserQuery` (dates as `string | null`, `yyyy-MM-dd`) and `EMPTY_APP_USER_QUERY`. No
`passwordHash` field anywhere. Add `AppRoleLookup` to `app-role.model.ts`.

### Service — `core/services/app-user.service.ts`

The standard six plus `resetPassword(pkid): Observable<AppUser>` →
`POST {base}/{pkid}/reset-password` with `null` body. `lookup.service.ts` gains
`getAppRoles()`.

### List component

Columns: 主代碼, 帳號, 姓名, 啟用 (`p-tag` 是／否), 角色數, 密碼更新時間
(`date:'yyyy-MM-dd HH:mm'` on `value + 'Z'` — Dapper hands `datetime` back as
`Kind = Unspecified`, and the spec template's UTC rule applies; `NULL` renders 「—」),
操作. Default sort `userId` ASC.

Drawer: 關鍵字 (帳號／姓名), 啟用 tri-state `p-select`, 角色 `p-select` from the role
lookup (`[showClear]`, `optionValue="roleId"`), 密碼更新時間 起／迄 `p-datepicker` pair.
Same `FilterForm` ↔ `AppUserQuery` conversion as the Course list (`toIsoDate` /
`parseIsoDate`), so the session-stored shape is the wire shape.

### Detail component

Fields as above, then a 角色 card of tags resolved through the lookup (raw `roleId` as the
fallback when the lookup misses), each linking to `/app-roles/{pkid}`. Toolbar carries
返回, **重設密碼** (confirm dialog → `resetPassword` → toast → reload) and 編輯.

### Form component

`userId` (disabled in edit, hint 帳號為角色關聯的鍵值，建立後不可變更), `userName`, `isActive`
`p-checkbox` seeded `true`, 角色 `p-multiselect`. In **add** mode a hint under the card
title says 密碼將設為系統預設密碼 so the operator is not left looking for a password field.
No password control in either mode, by contract. 409 → toast 帳號「x」已存在。

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.userId}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `app-user-list-filters` | `AppUserQuery` (wire shape) |
| `app-user-list-sort` | `{ sortField, sortOrder }` |
| `app-user-list-page` | `{ first, rows }` |

No incoming cross-entity query params yet. When `AppRoleDetail` grows a 查看使用者 button,
the contract is `/app-users?roleId={roleId}` (the natural key, since that is what the
junction stores) — accept it over the saved `roleId` filter, the way the Course list
handles `partnerPkid`.

### Date Handling / Sub-panels

Only the drawer's two date pickers; `core/utils/date.util.ts` does the conversion. No
sub-panels.

### Sidebar placement

**系統管理 Admin → 使用者 AppUser**, placed **first** in the group (before 角色 AppRole):
accounts are the thing an operator looks for most. `app.spec.ts` asserts the exact href
list and that the first nav link is `/app-roles`; both assertions move. The `''` and `**`
redirects stay on `/app-roles`.

Icon: `pi pi-users`.

---

## Tests

### Backend — `src/CMS.API.Tests/`

- `AppUserApiFactory.cs` — swaps `IAppUserRepository` for `FakeAppUserRepository` **and**
  `ISysConfigRepository` for `FakeSysConfigRepository` (default password `P@ssw0rd!`). Seeds
  three users out of `UserId` order: `miles@uuu.com.tw` (roles `Admin`, `User`), `helen`
  (inactive, role `User`, `PasswordUpdatedTime` 2026-03-01), `Jenny_Tsao` (no roles) — so
  ordering, the role filter, the tri-state, the date range and an empty role list are all
  observable. `helen` and `Jenny_Tsao` share the `UserName` `Helen` (one person, two
  accounts) so the no-duplicate-check-on-name rule is proven.
- `FakeAppUserRepository.cs` — in-memory; mirrors keyword LIKE on two columns, the
  `IsActive` / `RoleId` / date-range predicates, `UserId ASC` ordering, IDENTITY pkids,
  `RoleCount`, `OrdinalIgnoreCase` `UserIdExistsAsync`, delete-with-junction. Records the
  last `passwordHash` handed to `CreateAsync` / `ResetPasswordAsync` so the tests can
  assert the SHA-256 of the fake default, and never exposes it on a DTO.
- `AppUsersControllerTests.cs`:
  - `GetAll` ordered by `UserId`; `RoleCount` correct; **the JSON has no `passwordHash`
    property** (raw `JsonDocument`)
  - `Query`: keyword matches `UserName` not just `UserId`; `IsActive` false → helen only;
    `RoleId` `Admin` → miles only; date range around 2026-03-01; empty filter → all
  - `GetById` returns `RoleIds`; 404
  - `Create` → 201 + `Location`, server pkid, **fake received `Sha256Hex("P@ssw0rd!")`**,
    `PasswordUpdatedTime` null, roles synced, distinct-insensitive dedupe
  - `Create` duplicate `UserId` (different case) → 409 帳號重複
  - `Create` duplicate `UserName` → 201
  - `Create` blank `UserId` / `UserName` / overlong → 400
  - `Create` when the default password is missing → 500
  - `Update` changes `UserName`/`IsActive`/roles, **ignores a changed `UserId`**, does not
    touch the hash; `Pkid` 0 → 400 and repo untouched; unknown → 404
  - `Delete` → 204 then 404, junction rows gone; unknown → 404
  - `ResetPassword` → 200 with the row, fake received the default hash, `PasswordUpdatedTime`
    null; unknown → 404
- `LookupsControllerTests.cs` — `GET /api/lookups/app-roles` ordered by `RoleId`, `label`
  on the wire as `RoleName (RoleId)` via raw JSON.

### Frontend — Karma + Jasmine

- `app-user.service.spec.ts` — six standard methods plus `resetPassword` POSTs to
  `/app-users/{id}/reset-password`.
- `lookup.service.spec.ts` — `getAppRoles()` GETs `/lookups/app-roles`.
- `app-user-list.spec.ts` — loads on init; rows render 帳號／姓名／角色數 and the 啟用 tag;
  filters (keyword + tri-state + date) reach `query()` in wire shape and persist; restore;
  clear; sort/page persist; delete confirms then reloads; error → empty.
- `app-user-detail.spec.ts` — loads by route param; renders every field; null time → 「—」;
  role ids resolve to labels; empty-roles state; reset-password confirms then calls the
  service; not-found.
- `app-user-form.spec.ts` — add mode: title, `isActive` seeded true, `userId` editable,
  **no password control in the DOM**, required-field block, create trims and carries
  roles, 409 toast. Edit mode: title, loads, patches, `userId` disabled, PUT with pkid,
  cancel.
- `app.spec.ts` — 使用者 AppUser first in 系統管理 Admin, href list updated.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/AppUser.cs` | Create |
| `src/CMS.API/Models/AppUserRequest.cs` | Create |
| `src/CMS.API/Models/AppUserQuery.cs` | Create |
| `src/CMS.API/Models/AppConfig.cs` | Create |
| `src/CMS.API/Models/LookupItem.cs` | Modify — add `AppRoleLookup` |
| `src/CMS.API/Security/PasswordHasher.cs` | Create |
| `src/CMS.API/Repositories/IAppUserRepository.cs` | Create |
| `src/CMS.API/Repositories/AppUserRepository.cs` | Create |
| `src/CMS.API/Repositories/ISysConfigRepository.cs` | Create |
| `src/CMS.API/Repositories/SysConfigRepository.cs` | Create |
| `src/CMS.API/Repositories/ILookupRepository.cs` | Modify — add `GetAppRolesAsync` |
| `src/CMS.API/Repositories/LookupRepository.cs` | Modify — implement it |
| `src/CMS.API/Controllers/AppUsersController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — add `app-roles` |
| `src/CMS.API/Program.cs` | Modify — register both repositories |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/core/models/app-user.model.ts` | Modify — add entity/request/query |
| `src/CMS.NG/src/app/core/models/app-role.model.ts` | Modify — add `AppRoleLookup` |
| `src/CMS.NG/src/app/core/services/app-user.service.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.ts` | Modify — add `getAppRoles` |
| `src/CMS.NG/src/app/features/app-users/app-user-list/` | Create |
| `src/CMS.NG/src/app/features/app-users/app-user-detail/` | Create |
| `src/CMS.NG/src/app/features/app-users/app-user-form/` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four lazy routes |
| `src/CMS.NG/src/app/app.ts` | Modify — first entry of 系統管理 Admin |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/AppUserApiFactory.cs` | Create |
| `src/CMS.API.Tests/FakeAppUserRepository.cs` | Create |
| `src/CMS.API.Tests/FakeSysConfigRepository.cs` | Create |
| `src/CMS.API.Tests/AppUsersControllerTests.cs` | Create |
| `src/CMS.API.Tests/FakeLookupRepository.cs` | Modify — seed roles |
| `src/CMS.API.Tests/LookupApiFactory.cs` | Modify — seed roles |
| `src/CMS.API.Tests/LookupsControllerTests.cs` | Modify — `app-roles` tests |
| `src/CMS.NG/src/app/core/services/app-user.service.spec.ts` | Create |
| `src/CMS.NG/src/app/core/services/lookup.service.spec.ts` | Modify |
| `src/CMS.NG/src/app/features/app-users/**/*.spec.ts` | Create (three) |
| `src/CMS.NG/src/app/app.spec.ts` | Modify |
