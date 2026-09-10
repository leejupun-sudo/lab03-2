# Build Spec for My Profile (Auth)

- database schema: `.\database\auth.sql` — `AppUser` (one column written), `AppUserRole` (not touched)
- related specs: `spec/auth/Login.md` (the token this feature reads the account from),
  `spec/auth/Authorization.md` (the gate it sits behind), `spec/auth/AppUser.md` (the admin-side
  maintenance of the same row)

## Summary

The third non-CRUD feature: one endpoint and one page, letting a signed-in user change **their own
姓名 and nothing else**.

| Item | Detail |
|------|--------|
| Route | `PUT /api/auth/profile` (case-insensitive, so `/api/Auth/profile` reaches it) |
| Request | `{ userName }` — that is the whole DTO |
| Success | `200` `{ userId, userName }` |
| Failure | `400` blank/overlong name · `401` no or bad token, **or the account was deleted or deactivated** · `404` only on the delete-during-request race |
| Account written | taken from the token's `userId` claim, **never from the body** |
| Tables written | `AppUser.UserName`, one row, one column |
| Angular page | `/my-profile` — 我的帳號 My Profile, reached from the header user chip |

---

## The rule, and where it is actually enforced

> The user may change their own `UserName`. They may not change *whose* name it is, and they may
> not change anything else about themselves.

Three layers hold that, and only the first two matter:

1. **`UpdateProfileRequest` has exactly one property.** There is no `userId` to bind, no
   `roleIds`, no `isActive`. A body carrying them parses fine and every extra key is dropped on
   the floor — the endpoint cannot read what the model does not declare. A test PUTs
   `{ userId, userName, roleIds, isActive, passwordHash }` and asserts only the name moved.
2. **`AuthRepository.UpdateUserNameAsync` writes one column of one row**, keyed on the account
   from the token: `UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId`. `UserId` is
   the clustered primary key and the `AppUserRole` FK target — immutable here for the same reason
   `AppUsersController` never writes it. Roles are not on this table at all, so there is no
   junction sync to leave out.
3. The Angular page renders 帳號 and 角色 as text with no form control. That is a statement of
   what the endpoint does, not the thing enforcing it.

The account comes from `User.FindFirstValue("userId")` — the claim `JwtTokenService` writes and
`Program.cs` configures as `NameClaimType`. A token that somehow carries no such claim gets a
`401` rather than a guessed identity.

### `[AllowAnonymous]` had to move off the controller class

`AuthController` was decorated `[AllowAnonymous]` at class level, which was correct while it held
only `POST /api/auth/login`. It is now on the **login action alone**, and must stay there.

`[AllowAnonymous]` on the class wins over `[Authorize]` on an action inside it: the authorization
middleware skips the endpoint the moment it finds `IAllowAnonymous` in its metadata, and never
evaluates the policy. A class-level opt-out would therefore have left `PUT /api/auth/profile`
publicly writable, with no attribute able to close it — and the endpoint's entire security is
that there *is* an authenticated identity to read the account from. Moving the attribute down
restores the blanket `MapControllers().RequireAuthorization()` (`spec/auth/Authorization.md`) over
the profile endpoint. Two tests pin both halves: the profile endpoint `401`s anonymously, and
login still succeeds without a token.

### The token is not re-issued

A rename leaves the outstanding access token alone, so its `userName` claim keeps the pre-rename
value. Nothing reads that claim — the shell renders the profile from session storage, and the API
authorizes on `userId` and `role` — and re-issuing would quietly restart the 24-hour expiry as a
side effect of an edit that has nothing to do with the session's lifetime.

The Angular service therefore rewrites **only** `userName` on the stored profile, keeping
`userId` and `accessToken`; a test asserts the token is byte-identical afterwards.

### Blank names

`[Required]` is enough: `RequiredAttribute` trims a string before its emptiness check, so `""`,
`" "` and `"\t"` are all `400` and the repository is never reached. The controller trims again
before storing, which is what makes `"  Miles  "` land as `Miles`.

Angular needs both `Validators.required` **and** `Validators.pattern(/\S/)` for the same rule —
`required` alone accepts whitespace. Same pairing as `FeaturedPromoItemForm`.

### `401` for a deleted account — the `404` survives only for the race

This section previously argued for `404`: the token is genuine, so `401` would be a lie about the
credential. `Security/ActiveAccountEvents.cs` overtook that reasoning. It re-reads the account on
**every** authenticated request and calls `context.Fail(...)` when the row is missing or
`IsActive = 0`, so a token for a deleted account never reaches this controller — the pipeline
answers `401` first, and that is the right answer after all: the credential really is finished,
which is also what makes the Angular interceptor clear the session and return to `/login`. A
`404` would have left the user staring at an error on a page they can no longer use.

`UpdateUserNameAsync` returning `false` still answers `404` 查無使用者, but the only way to reach
it now is the delete-during-request race — the row disappearing between the middleware's check and
the `UPDATE`. `AuthProfileTests.UpdateProfile_ForAnAccountThatNoLongerExists_Returns401FromTheMiddleware`
pins the ordinary path; the race branch is deliberately untested, because the current fake backs
both calls with one dictionary and cannot express it.

---

## Frontend

`features/auth/my-profile/` — beside `features/auth/login/`, since this is not a table feature and
has no list/detail/form triple. One route, `/my-profile`, inside the guarded pathless parent, so
it is covered by `authGuard` by construction.

### 帳號 and 角色 come from the session, not from a request

There is no `GET /api/auth/profile`. 帳號 is the stored profile's `userId`; 角色 are the token's
`role` claims, decoded in the browser exactly as the role-gated sidebar already decodes them
(`spec/auth/Authorization.md` — "The Admin gate is real — three layers, only one of which is the boundary"). No extra call, and one
place that reads roles rather than two.

The cost is that both are as of sign-in: an admin who changes a user's roles mid-session does not
change what this page shows until the next login. That is already true of the sidebar, and this
page shows the same values from the same source, so it cannot disagree with the menu beside it.

### The header user chip is the way in

`app.html`'s signed-in user chip became a `routerLink` to `/my-profile`, keeping its
`data-testid="signed-in-user"` and gaining a 我的帳號 label. **No sidebar entry** — the page
belongs to whoever is signed in rather than to 首頁 / 系統管理 / 課程管理, and `app.spec.ts`
asserts `/my-profile` is *absent* from the nav href list.

### Saving refreshes the shell

`AuthService.updateProfile()` writes the new name back into session storage on success, and
`AuthService.userName` — which the header binds to — is computed off that storage, so the header
updates with no event, no reload and no second source of truth. The form then re-patches from the
**response**, not from what was typed, so the box shows exactly what was stored (trimmed).

---

## Files

| Layer | File |
|-------|------|
| Request DTO | `src/CMS.API/Models/UpdateProfileRequest.cs` — **new**, one property |
| Response DTO | `src/CMS.API/Models/UserProfileResponse.cs` — **new** |
| Repository | `src/CMS.API/Repositories/IAuthRepository.cs`, `AuthRepository.cs` — `UpdateUserNameAsync` |
| Controller | `src/CMS.API/Controllers/AuthController.cs` — `UpdateProfile`; `[AllowAnonymous]` moved to `Login` |
| Backend tests | `src/CMS.API.Tests/AuthProfileTests.cs` — **new**; `FakeAuthRepository` gained the write |
| NG model | `src/CMS.NG/src/app/core/models/auth.model.ts` — `UpdateProfileRequest`, `UserProfile` |
| NG service | `src/CMS.NG/src/app/core/services/auth.service.ts` — `updateProfile()` |
| NG page | `src/CMS.NG/src/app/features/auth/my-profile/` — **new** |
| NG shell | `src/CMS.NG/src/app/app.html`, `app.scss`, `app.routes.ts` |

Nothing was added to `Program.cs`: `IAuthRepository` was already registered, and the blanket
`RequireAuthorization()` already covers the new endpoint.

---

## Tests

### Backend — `AuthProfileTests`, 18 cases (reusing `AuthApiFactory`)

The profile endpoint lives on `AuthController`, so it reuses that controller's factory and fake
rather than adding a ninth pair. `FakeAuthRepository` gained `UpdateUserNameAsync` mirroring the
SQL — same CI key match, one column written, `false` on no row — plus an `UpdateCount` so a test
can prove validation ran *before* any write, and a `Row()` accessor for asserting what did not
move.

- renames the token's account; response is exactly `{ userId, userName }`
- a different `TokenUserId` renames a different row, and an uppercased one still matches (CI)
- `"  Miles Sun  "` is stored trimmed
- a `userId` in the body is ignored — the token's row moves, the named one does not
- `roleIds` / `isActive` / `passwordHash` in the body change nothing
- `""`, `" "`, `"\t"`, a missing property, and a 201-character name each `400` with `UpdateCount == 0`
- anonymous → `401` + `WWW-Authenticate: Bearer`, nothing written; a garbage token → `401`
- login is still reachable without a token (the moved `[AllowAnonymous]` still covers it)
- a token for a deleted account → `401` from the bearer middleware, nothing written

### Frontend — 27 Karma cases

- **page** (`my-profile.spec.ts`): shows the signed-in `UserId` with no input and no control for
  it (the form's only control is `userName`); renders the token's roles as text with no editor,
  handles a bare-string claim and the no-roles empty state; pre-fills the stored name; PUTs
  `{ userName }` and nothing else; trims; on success updates session storage and
  `AuthService.userName` while keeping `userId` and the token; shows the stored name rather than
  the typed one; blank and whitespace names make no call and show 姓名為必填; a failed save leaves
  the stored name alone; 還原 restores it
- **service** (`auth.service.spec.ts`): `updateProfile()` PUTs `{ userName }` to `/Auth/profile`;
  replaces only the name in session storage; writes nothing to local storage; a failure changes
  nothing; a call with nobody signed in stores nothing
- **shell** (`app.spec.ts`): the header chip links to `/my-profile` and says 我的帳號, and
  `/my-profile` is **not** in the sidebar href list; renaming through the service refreshes the
  header text

### Not exercised against the live database

Every other feature's spec records a live probe. This endpoint's only action is a **write** to the
one live `AppUser` row, and `CLAUDE.md` rules out writing to the live database to "verify" an
endpoint without asking, so it was not run. The xUnit suite covers the write against the in-memory
fake.
