# Testing conventions

Read this before writing or changing xUnit or Karma tests.

## Backend — `src/CMS.API.Tests`

`CMS.API.Tests` hosts the real pipeline with `WebApplicationFactory<Program>` and swaps only
the repository under test for an in-memory fake. This exercises routing, model binding,
DataAnnotations validation and JSON casing without a database. `Program.cs` ends with
`public partial class Program;` to make that possible — keep it.

- **One factory + one fake per feature**: `AppRoleApiFactory`, `AppUserApiFactory`,
  `PublishStatusApiFactory`, `CourseGroupApiFactory`, `PartnerApiFactory`,
  `CourseApiFactory`, `FeaturedPromoItemApiFactory`, plus `LookupApiFactory` for
  `LookupsController`. `AppUserApiFactory` swaps a second repository too —
  `FakeSysConfigRepository`, so the default password is a known constant and the tests can
  assert its SHA-256 reached the user fake.
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

- Assert against `data-testid` attributes. Clear `sessionStorage` around list-page specs.
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
- Run headless: `$env:CHROME_BIN` must be set (see `CLAUDE.md`).
