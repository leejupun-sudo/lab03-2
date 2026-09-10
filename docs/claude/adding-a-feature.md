# Adding a feature

Read this before scaffolding a new table, and before running the `/crud` skill.

## Procedure

1. Read the DDL, then **probe the live DB** — see `docs/claude/schema-traps.md` for the
   checklist: row counts, distinct-vs-total on every candidate natural key, unique indexes,
   which tables actually reference this one (constraints *and* unconstrained columns), and
   the `ON DELETE` action of every FK in both directions.
2. Write `spec/{sub-system}/{Table}.md` from `spec/feature-spec.template.md`.
   `spec/sample1.spec.md` (Course, FK+N-N heavy) and `spec/sample2.spec.md` (SkillTrain,
   simpler) show the depth expected; `spec/admin/PublishStatus.md`,
   `spec/course/Course.md` and `spec/promotion/FeaturedPromoItem.md` are real worked
   examples. If the customer supplied a UI spec (`custom/{Table}/`), the build spec is
   written *from* it and records every deviation from the house pattern. Record every
   judgment call and its reason — that is what the spec is for.
3. Backend: models, `I{Table}Repository` + implementation, controller, then **register the
   repository in `Program.cs`** — a missing registration only fails at request time. Add a
   `GET /api/lookups/{plural}` if anything FKs to this table.
4. Frontend: model + service in `core/`, three components under `features/`, four lazy
   routes (`/new` before `/:id`), and a nav entry in the `navGroups` signal in `app.ts`.
   A customer mockup can override the three-page shape (FeaturedPromoItem is one route with
   an inline form) — say so in the spec.
5. Add tests on both sides — `docs/claude/testing.md`. Update `app.spec.ts` for the nav.
6. Verify: `dotnet build`, `dotnet test`, `ng build`, `ng test`, then read-only probes of
   the new endpoints against the live DB. **Kill the `dotnet run` process afterwards** — a
   surviving `CMS.API.exe` locks the output file and the next build fails with MSB3027.
7. Add the feature's decisions to `docs/claude/features.md` and its files to the spec.

## Gaps between the `/crud` skill and this codebase

The skill's step list is not fully implementable here yet. Do not silently skip these —
say so in the report.

- **RowAudit does not exist.** The skill asks for a `RowAuditWriter` injected into every
  repository and a `RowAuditBadgeComponent` in the detail/form toolbars. `admin.sql` has a
  `RowAudit` *table*, but there is no C# writer and no Angular component, and no implemented
  feature uses either. Building that infrastructure is separate work; until then, follow the
  AppRole shape and note the omission.
- **Primary-Foreign link buttons** need the child feature to exist first. `PublishStatus`,
  `CourseGroup`, `Partner` and `Course` ship usage *counts* as plain numbers because
  `/certifications`, `/promotion2s`, `/partner-course-groups`, `/seminars`, `/course-faqs`,
  `/course-related-links`, `/hot-courses` and `/course-recomms` are dead routes; the specs
  record the routes and query-param names as the contract. Live targets not yet wired:
  `/courses?partnerPkid|courseGroupPkid|publishStatusPkid=` (查看課程 buttons on the three
  parent detail pages), `/app-users?roleId={roleId}` (the user list does not read it yet),
  `/featured-promo-items?trainingCenterPkid=&weekOf=` (nothing links in yet).
  `FeaturedPromoItem` shows `TrainingCenterName` and `PromoCode` as plain text because
  `/training-centers` and `/promotion2s` are dead routes.
- **The junction heuristic can be wrong** — `docs/claude/schema-traps.md`.
