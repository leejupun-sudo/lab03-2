# Build Spec for Login (Auth)

- database schema: `.\database\auth.sql` — `AppUser`, `AppUserRole`, `SysConfig`
- related spec: `spec/auth/AppUser.md` (the `PasswordHash` contract this feature inherits)

## Summary

The first feature in this repo that is **not** a table CRUD. It is one endpoint,
`POST /api/auth/login`, which verifies a credential against `AppUser` and issues a signed
JWT access token. There is no list, no detail, no form, and no Angular page — the frontend
is out of scope for this change.

| Item | Detail |
|------|--------|
| Route | `POST /api/auth/login` (routes are case-insensitive, so `/api/Auth/login` reaches it) |
| Request | `{ userId, password }` — both `[Required]` |
| Success | `200` `{ userId, userName, accessToken }` |
| Failure | `401` + `ProblemDetails`, one generic message for every cause |
| Tables read | `AppUser` (incl. `PasswordHash`), `AppUserRole`, `SysConfig` |
| Tables written | **none** — login is a pure read |
| Token lifetime | 24 hours |
| Signing | HMAC-SHA256, key from `SysConfig.appConfig.symmetricSecurityKey` |

### Live data (read-only probe, 2026-09-08)

| Measure | Value |
|---------|-------|
| `AppUser` rows | 1 — `admin@example.com`, `IsActive` 1, one role `Admin` |
| Stored `PasswordHash` | 64 lowercase hex; equals `SHA256(UTF8("CMS4fun#"))`, the `defaultPassword` |
| `symmetricSecurityKey` | present in the `appConfig` JSON, **32 characters** |
| `UserId` collation | `Chinese_Taiwan_Stroke_CI_AS` — case-insensitive |

---

## The three credential checks

All three must pass. Each is checked separately server-side so the log records *why*, but
the client always receives the same body.

1. **The account exists.** `SELECT ... FROM AppUser WHERE UserId = @UserId` — a seek on the
   clustered primary key.
2. **`IsActive` is 1.** A disabled account with the correct password is still rejected.
3. **`SHA256(UTF8(password))` equals `PasswordHash`.** Reuses `Security/PasswordHasher.cs`,
   the same hasher that writes the hash on create and reset — one format, one place.

### Why the failures are indistinguishable

Every rejection returns `401` with `title` 登入失敗 and `detail` 帳號或密碼錯誤。 —
byte-identical, asserted by a test that compares the three bodies. Distinguishing "no such
account" from "wrong password" turns the endpoint into an account-existence oracle, and
distinguishing "disabled" leaks who has been suspended. The server-side `ILogger` lines carry
the real reason.

### `UserId` matching follows the column collation

The lookup is a plain SQL equality on a `Chinese_Taiwan_Stroke_CI_AS` column, so it is
case-insensitive. This is deliberate and not a loosening: `AppUserRepository.UserIdExistsAsync`
enforces uniqueness case-insensitively on create, so two accounts differing only in case
**cannot exist**. An ordinal comparison here would reject a valid user for typing
`Admin@example.com`, with no security gained. There is no trimming and no `LIKE` — the match
is otherwise exact.

### The hash comparison is constant-time

`CryptographicOperations.FixedTimeEquals` over the two hex strings' bytes, with the stored
value lowercased first so a row written in uppercase hex still verifies. An ordinary
`string ==` exits at the first differing character and leaks a timing signal.

---

## The token

`Security/JwtTokenService.cs`. Claims written, all with **short names** (no `ClaimTypes.*`
URIs — `JwtSecurityTokenHandler` only remaps the long forms, so these pass through verbatim
and a reader needs `MapInboundClaims = false` to see them unchanged):

| Claim | Value |
|-------|-------|
| `sub` | `AppUser.UserId` |
| `jti` | fresh GUID per token |
| `userId` | `AppUser.UserId` |
| `userName` | `AppUser.UserName` |
| `role` | one claim **per** `AppUserRole.RoleId`; a user with no roles gets none |
| `nbf` / `exp` | issue instant and issue + 24 h |

No `iss` and no `aud`. These tokens are issued and validated by the same process against a key
only it holds, so there is no second issuer to distinguish and no other audience to exclude;
declaring values no validator checks would be decoration. `spec/auth/Authorization.md` — the
change that added the bearer middleware — switches `ValidateIssuer` and `ValidateAudience` off
to match, and that is the one thing to revisit if this API ever accepts tokens from elsewhere.

> **Superseded:** this spec originally recorded that the API adds no authentication middleware
> and `[Authorize]`s no endpoint. That is no longer true — see `spec/auth/Authorization.md`.
> The token's shape and the login endpoint itself are unchanged; the claims above are exactly
> what the middleware now validates and what the Angular sidebar reads for role gating.

### The signing key is read at runtime, never hard-coded

`ISysConfigRepository.GetAppConfigAsync()` already parses the `appConfig` row (compat level
100 rules out `OPENJSON`, so the JSON is parsed in C#). `AppConfig` gained a
`SymmetricSecurityKey` property — the type was previously kept narrow on purpose so the key
could not leak, but `DefaultPassword` is an equally sensitive secret in the same class, so the
real rule is the one now stated in its doc comment: **`AppConfig` is never returned by a
controller and never logged.**

The key is fetched per token, so editing the `SysConfig` row rotates it with no redeploy — a
test asserts exactly that. `spec/auth/Authorization.md` moved the read into a shared
`Security/JwtSigningKey.cs` and gave the bearer middleware the same per-request read, so
rotation now invalidates outstanding tokens just as immediately as it changes new ones.

### Deployment faults are 500, not 401

A missing `appConfig` row, a blank `symmetricSecurityKey`, or a key under 32 bytes throws
`InvalidOperationException`. HMAC-SHA256 requires a 256-bit key, and the live key is exactly
32 bytes — one character shorter and signing would fail. This mirrors
`AppUsersController.DefaultPasswordHashAsync`: a broken deployment is not a credential
problem and must not be reported as one.

---

## Files

| Layer | File |
|-------|------|
| Request DTO | `src/CMS.API/Models/LoginRequest.cs` |
| Response DTO | `src/CMS.API/Models/LoginResponse.cs` |
| Internal model | `src/CMS.API/Models/AppUserCredential.cs` — the **only** model with `PasswordHash` |
| Config | `src/CMS.API/Models/AppConfig.cs` (+ `SymmetricSecurityKey`) |
| Repository | `src/CMS.API/Repositories/IAuthRepository.cs`, `AuthRepository.cs` |
| Token service | `src/CMS.API/Security/JwtTokenService.cs` |
| Signing key (shared) | `src/CMS.API/Security/JwtSigningKey.cs` — added by `spec/auth/Authorization.md` |
| Hasher (reused) | `src/CMS.API/Security/PasswordHasher.cs` |
| Controller | `src/CMS.API/Controllers/AuthController.cs` |
| DI | `src/CMS.API/Program.cs` — `IAuthRepository`, `IJwtTokenService`, `TimeProvider.System` |
| Package | `System.IdentityModel.Tokens.Jwt` **8.0.1** (both projects — raised from 6.35.0 by `spec/auth/Authorization.md`) |
| Tests | `src/CMS.API.Tests/AuthControllerTests.cs`, `AuthApiFactory.cs`, `FakeAuthRepository.cs`, `FixedTimeProvider.cs` |

### `AppUserCredential` is the containment boundary

`AppUser` has no `PasswordHash` property by contract, and that stays true. The hash needed a
home for the comparison, so it got a separate backend-only model that no controller returns.
`LoginResponse` is a distinct type with exactly three properties; a test asserts the response
JSON has exactly those three keys, and that neither the body nor the decoded token payload
contains the hash or the plaintext.

### `TimeProvider` is injected

So the 24-hour expiry is asserted exactly rather than within a tolerance. The tests pin the
clock with `FixedTimeProvider`; `Program.cs` registers `TimeProvider.System`.

---

## Tests

`AuthControllerTests` — 16 cases, no database:

- valid active user → `200`, correct `userId` / `userName`, a three-segment JWS
- response JSON has exactly `accessToken`, `userId`, `userName`
- wrong password / unknown `UserId` / `IsActive = 0` → `401` each
- the three `401` bodies are byte-identical and name neither the account nor the failing check
- a rejection carries no `accessToken` key at all
- blank `userId` or blank `password` → `400`, and the repository is never consulted
- the hash and the plaintext appear in neither the body nor the decoded token payload
- the token carries `userId`, `userName` and `sub`
- both `AppUserRole` rows become `role` claims; the roleless user gets none
- `exp − nbf` is exactly 24 h and `nbf` is the pinned issue instant
- the signature verifies against the `SysConfig` key and fails against any other
- changing the `SysConfig` key changes the signing key

The fake seeds take **plaintext** passwords and hash them with the production
`PasswordHasher`, so a test can never assert against a hand-written digest the real hasher
would not produce.

### Verified against the live database (2026-09-08)

The endpoint was exercised on `.\SQLEXPRESS` with the real `SysConfig` key: the one live
account returned `200` with a token whose payload is
`{sub, jti, userId, userName, role: "Admin", nbf, exp}` and whose `exp − nbf` is 24 hours;
a wrong password and an unknown account each returned the identical `401`. Login writes
nothing, so this probe was read-only.
