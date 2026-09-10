# Build Spec for Authorization (Auth)

- database schema: `.\database\auth.sql` — `SysConfig` (read only; nothing new is stored)
- related spec: `spec/auth/Login.md` — the endpoint that issues the tokens this one validates

## Summary

The second non-CRUD feature. It adds no table, no endpoint and no page of its own: it turns the
tokens `POST /api/auth/login` already issued into an actual gate, on both sides.

| Item | Detail |
|------|--------|
| Backend | JWT bearer authentication; **every controller but `AuthController` requires a token** |
| No token / bad token | `401` + `WWW-Authenticate: Bearer`, empty body |
| Signing key | `SysConfig.appConfig.symmetricSecurityKey`, re-read **per request** |
| Frontend | Login page, HTTP interceptor, route guard, logout, role-gated sidebar |
| Token storage | **session storage**, key `cms-auth` — never local storage |
| Role gate | `[Authorize(Roles = "Admin")]` on `AppUsersController`, `AppRolesController`, `PublishStatusesController`, and on the `app-users` / `app-roles` **actions** of `LookupsController` |
| Missing role | `403`, session kept — the credential is fine, the permission is not |
| Tables written | **none** |

Roles are enforced **server-side**, not merely in the menu — see
"[The Admin gate is real — three layers, only one of which is the boundary](#the-admin-gate-is-real--three-layers-only-one-of-which-is-the-boundary)".
An earlier revision of this spec recorded the opposite as a deliberate scope choice; a `/cso`
security audit reversed it, and that section records the reasoning that was wrong.

### Verified against the live database and a real browser (2026-09-08)

> **Predates the role gate — not re-verified.** The table below was captured before
> `[Authorize(Roles)]` shipped, so its role rows no longer describe this API: `GET /api/app-roles`
> with a non-Admin token is now `403`, not `200`. The token rows (no token, garbage token,
> tampered signature) are unaffected. Re-running it needs the API plus `npm start` and a live sign-in.

| Check | Result |
|-------|--------|
| `GET /api/app-roles` with no token | `401` |
| `POST /api/Auth/login` (`admin@example.com`) | `200`, three-segment JWS, claim `role: "Admin"` |
| `GET /api/app-roles` with that token | `200`, 2 rows |
| `GET /api/courses` with `Bearer garbage` | `401` |
| Browser: `/courses` while signed out | redirected to `/login?returnUrl=%2Fcourses` |
| Browser: login, then the 1084-row course list | rendered — the interceptor's header reached the API |
| Browser: tampered signature, then navigate | session storage emptied, bounced to `/login` |
| Browser: `User`-only token | 系統管理 Admin group and its three links absent; other groups intact |

---

## Backend

### Authorization is applied to the surface, not to each class

`Program.cs`:

```csharp
app.MapControllers().RequireAuthorization();
```

One line covers every controller that exists and every controller anyone adds later. The
alternative — `[Authorize]` on each class — is a rule you have to remember, and the failure mode
of forgetting is a silently public endpoint. `AuthController` carries the single
`[AllowAnonymous]`, which endpoint metadata gives precedence over the blanket policy; it has to
be reachable without a token, because it is how a caller gets one.

`AddAuthorization()`'s `FallbackPolicy` was the other candidate. It reaches further than
"controllers" — it would also cover the `MapGet("/")` swagger redirect — so it says something
slightly different from what was asked for, and would need its own opt-out.

### The signing key is read per request, from the database

`JwtBearerOptions` is built once at startup, but the key lives in a `SysConfig` row that
`spec/auth/Login.md` deliberately re-reads on every token issue so rotation needs no redeploy.
Validation had to keep the same footing.

`Security/SysConfigSigningKeys.cs` implements `IConfigurationManager<OpenIdConnectConfiguration>`
and is handed to `options.ConfigurationManager`. That is the one hook `JwtBearerHandler`
**awaits** per request, so the repository call stays async. The obvious-looking alternative,
`TokenValidationParameters.IssuerSigningKeyResolver`, is synchronous and would mean blocking a
thread on Dapper.

Both halves now read the key through one shared `Security/JwtSigningKey.cs`, so the issuing key
and the validating key cannot drift and the 256-bit floor is enforced in one place. A test
rotates the fake `SysConfig` row mid-test and asserts the previously-working token is rejected on
the very next request.

The cost is one single-row `SysConfig` seek per authenticated request, bought for instant
rotation. Nothing is cached; if that ever matters, `SysConfigSigningKeys` is the only place to
change.

### Validation parameters, and why each is set that way

| Parameter | Value | Reason |
|-----------|-------|--------|
| `ValidateIssuerSigningKey` | `true` | The whole point. |
| `ValidateLifetime` | `true` | The 24 h `exp` has to mean something. |
| `ValidateIssuer` / `ValidateAudience` | **`false`** | The tokens carry no `iss`/`aud` — `spec/auth/Login.md` explains why. Leaving these on would reject every token the API itself issues. |
| `ClockSkew` | `TimeSpan.Zero` | The default is a 5-minute grace period. Issuer and validator are the same process on the same clock, so there is no skew to forgive, and a 24 h token does not need a 5 min cushion. |
| `MapInboundClaims` | `false` | Otherwise `JsonWebTokenHandler` rewrites the long claim forms into `ClaimTypes.*` URIs. The tokens use short names (`role`, `userId`, `userName`) precisely so a reader sees them verbatim. |
| `NameClaimType` / `RoleClaimType` | `userId` / `role` | So `User.Identity.Name` and `User.IsInRole()` mean what they look like, if a later change wants them. |

### A missing `appConfig` row is now a 500 on *every* endpoint

Previously only creating a user or resetting a password needed that row. Now authentication does
too, and `JwtSigningKey.ReadAsync` throws for a missing row, a blank key or one under 32 bytes.
The exception surfaces as a `500`, which is the honest answer and matches the rule
`spec/auth/Login.md` already set: a broken deployment is not a credential problem.

This bit two existing tests that simulated the fault by nulling the whole config. They now blank
`defaultPassword` and **keep** `symmetricSecurityKey` — same deployment fault, still a `500` from
the controller, but the request can still authenticate, so the 500 under test is the one the test
is named for rather than a side effect.

### Swagger

`AddSecurityDefinition` + `AddSecurityRequirement` give the Swagger UI its 授權 box, so the API
stays explorable by hand. The swagger JSON and UI are middleware, not endpoints, so
`RequireAuthorization()` does not cover them — they remain open, as before this change.

### Package versions

`Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.0 depends on the 8.x
`Microsoft.IdentityModel.*` stack, which `Microsoft.Data.SqlClient` 5.2.2 already pulls in
transitively. `System.IdentityModel.Tokens.Jwt` was pinned at 6.35.0 in both projects and had to
be raised to **8.0.1**, or restore fails with `NU1605` (package downgrade).

---

## Frontend

### The profile lives in session storage

`core/services/auth.service.ts`, key `cms-auth`, holding exactly the three properties
`LoginResponse` returns. Session storage, not local storage: it is scoped to the one tab and gone
when that tab closes, so a shared machine does not leave the next person signed in. A test
asserts `localStorage` stays empty.

`token()` re-reads storage on every call rather than returning a memoised value, because the
interceptor must see a token a just-completed login wrote.

### Logging out clears *all* of session storage

Not just `cms-auth`. The list pages park `{entity}-list-filters`, `-sort` and `-page` there;
those are the previous user's view of the data, and leaving them for whoever signs in next is
both confusing and a small leak. Same on a 401. Two tests seed an unrelated
`course-list-filters` key and assert `sessionStorage.length === 0` afterwards.

### One interceptor, two halves of one rule

`core/interceptors/auth.interceptor.ts` attaches the header **and** handles the 401, because they
are the same rule: the token it attaches is the one the API rejects, and a rejection means the
session is over.

- The header goes only on requests to `environment.apiBaseUrl`. The token is a credential for
  this API; the QR-code links to `publicSiteBaseUrl` must not carry it.
- **The login endpoint's own 401 is exempt.** There it means 帳號或密碼錯誤, belongs to the login
  form, and treating it as a dead session would clear storage and re-navigate to the page the
  user is already on — swallowing the message. A test covers exactly this.
- The error is re-thrown after the redirect, so callers still see it.

### One guard, on a pathless parent

`app.routes.ts` gained a `{ path: '', canActivateChild: [authGuard], children: [...] }` wrapper
around every existing route, rather than `canActivate` repeated on twenty-five of them. A route
added tomorrow is covered by construction. `/login` sits outside the wrapper.

The guard returns a `UrlTree` to `/login` carrying `returnUrl`, so signing in lands the user where
they were headed. `Login` only honours a `returnUrl` that starts with a single `/` — a value from
the query string that pointed off-site would make the login page an open redirect.

### The shell appears with the session, the outlet never moves

`app.html` keeps **one** `<router-outlet>` at all times; the sidebar and the header are what
`@if (signedIn())` adds and removes. Wrapping the outlet itself in the `@if`/`@else` was the first
shape and it works, but it destroys and re-creates the outlet on every sign-in and sign-out for no
gain. `.cms-shell--bare` drops the page padding so the login page can own the full height.

### The Admin gate is real — three layers, only one of which is the boundary

> **Superseded.** This section previously read "The Admin gate is cosmetic, on purpose" and
> recorded a deliberate scope choice: the menu hidden, the routes unguarded, the controllers
> ungated. The 2026-09-10 `/cso` security audit reversed it (Finding 1, CRITICAL; the report
> lives under `.gstack/`, which is gitignored, so it is not in this repo).
> The reasoning that was wrong is worth keeping, because it is easy to
> repeat: the old text argued the hidden menu was harmless because "nothing behind it would
> answer — every request still carries a token the API validates." Validating a signature is
> **authentication**; it says the token is ours and unexpired. It says nothing about what the
> holder may do. Those endpoints answered fine, and `POST /api/app-users/{id}/reset-password`
> answered for any signed-in caller.

`navGroups` is a `computed` that filters out any group with a `requiresRole` the stored token
does not carry. Roles come from decoding the JWT payload in the browser — **no extra API call**.
That decode does not verify the signature, so a user who edits their own session storage can
unhide the menu. Two things now stand behind it:

| Layer | Where | What it is |
|-------|-------|-----------|
| Hidden menu | `app.ts` `navGroups` | Tidiness. Bypassable by editing session storage. |
| `adminGuard` | `app.routes.ts`, on the 系統管理 pathless parent | Navigation ergonomics. Same client-side roles, same bypass. Stops the honest case loading a page that would only 403. |
| `[Authorize(Roles = "Admin")]` | `AppUsersController`, `AppRolesController`, `PublishStatusesController`, and the `app-users` / `app-roles` **actions** on `LookupsController` | **The boundary.** Server-side, on the validated token's `role` claims. |

Only the third row is security. The first two exist so a non-Admin never meets a broken page.

**The two lookup actions are gated individually, not the whole controller.** `/api/lookups/app-users`
and `/api/lookups/app-roles` feed the Admin-only forms; the rest (發布狀態, 課程群組, 合作廠商,
認證, 職務類別, 據點) feed the course maintenance forms that every signed-in user needs. Gating the
class would break ordinary work; leaving those two open would leave account enumeration wide open
after gating `AppUsersController` — the role check would be half-done.

**`AppRolesController` and `AppUsersController` must be gated together, in one change.** This is
the trap this feature was one commit away from: `AppRoleRequest.UserIds` flows into
`AppRoleRepository.SyncUserRolesAsync`, which deletes and rewrites the whole `AppUserRole`
membership for a role. An ungated `PUT /api/app-roles` is therefore a one-request self-grant of
`Admin` that walks straight through a gate applied anywhere else. Any future role check has the
same obligation: gate the thing that *assigns* the role at the same moment as the thing the role
protects.

Case sensitivity differs by layer, on purpose:

- **Angular** (`auth.service.hasRole`) compares case-insensitively, matching the
  `Chinese_Taiwan_Stroke_CI_AS` collation on `AppRole.RoleId`. It only decides menu and routing.
- **`[Authorize(Roles = ...)]`** goes through `ClaimsPrincipal.IsInRole`, an **ordinal,
  case-sensitive** match against the `role` claim — which `JwtTokenService` writes verbatim from
  the database. `AppRoles.Admin` must therefore match the stored casing exactly. `RoleIdExistsAsync`
  keeps a second differently-cased row from being created, so the two cannot drift in practice.

### The account is re-checked on every request

`ActiveAccountEvents` (a `JwtBearerEvents` on the bearer scheme) reads `AppUser` after the
signature validates and fails the request when the row is missing or `IsActive = 0`.

Before it, `IsActive` was read in exactly one place in the API — the login action — so a token
outlived deactivation and deletion by up to its full 24-hour lifetime. Unticking 啟用 looked like
an access control and was not one; an offboarded user kept full write access until the next day.
This was Finding 3 (HIGH) of the same 2026-09-10 `/cso` audit.

Both cases fail with **401**, not 403: the credential itself is finished, and 401 is what makes the
Angular interceptor clear the session and return to the login page. 403 is reserved for "we know
who you are, you may not do this" — the role gate above.

The cost is one clustered-PK seek on `AppUser` per authenticated request (`PK_AppUser` is on
`UserId`). The middleware already reads `SysConfig` per request for the signing key, so this is the
same order of cost, and the two are the pair to cache together if it ever matters. Do not answer a
profiler by deleting the check — that returns 啟用 to being decorative.

`AuthController.UpdateProfile` keeps its `404 查無使用者` branch even though the middleware now
catches a deleted account first. It is reachable only if the row disappears between that check and
the UPDATE, and answering that race honestly beats reporting a success that wrote nothing.

---

## Files

| Layer | File |
|-------|------|
| Signing key (shared) | `src/CMS.API/Security/JwtSigningKey.cs` — **new**, also used by `JwtTokenService` |
| Key for validation | `src/CMS.API/Security/SysConfigSigningKeys.cs` — **new** |
| Middleware + policy | `src/CMS.API/Program.cs` |
| Anonymous opt-out | `src/CMS.API/Controllers/AuthController.cs` — `[AllowAnonymous]` |
| Account re-check | `src/CMS.API/Security/ActiveAccountEvents.cs` — **new**; `IAuthRepository.IsActiveAccountAsync` |
| Role constant | `src/CMS.API/Security/AppRoles.cs` — **new** |
| Role gate | `AppUsersController`, `AppRolesController`, `PublishStatusesController` (class-level), `LookupsController` (two actions) |
| Package | `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.0; `System.IdentityModel.Tokens.Jwt` 6.35.0 → 8.0.1 |
| Backend tests | `src/CMS.API.Tests/AuthorizationTests.cs`, `ApiFactory.cs`, `TestTokens.cs` (all new) |
| NG model | `src/CMS.NG/src/app/core/models/auth.model.ts` |
| NG service | `src/CMS.NG/src/app/core/services/auth.service.ts` |
| NG interceptor | `src/CMS.NG/src/app/core/interceptors/auth.interceptor.ts` |
| NG guard | `src/CMS.NG/src/app/core/guards/auth.guard.ts`, `admin.guard.ts` — **admin.guard new** |
| NG page | `src/CMS.NG/src/app/features/auth/login/` |
| NG shell | `src/CMS.NG/src/app/app.ts`, `app.html`, `app.scss`, `app.routes.ts`, `app.config.ts` |

---

## Tests

### Backend — `AuthorizationTests`, 30 cases

- a protected endpoint returns `401` without a token and `200` with one
- the `401` carries a `WWW-Authenticate: Bearer` challenge
- seven feature endpoints — including `/api/lookups/app-roles` — each `401` anonymously
- an anonymous `POST` is rejected **and writes nothing**
- garbage, wrongly-signed and expired tokens each `401`
- rotating the `SysConfig` key rejects the previously-working token on the next request
- `AuthController` stays anonymous: login succeeds without a token; bad credentials return the
  controller's `登入失敗` `ProblemDetails` **with no bearer challenge** (proof the request reached
  the controller rather than being turned away by the middleware); a blank `userId` still `400`s

The Admin gate:

- a role-less token and a `User`-only token each get `403` on 系統管理 — not `401`; the caller is
  known, they simply may not do this. (The first of these asserted `200` until the audit.)
- all five Admin-only URLs `403` without the role, including the two gated lookup actions
- the three content lookups still `200` for a `User` token — over-gating is as wrong as under-gating
- `PUT /api/app-roles` `403`s **and leaves the role's membership untouched**, proving
  `SyncUserRolesAsync` never ran — the self-grant path is closed
- an Admin token gets `200` on the same endpoint

The per-request account check:

- deactivating an account `401`s its **next** request; deleting it does the same
- three requests produce three account lookups — the check is per request, not per token
- a token naming an account that never existed `401`s, so a token minted from a leaked signing
  key still buys nothing
- an inactive account cannot ride in on a token, matching the login path's answer

### Every other backend suite now authenticates

`RequireAuthorization()` would have `401`d all 276 existing tests. Rather than exempting the test
host, they now go through the real middleware: `ApiFactory` is a new base for all eight feature
factories that fakes `ISysConfigRepository` (so no test reaches SQL Server for the signing key)
and gives `CreateClient()` a token issued by the **production** `JwtTokenService`.
`CreateAnonymousClient()` is how a test asks for the unauthenticated behaviour. See
`docs/claude/testing.md`.

### Frontend — 49 Karma cases

- **interceptor**: attaches `Bearer` on reads and writes; omits it with no token; re-reads storage
  per request; leaves non-API hosts alone; a 401 empties session storage and navigates to
  `/login`; the error still reaches the caller; 409 leaves the session alone; the login
  endpoint's 401 does not redirect
- **guard**: passes a signed-in user; returns a `UrlTree` to `/login` with no token; carries
  `returnUrl`; blocks again after the session is cleared; blocks a stored profile with no token
- **service**: posts `{ userId, password }`; stores in session storage and **not** local storage;
  a failed login stores nothing; roles read from the token as array, bare string, and absent;
  case-insensitive `Admin` match; malformed token and unparsable JSON degrade quietly; logout
  clears the whole of session storage
- **login page**: no API call on an empty form; trims the account; lands on `returnUrl`; ignores
  an off-site `returnUrl`; shows 帳號或密碼錯誤 on 401 and a different message on 500
- **app shell**: 系統管理 Admin shows for `Admin`, hides for `User` and for no roles (with the
  exact remaining href list asserted); the signed-in `UserName` renders; logout clears storage,
  navigates to `/login` and drops the sidebar; nothing renders the shell when signed out

`app.spec.ts`'s existing nav assertions now seed an Admin session first — the shell does not
exist without one.
