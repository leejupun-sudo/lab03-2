# Testing conventions

Read this before writing or changing xUnit or Karma tests.

## Backend — `src/CMS.API.Tests`

`CMS.API.Tests` hosts the real pipeline with `WebApplicationFactory<Program>` and swaps only
the repository under test for an in-memory fake. This exercises routing, model binding,
DataAnnotations validation and JSON casing without a database. `Program.cs` ends with
`public partial class Program;` to make that possible — keep it.

- **Every factory derives from `ApiFactory`** (`ApiFactory.cs`). Since `Program.cs` applies
  `MapControllers().RequireAuthorization()`, three things hold for every suite: the base fakes
  `ISysConfigRepository` — the bearer middleware reads its signing key from that row per request,
  and no test may go to SQL Server for it — the base also fakes `IAuthRepository` as
  `factory.Accounts`, because `ActiveAccountEvents` re-reads the account on every request for
  exactly the same reason, and `CreateClient()` returns a client already
  carrying a token, minted by the **production** `JwtTokenService` via `TestTokens` so a test can
  never authenticate with a token shaped unlike the real one. Subclasses override
  `ConfigureFakes(IServiceCollection)` instead of `ConfigureWebHost`. Use
  `factory.CreateAnonymousClient()` for the 401 case, and set `TokenUserId` / `TokenRoles` before
  `CreateClient()` to sign in as someone else.
- **`TokenUserId` must name a seeded account or nothing authenticates.** `Accounts` holds three:
  `miles@uuu.com.tw` (active, Admin + User — the default), `helen` (**inactive**) and
  `Jenny_Tsao` (active, **no roles**). A token for an unseeded id now `401`s rather than sailing
  through, which is the point — but it turns a typo into a puzzling 401. `Accounts.SetActive(...)`
  and `Accounts.Remove(...)` simulate an admin deactivating or deleting someone mid-session, and
  `Accounts.AccountCheckCount` proves the middleware really consulted the row.
- **Setting `TokenRoles` decides 200 vs 403 on 系統管理.** The default carries `Admin`, so the
  existing suites for `AppUsersController` / `AppRolesController` / `PublishStatusesController`
  pass unchanged; drop to `["User"]` or `[]` to assert the role gate.
- **A test that simulates a missing `appConfig` row must keep `symmetricSecurityKey`** — blank
  only the field under test. Nulling the whole config fails authentication instead, and the 500
  you assert is then the middleware, not the code you meant to exercise.
- **One factory + one fake per feature**: `AppRoleApiFactory`, `AppUserApiFactory`,
  `PublishStatusApiFactory`, `CourseGroupApiFactory`, `PartnerApiFactory`,
  `CourseApiFactory`, `FeaturedPromoItemApiFactory`, plus `LookupApiFactory` for
  `LookupsController`. `AppUserApiFactory` swaps a second repository too —
  the inherited `FakeSysConfigRepository`, so the default password is a known constant and the
  tests can assert its SHA-256 reached the user fake. The unit is the **controller**, not the
  endpoint: `AuthControllerTests` and `AuthProfileTests` share `AuthApiFactory` because both
  endpoints live on `AuthController`.
- **Construct a fresh factory per test** — the fakes hold mutable state. A test may seed
  extra rows through `factory.Repository` before `CreateClient()` (the lookup cap test does).
- **Seed data that exercises the rules**: a referenced row so the delete guard has
  something to block, duplicate names where duplicates are legal, rows just outside a date
  boundary, rows seeded out of order so the `ORDER BY` is observable.
- **A fake must mirror the SQL, including its ordering** — otherwise the test passes while
  the endpoint returns rows in the wrong order. Mirror the filter semantics too (half-open
  date ranges, case-insensitive LIKE, `TOP n` after ordering).
- **Computed C# properties need a raw-JSON assertion.** `LookupItem`'s `Label` is a
  `get`-only expression, so `ReadFromJsonAsync` recomputes it client-side and an equality
  check proves nothing. Parse with `JsonDocument` and read `.GetProperty("label")` to prove
  the value reaches the Angular `optionLabel="label"` binding. Same for `DateOnly` — assert
  the `yyyy-MM-dd` string on the wire.
- Each test class keeps a private `ProblemDetailsDto { Title, Detail }` to assert 409 bodies.

## Frontend — Karma + Jasmine

Standard TestBed providers for a PrimeNG page: `provideRouter([])`,
`provideNoopAnimations()`, `providePrimeNG({ theme: { preset: Aura } })`, `MessageService`,
`ConfirmationService`, plus jasmine spies for the data services and an `ActivatedRoute`
stub with `snapshot.paramMap` / `queryParamMap` via `convertToParamMap`.

- Assert against `data-testid` attributes. Clear `sessionStorage` around list-page specs — and
  around **any** spec that renders `App`, since the signed-in profile lives there too.
- **The app shell only renders for a signed-in user.** A spec that touches the sidebar, the
  header, or a nav href must seed a session first; `auth.service.spec.ts` exports `makeToken()`,
  `makeProfile()` and `seedSession()` for that, and the seeded token's `role` claims decide which
  nav groups render.
- Each `*.service.spec.ts` exports a `make{Entity}()` factory the component specs import.
- Reach protected members through a local `Internals` interface cast, as the existing specs
  do, rather than making members public for tests.
- Components with signal inputs are configured with `fixture.componentRef.setInput()` before
  the first `detectChanges()`; outputs are observed with `componentInstance.x.subscribe()`.
- A spy on `MessageService` must be created **after** `TestBed.configureTestingModule` and
  **before** `createComponent` if the toast fires during init — pass a hook into the spec's
  `setup()` for that.
- Do not mock the clock (`jasmine.clock()` conflicts with `fakeAsync`). For date-dependent
  pages, pin the date through the page's own saved state (session storage) and compute the
  "today" expectation with the same util the component uses.
- Adding a nav entry breaks `app.spec.ts` if it asserts an exact href list — update it in
  the same commit.
- The seeded token's `role` claims now drive `adminGuard` as well as the sidebar. A spec that
  navigates to 使用者／角色／發布狀態 needs an `Admin` claim in its session, or the guard redirects
  to `/featured-promo-items`. `seedSession()`'s default profile carries `['Admin', 'User']`.
- Any spec exercising an HTTP call runs `authInterceptor`, which resolves `MessageService`
  **optionally** — so a spec need not provide one. Provide it only to assert the 403 toast.
- Run headless: `$env:CHROME_BIN` must be set (see `CLAUDE.md`).
