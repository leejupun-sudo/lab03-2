# TODOS

Deferred findings from the `/ship` pre-landing review of the JWT authorization feature
(branch `lab07-1`, v0.1.0.0, 2026-09-10). Nine reviewers: the ship checklist pass plus
testing, maintainability, security, performance, api-contract, design, simplification and
red-team specialists.

Every item below was **found and deliberately deferred**, not missed. Each cites `file:line`.

---

## Security

### Deleting the Admin role permanently locks 系統管理 out

**What:** Add a last-admin guard so no write can leave zero active users holding
`AppRoles.Admin`.

**Why:** `AppRolesController.Delete` (`src/CMS.API/Controllers/AppRolesController.cs:122`) has no
guard at all, and `AppRoleRepository.DeleteAsync:142-143` hard-deletes every `AppUserRole` row for
the role and then the `AppRole` row. Once the `Admin` row is gone no token can ever carry
`role: Admin` again — and `POST /api/app-roles`, the only endpoint that could re-create it, is
itself gated on that role. 使用者 / 角色 / 發布狀態 maintenance becomes unreachable from inside the
app, permanently. The same end state is reachable via `PUT /api/app-users` with the last admin's
`RoleIds` emptied or `IsActive = false`, and via `DELETE /api/app-users/{id}`. The list page's
confirmation is the generic house string (`app-role-list.ts:133`) and gives no warning that the row
is load-bearing.

**Context:** Before this release the role was cosmetic, so deleting it cost nothing — this release
is what makes it catastrophic. Right now the stale 24-hour role claim is the *only* recovery path:
an already-signed-in admin has until their token expires to re-create the role. **That means this
item and "Revoking a role takes up to 24 hours" interact: fixing the revocation lag without landing
this guard first turns the lockout instantaneous and SQL-only to recover.** Land this one first, or
land both together. Follow the existing `IsInUseAsync` guard pattern (`docs/claude/schema-traps.md`)
and answer `409` + `ProblemDetails`, matching the house rule for foreseeable conflicts.

**Effort:** M
**Priority:** P0
**Depends on:** None — and this one blocks the revocation-lag item below.

### Revoking a role takes up to 24 hours to bite

**What:** Make `[Authorize(Roles)]` evaluate live `AppUserRole` membership instead of the roles
baked into the token at login.

**Why:** `ActiveAccountEvents.cs:50` re-reads the account per request but asks only
`WHERE UserId = @UserId AND IsActive = 1` (`AuthRepository.cs:57-60`) — it never looks at roles,
while `JwtTokenService.cs:66` writes one `role` claim per `AppUserRole` row at login. Removing
someone's Admin role therefore leaves them Admin until their token expires. Deactivating an account
takes effect on the next request; demoting one does not. That inconsistency is the surprising part.

**Context:** `IsActiveAccountAsync` already opens a connection and seeks the clustered PK; joining
`AppUserRole` is one more seek on the same connection. Either compare the returned `RoleIds`
against the token's `role` claims and `context.Fail(...)` on divergence, or rebuild the principal's
role claims from the database so the attribute always sees live membership. Add xUnit coverage for
both directions (granted mid-session, revoked mid-session).

**Effort:** M
**Priority:** P0
**Depends on:** The last-admin guard above — see the interaction noted there.

### Login has no rate limiting or account lockout

**What:** Add ASP.NET Core rate limiting to `POST /api/auth/login`, partitioned on remote IP and on
the submitted `UserId`, plus a per-account failure counter.

**Why:** `grep -rn "RateLimiter" src/CMS.API/` matches nothing but build artefacts. Verification is
a single SHA-256, so an unauthenticated caller can test credentials as fast as SQL Server answers.
This is concretely exploitable here rather than theoretical: `AppUsersController`'s own remarks
record that every account is created with the shared `appConfig.defaultPassword`, so spraying one
known password across account names is a realistic path to an Admin session.

**Context:** Needs a product decision on the policy (window size, threshold, whether a failed
streak locks the account or just throttles the caller) — that is why it was deferred rather than
guessed at. Pair it with the user-enumeration item below; rate limiting narrows the timing-oracle
sampling window but does not remove the signal.

**Effort:** M
**Priority:** P0
**Depends on:** None

### Authentication failures return an unhandled 500 and leak stack traces in Development

**What:** Add `app.UseExceptionHandler()` (or `AddProblemDetails` plus a ProblemDetails handler) as
the first middleware, and override `ActiveAccountEvents.AuthenticationFailed` to separate an
infrastructure fault (503) from a bad credential (401).

**Why:** `Program.cs` goes `UseSwagger → UseCors → UseAuthentication → UseAuthorization →
MapControllers` with no exception middleware anywhere. Two database reads now sit *inside* the
authentication middleware, and both throw: `JwtSigningKey.ReadAsync:43-53` on a missing `appConfig`
row, a blank key or one under 32 bytes, and `AuthRepository.IsActiveAccountAsync` on any
`SqlException`. `JwtBearerHandler` logs and rethrows, so a SQL Server restart, an edited `SysConfig`
row or pool exhaustion turns **every** authenticated request into an unhandled 500. In Development,
`WebApplication` installs the developer exception page, so that 500 hands the exception message and
full stack trace to any anonymous caller who sends a token-shaped `Authorization` header.

**Context:** Client-side the failure is invisible: `auth.interceptor.ts` branches on 401 and 403
only, so a 500 falls through to each feature's own handler and every page shows its own 載入失敗
toast while the real cause is that authentication is down. Give the interceptor a `>= 500` branch
with an honest 系統暫時無法使用 message at the same time.

**Effort:** S
**Priority:** P0
**Depends on:** None

### Swagger serves the full API map to anonymous callers

**What:** Decide explicitly whether `/swagger` should be reachable without a token; if not, wrap it
in `app.Environment.IsDevelopment()` or move it below `UseAuthentication` with
`RequireAuthorization`.

**Why:** `UseSwagger`/`UseSwaggerUI` sit at `Program.cs:123-128`, before `UseAuthentication` at
`:132`, so `GET /swagger/v1/swagger.json` is answered and short-circuited before any token is
looked for. It enumerates every route this release was meant to close, including the Admin-only
系統管理 endpoints and `POST /api/app-users/{id}/reset-password`, plus the security scheme
explaining how to authenticate. Combined with the missing rate limiting and the shared default
password, that is a self-service attack map.

**Context:** This is **documented, not accidental**: `spec/auth/Authorization.md` (the Swagger
section) states that the swagger JSON and UI are middleware, not endpoints, so
`RequireAuthorization()` does not cover them and they remain open as before. What was never
weighed is what the document now exposes after the Admin gate landed. Also decide whether
`app.MapGet("/")` (the swagger redirect at `Program.cs:143`) should carry authorization —
`MapControllers().RequireAuthorization()` does not reach it.

**Effort:** S
**Priority:** P1
**Depends on:** None

### Resetting a password does not invalidate that user's live tokens

**What:** Give the token a watermark derived from the stored credential and compare it in
`TokenValidated`.

**Why:** The per-request check reads only `IsActive`, and the JWT carries nothing password-related,
so a stolen token keeps working for the rest of its 24 hours after an admin resets the password.
The class comment on `ActiveAccountEvents` has been corrected to say so, and to record that the
operational answer to a leaked token today is to untick 啟用 rather than to reset the password.

**Context:** `AppUserRepository.ResetPasswordAsync` sets `PasswordUpdatedTime = NULL`
(`AppUserRepository.cs:188-191`), so that column cannot serve as the watermark as-is — this needs a
monotonic `PasswordVersion` column or a short hash of `PasswordHash`.

**Effort:** M
**Priority:** P1
**Depends on:** None

### Login leaks which accounts exist through response timing

**What:** Equalise the work done on every login failure path.

**Why:** `AuthController.cs:67-71` returns for an unknown `UserId` after one round trip, never
hashing; an existing account costs a second round trip (the `AppUserRole` query at
`AuthRepository.cs:43-44`) and, if active, a SHA-256. So {unknown} / {inactive} / {active, wrong
password} are distinguishable by response time even though all three return the identical `401`
body — which is exactly the enumeration that identical body exists to prevent.

**Context:** Always compute the hash and always run `FixedTimeEquals` against a fixed dummy hash
when the credential is null or inactive, and defer the `AppUserRole` lookup until after the
password check succeeds (roles are only needed to mint the token).

**Effort:** S
**Priority:** P2
**Depends on:** None

### Key rotation is a fleet-wide forced logout, not a graceful roll

**What:** Either support an overlap key (`previousSymmetricSecurityKey`) or stop describing runtime
rotation as safe.

**Why:** `SysConfigSigningKeys.cs:43-44` populates exactly one `SigningKey`, so the moment the
`SysConfig` row is edited every outstanding token fails signature validation. There is no overlap
window. Every signed-in user is cleared and bounced to `/login` mid-work, with unsaved form data
lost. The code comments actively encourage rotation as a restart-free operation.

**Context:** Reading `symmetricSecurityKey` plus an optional `previousSymmetricSecurityKey` into
`configuration.SigningKeys`, and signing only with the current one, gives a real grace period. Add
an xUnit case proving a token signed with the previous key still validates during the overlap.

**Effort:** M
**Priority:** P2
**Depends on:** None

---

## Frontend

### Session expiry mid-edit silently destroys unsaved work

**What:** Preserve the user's location and tell them what happened when a 401 ends the session, and
guard dirty forms against the resulting navigation.

**Why:** `auth.interceptor.ts:45` navigates to `/login` immediately. There is no `CanDeactivate`
guard anywhere (`grep -rn 'canDeactivate|CanDeactivate|beforeunload' src/CMS.NG/src` → no matches),
the interceptor passes no `returnUrl` even though `authGuard:26` is careful to build one, and the
login page renders with no message. A user 20 minutes into a course form clicks 儲存, gets a 401,
and lands on an empty login page with no explanation, no draft, and no way back to the record.

**Context:** Minimum: `router.navigate(['/login'], { queryParams: { returnUrl: router.url, reason:
'expired' } })` plus a 登入狀態已逾時 banner in `login.ts`. Better: stash the dirty form value under
a session-storage key that `clearSession()` deliberately preserves, and re-hydrate after re-login.

**Effort:** M
**Priority:** P1
**Depends on:** None

### Every feature reports a 401 as a data error

**What:** Either have the interceptor swallow 401 (return `EMPTY`) so component handlers never see
it, or add the 401 branch that `my-profile.ts:92-95` already has to every handler.

**Why:** `my-profile.ts` is the single place that special-cases 401. Everywhere else lumps it into a
generic message — `course-list.ts:477-484` tells the user their inline edit was rejected and
reverted for a data reason when in fact the session ended. Because `<p-toast>` sits at
`app.html:87`, outside the `@if (signedIn())` block, that misleading toast survives the navigation
and renders on top of the login card: two contradictory stories, neither saying 你的登入已逾時.

**Context:** Swallowing 401 in the interceptor is the smaller and less forgettable change. Whichever
is chosen, record it once in CLAUDE.md's frontend conventions so the next feature inherits it.

**Effort:** S
**Priority:** P1
**Depends on:** None

### A stale token is attached to the login request

**What:** Reuse the existing `isLoginRequest()` helper on the outgoing branch, not just the response
branch.

**Why:** `auth.interceptor.ts:36-39` attaches the header whenever `isApiRequest && token`, while
`:43` excludes login only on the way back. A user whose token is expired-but-present (authGuard only
redirects when `token()` is falsy) therefore sends `Authorization: Bearer <expired>` to
`POST /api/auth/login`. Server-side that runs the full bearer pipeline — signing-key read, signature
check, account seek — and logs an ERROR-level validation failure for a completely normal re-login,
which is exactly the noise that hides a real attack in the log.

**Context:** One extra term: `const attach = isApiRequest && token && !isLoginRequest(req.url);`

**Effort:** S
**Priority:** P2
**Depends on:** None

### Login form errors are invisible to screen readers

**What:** Associate the field-level errors with their inputs and manage focus.

**Why:** `login.html:21-23` renders `<small class="cms-field__error">` with no `aria-invalid`, no
`aria-describedby` and no live region; a repo-wide grep finds ARIA on exactly one element
(`login.html:46`). `login.ts:48-51` marks controls touched and returns without moving focus, so for
a keyboard or screen-reader user the 登入 button appears to do nothing. Nothing focuses 帳號 on
load, on the one page whose entire purpose is typing two fields. On a 401, `login.ts:69` clears the
password but leaves focus where it was.

**Context:** `aria-invalid` + `aria-describedby` on both inputs, `id` on both `<small>`s, focus the
first invalid control after `markAllAsTouched()`, focus 密碼 after a 401, and `autofocus` on 帳號.

**Effort:** S
**Priority:** P1
**Depends on:** None

### The header user chip hides the user's name from screen readers

**What:** Drop the `aria-label` (and the redundant `title`) on the chip, or extend it to include the
name.

**Why:** `app.html:64` sets `aria-label="我的帳號 My Profile"`, which overrides the element's own
content — so a screen reader announces the link as "我的帳號 My Profile" and never reads
`{{ userName() }}`, the one piece of information the chip exists to show. The visible text already
says those same words, so the label only removes information.

**Effort:** S
**Priority:** P2
**Depends on:** None

### No focus-visible styles anywhere in the app

**What:** Add a shared `:focus-visible` rule for the hand-rolled shell controls.

**Why:** `app.scss:163-167` and `:192-195` define `:hover` only, and a repo-wide grep for
`:focus`/`:focus-visible` across all SCSS returns nothing. Native elements fall back to the UA
outline, which is undefined against the app's emerald/slate palette. The sidebar toggle and nav
headers have the same gap.

**Effort:** S
**Priority:** P2
**Depends on:** None

### Login page introduces a third red and undersized error text

**What:** Promote a danger token trio into `styles.scss` and use it.

**Why:** `login.scss:46-54` hardcodes `#fecaca` / `#fef2f2` / `#b91c1c`, none of which exist
elsewhere — `styles.scss:110` uses `#dc2626` for `.cms-field__required` and `.cms-field__error`. It
is also set at `0.85rem`, about 11.9px at the 14px root, making the page's single most important
message its smallest text.

**Effort:** S
**Priority:** P3
**Depends on:** None

### Shared SCSS primitives are copy-pasted across eight components

**What:** Hoist `.cms-field__hint`, `.cms-field__readonly` and `.cms-tags` into `src/styles.scss`
and delete the per-component copies.

**Why:** `my-profile.scss:1-14` is the eighth divergent copy (partner-form, course-form,
course-group-form, app-user-form, app-role-form, app-user-detail, course-list already carry it), and
they have already drifted — this copy has `margin: 0` on `.cms-field__readonly`, partner-form's does
not. CLAUDE.md puts shared primitives in `styles.scss`.

**Context:** Deliberately out of scope for the auth release: the fix touches eight unrelated
existing pages.

**Effort:** M
**Priority:** P3
**Depends on:** None

### Small touch targets and 100vh on mobile

**What:** Give `.cms-topbar__user` / `.cms-topbar__logout` a 44px minimum, raise
`.cms-topbar__user-action` off `0.8rem` (11.2px at the 14px root), and add `min-height: 100svh`
beside `100vh` on `.cms-login`.

**Why:** `app.scss:155-159` and `:179-190` compute to roughly 24-26px tall in the top-right corner,
the hardest area to hit on a phone. `login.scss:5`'s `100vh` is the tallest-chrome height on iOS
Safari and Android Chrome, so the centred card sits low and the page gains a scrollbar. Login is the
page most likely to be opened on a phone.

**Effort:** S
**Priority:** P3
**Depends on:** Verification on a real device

---

## Testing

### Nothing asserts the guards and the interceptor are actually wired up

**What:** Add `app.routes.spec.ts` and `app.config.spec.ts`.

**Why:** This is the auth feature's single largest blind spot. `auth.guard.spec.ts` and
`admin.guard.spec.ts` invoke the guard functions directly; `app.spec.ts:43` configures
`provideRouter([])`; no spec imports `routes` from `app.routes.ts`, and no spec imports `appConfig`.
Deleting `canActivateChild: [adminGuard]` (`app.routes.ts:48`) leaves all 468 specs green. Deleting
`withInterceptors([authInterceptor])` (`app.config.ts:45`) — which strips the Bearer header from
every request and disables 401 logout — also leaves all 468 green. `app.routes.ts:42`'s own comment
claims routes are "guarded by construction rather than by remembering"; nothing pins that property.

**Context:** Assert that the pathless shell parent carries `authGuard`, that every
`app-users`/`app-roles`/`publish-statuses` path is a descendant of the `adminGuard` branch, that no
such path exists outside it, and that an `HttpClient` built from `appConfig.providers` attaches the
header.

**Effort:** S
**Priority:** P1
**Depends on:** None

### The nbf test does not pin ClockSkew

**What:** Change the offset in
`AuthorizationTests.ProtectedEndpoint_WithANotYetValidToken_Returns401` from 10 minutes to 1.

**Why:** `AuthorizationTests.cs:522` issues a token 10 minutes ahead, but
`TokenValidationParameters.DefaultClockSkew` is 5 minutes — so the test passes unchanged if
`ClockSkew = TimeSpan.Zero` is deleted from `Program.cs:78`. Its own docstring
(`AuthorizationTests.cs:511-513`) claims it proves the not-yet-valid direction. The expiry-direction
test at `:138` does pin the skew; this one does not.

**Effort:** S
**Priority:** P2
**Depends on:** None

### The API side never tests role-claim case sensitivity

**What:** Add one xUnit case asserting `factory.TokenRoles = ["admin"]` gets a 403 on an Admin-only
endpoint.

**Why:** `AppRoles.cs:11-14` documents at length that `[Authorize(Roles)]` compares ordinally while
the SQL collation and the Angular sidebar are both case-insensitive. `auth.service.spec.ts:153` and
`admin.guard.spec.ts:69` assert the browser *accepts* a differently-cased claim; nothing asserts the
API *rejects* it. If a role row is ever stored as `ADMIN`, the sidebar and guard open 系統管理 and
every page behind it 403s, and no test on either side flags the mismatch.

**Effort:** S
**Priority:** P2
**Depends on:** None

### Four specs contain zero Jasmine expectations

**What:** Add a real assertion alongside the `httpMock.expectNone(...)` in
`my-profile.spec.ts:195`, `:204`, `:296` and `login.spec.ts:70`.

**Why:** Karma warns `has no expectations` for each on every run. `expectNone()` does throw on a
stray request, so behaviour is checked — but with `failSpecWithNoExpectations` off these specs would
also pass if their body were gutted. The pattern predates this release (`lookup.service.spec.ts` has
one too), so it is a convention worth correcting rather than a new mistake.

**Effort:** S
**Priority:** P3
**Depends on:** None

### `UpdateProfile`'s 404 branch cannot be tested through the current fake

**What:** Give `FakeAuthRepository` a switch that makes `UpdateUserNameAsync` return false for an
account that still passes `IsActiveAccountAsync`.

**Why:** `FakeAuthRepository` backs both calls with one dictionary (`:111`, `:128`), so the
delete-during-request race the `404` branch exists for (`AuthController.cs:133-143`) is
inexpressible. `spec/auth/MyProfile.md` now records the branch as deliberately untested; a small
fake change would make it testable instead.

**Effort:** S
**Priority:** P3
**Depends on:** None

### A UTF-8 decode test asserts nothing about UTF-8

**What:** Rewrite `auth.service.spec.ts:159` to assert through a non-ASCII **role** claim.

**Why:** The spec is named 'decodes a UTF-8 userName out of the token payload' but checks only that
`token()` is truthy and `roles()` is empty — both true for any token string, neither touching the
`TextDecoder` path at `auth.service.ts:172`. `AuthService` never surfaces the token's `userName`
claim (that comes from the stored profile), so the spec cannot fail for the reason it names.

**Effort:** S
**Priority:** P3
**Depends on:** None

---

## API contract

### The OpenAPI document omits 401 and 403 everywhere

**What:** Add an `IOperationFilter` that appends 401 to every operation without `[AllowAnonymous]`
and 403 to every operation whose metadata carries an `AuthorizeAttribute` with roles.

**Why:** All 119 `[ProducesResponseType]` attributes outside `AuthController` cover only
200/201/204/400/404/409, and `AddSwaggerGen` registers no operation filter — so the generated
document tells a client that `GET /api/app-roles` has exactly one possible outcome. A generated
client has no branch for the two status codes this entire release is about.

**Context:** One filter beats 119 hand-written attributes and cannot drift when a controller is
added. The same filter should exempt `[AllowAnonymous]` operations from the global security
requirement — right now `Program.cs:47-57` declares that `POST /api/auth/login` also requires a
bearer token, describing an API that cannot be bootstrapped.

**Effort:** S
**Priority:** P1
**Depends on:** None

### The Angular client calls `/api/Auth` with a capital A

**What:** Change `auth.service.ts:33` to `${environment.apiBaseUrl}/auth` and update the doc
comments in `core/models/auth.model.ts` and the affected spec constants.

**Why:** It is the only service base URL in the app that is not kebab-lowercase — nine siblings use
`/app-roles`, `/publish-statuses`, and so on. It works only because ASP.NET routing is
case-insensitive; any case-sensitive hop in front of the API (a reverse-proxy location block, a
gateway rule, a WAF path allowlist) would see a path the OpenAPI document does not contain. The
mismatch has already leaked: `auth.interceptor.ts:66` has to lowercase the URL to recognise its own
login request.

**Effort:** S
**Priority:** P2
**Depends on:** None

### `UpdateProfile` declares response shapes it never returns

**What:** Correct the `[ProducesResponseType]` set on `AuthController.UpdateProfile`.

**Why:** `:109-110` declare a bare 401 and a 404 and the action builds `ProblemDetails` bodies for
both, but neither branch is reachable in normal operation — `ActiveAccountEvents` rejects both
conditions first, and its rejection is the JwtBearer challenge: empty body plus
`WWW-Authenticate`, not `ProblemDetails`. A client generated from the document will parse a
title/detail a real 401 never carries.

**Effort:** S
**Priority:** P3
**Depends on:** None

### The breaking change is invisible in the served document

**What:** Extend the `OpenApiInfo` description with the date and the new rule.

**Why:** Public-to-authenticated across the whole surface plus Admin-only on five endpoints ships
under an unchanged `Version = "v1"` with a description that says nothing about it
(`Program.cs:23-28`). The break is contained in practice — the sole consumer is the Angular SPA in
this repo, updated in the same change — but nothing tells an out-of-repo caller (a script, a Swagger
UI bookmark) why its previously working calls now 401.

**Effort:** S
**Priority:** P3
**Depends on:** None

---

## Performance

### Cache the signing key; make `RequestRefresh()` do its job

**What:** Cache the `OpenIdConnectConfiguration` behind a short TTL plus a `SemaphoreSlim`, and make
`RequestRefresh()` invalidate it.

**Why:** `SysConfigSigningKeys.cs:49-52` is a deliberate no-op, so every token-bearing request pays
a connection open, a single-row seek, a JSON deserialize and a new key allocation. `JwtBearerHandler`
fetches the configuration *before* checking the signature, so `Authorization: Bearer garbage` forces
a database read per request — a pre-auth amplification vector while no rate limiting exists.
Opening the Course form fires six parallel requests (`course-form.ts:163` `forkJoin`), so that one
page costs six identical reads of the same row within milliseconds, uncoalesced.

**Context:** Zero caching is not actually required for instant rotation:
`JwtBearerOptions.RefreshOnIssuerKeyNotFound` (default true) already calls `RequestRefresh()` and
retries when a token fails to match a key — the hook this class leaves empty. Verify which exception
a rotated symmetric key produces (`SecurityTokenSignatureKeyNotFoundException` triggers the retry,
`SecurityTokenInvalidSignatureException` does not) before relying on the hook alone; the TTL is the
safety net either way. Do **not** remove either per-request check — CLAUDE.md is right about that;
reduce their cost instead.

**Effort:** M
**Priority:** P2
**Depends on:** None

### An authenticated request now leases three pooled connections

**What:** Set an explicit `Max Pool Size` and a short `Connect Timeout`, and reconsider whether the
account check can share the request's existing connection.

**Why:** Signing key, account check, then the controller's own repository call — each opens its own
`SqlConnection` (connection-per-method). The connection string sets neither limit, so the default
pool of 100 saturates at roughly a third of the previous concurrency and then blocks for 15 seconds
before throwing *inside* the authentication middleware, where (until the exception-handler item
above lands) it becomes an unhandled 500. A load spike presents as a total auth outage rather than
as latency.

**Context:** The two reads genuinely cannot be merged into one round trip: the key must be read
before signature validation, and the `userId` claim is only trustworthy after it. Caching the key is
the real fix.

**Effort:** S
**Priority:** P2
**Depends on:** The signing-key cache above

### `GetCredentialAsync` uses two round trips

**What:** Fold the credential and role-ids queries into one `QueryMultipleAsync`.

**Why:** `AuthRepository.cs:33-44` issues them sequentially on one connection. Both are seeks and it
is not an N+1 — the second runs once, not per row — and it is on the login path only. Flagged purely
because it is trivially removable.

**Effort:** S
**Priority:** P4
**Depends on:** None

### Drop `System.IdentityModel.Tokens.Jwt`

**What:** Replace `new JwtSecurityTokenHandler().WriteToken(token)` with `JsonWebTokenHandler` and
remove the package reference.

**Why:** `CMS.API.csproj:16` pins the package purely for the legacy handler, while
`Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.0 already brings
`Microsoft.IdentityModel.JsonWebTokens`, whose `JsonWebTokenHandler` is what the middleware itself
uses for validation on .NET 8+. The pinned 8.0.1 may also differ from the version JwtBearer 9.0.0
resolves.

**Effort:** S
**Priority:** P4
**Depends on:** None

---

## Maintainability

### The default landing route is written out in four places

**What:** Export one `DEFAULT_LANDING` constant and import it everywhere.

**Why:** `login.ts:12` (`DEFAULT_LANDING`), `admin.guard.ts:9` (`NON_ADMIN_LANDING`), and two
literals in `app.routes.ts:21` and `:216`. CLAUDE.md and `features.md` both document it as a *pair*
and tell the reader to keep the two equal — someone following that instruction will update
`app.routes.ts` and `login.ts` and silently leave a non-Admin redirecting to a stale target. This
release added the third named copy.

**Context:** Collapsing them lets the CLAUDE.md drift warning be deleted too.

**Effort:** S
**Priority:** P2
**Depends on:** None

### `ADMIN_ROLE` documents the cross-stack contract one-way

**What:** Extend the `ADMIN_ROLE` docstring to point back at `Security/AppRoles.cs`.

**Why:** `AppRoles.cs` carries a long comment naming its TypeScript counterpart and explaining that
the API's check is case-sensitive while the browser's deliberately is not. `auth.service.ts:15-16`
says only "the role that unlocks the 系統管理 Admin sidebar group" — naming neither `AppRoles.Admin`
nor `adminGuard`, which now also depends on it. The description is understated too: since this
release the constant gates a whole route branch, not a menu group.

**Effort:** S
**Priority:** P3
**Depends on:** None

### `admin.guard.spec.ts` re-implements `seedSession()`

**What:** Have `seedRoles()` call the exported `seedSession()` helper.

**Why:** `admin.guard.spec.ts:25-30` imports `AUTH_STORAGE_KEY` purely to hand-roll the
`sessionStorage.setItem` that `seedSession` already performs. `docs/claude/testing.md`, updated in
this same release, points every new spec at `seedSession()`; this is the one new spec that bypasses
it.

**Effort:** S
**Priority:** P3
**Depends on:** None

### Malformed XML doc comment

**What:** Close the `<para>` opened at `AuthorizationTests.cs:244` before `</summary>` at `:249`.

**Why:** It is the only unbalanced `<para>` in the changeset, so it reads as a typo, and it will
trip doc-comment tooling if XML doc generation is ever enabled for the test project.

**Effort:** S
**Priority:** P4
**Depends on:** None

---

## Simplification

These are advisory: structure suggestions, not defects. Net −17 lines if all three are taken.

### `IJwtTokenService` has one implementation and no fake

**What:** Delete the interface; inject `JwtTokenService` directly.

**Why:** `JwtTokenService.cs:9`. Unlike the repository interfaces, nothing fakes it — `TestTokens.cs:53`
constructs the concrete type. It is not a repository, so the house interface-per-repository
convention does not cover it. (−7 lines)

**Effort:** S
**Priority:** P4
**Depends on:** None

### `auth.service.ts`'s `revision` counter is hand-rolled cache invalidation

**What:** Replace `revision` plus the re-reading `computed` with a plain
`signal<AuthProfile | null>` seeded once from storage.

**Why:** `auth.service.ts:35-41` bumps a counter on every write purely to force `profile` to re-read
`sessionStorage`. The stated rationale at `:83` is that another tab may have written the token — but
session storage is per-tab, so that cannot happen, and nothing outside this service writes the
`cms-auth` key. (−8 lines)

**Effort:** S
**Priority:** P4
**Depends on:** None

### `login.scss` gradient mixes a token with a raw hex

**What:** Use `var(--cms-sidebar-active)` for the second stop.

**Why:** `login.scss:8` reads one endpoint through a theme token and hardcodes the other as
`#1f2937`, which `styles.scss:10` already defines as `--cms-sidebar-active`. Half the gradient
follows a palette change and half does not. The two stops are also close enough that the gradient
reads as a flat fill at card scale. (−2 lines)

**Effort:** S
**Priority:** P4
**Depends on:** None

---

## Verification

### Re-run the two live verification tables in the specs

**What:** Re-capture `spec/auth/Login.md`'s live login probe and `spec/auth/Authorization.md`'s
eight-row API + browser table.

**Why:** Both were captured on 2026-09-08, **before** the Admin role gate shipped, so their role
rows no longer describe this API — `GET /api/app-roles` with a non-Admin token is now `403`, not the
`200` the table records. `Authorization.md` now carries a banner saying so. The database
preconditions were re-confirmed by read-only probe during this release (key present and 32 chars,
`PasswordHash` column present, CI collation, exactly-cased `Admin` role row), but the endpoint and
browser checks themselves were not re-run.

**Context:** Needs `dotnet run --project CMS.API` plus `npm start` and a live sign-in.

**Effort:** S
**Priority:** P2
**Depends on:** None

---

## Completed

_(nothing yet)_
