# Things the schema will not tell you — check the live DB first

Read this before writing a repository, a delete guard, or a `*ExistsAsync`. Read-only probes
against `.\SQLEXPRESS` are cheap and have caught a real bug in every feature so far. Run them
before writing the repository, not after. (Probe recipes: `CLAUDE.md` → Environment gotchas.)

## Probe checklist

For the table in hand:

1. Row count; `SELECT COUNT(DISTINCT col), COUNT(*)` on every candidate natural key.
2. `sys.indexes WHERE is_unique = 1` — unique indexes the DDL may omit.
3. `sys.foreign_keys` in **both** directions, with `delete_referential_action_desc`.
4. Grep the DDL for `{Table}_pkid` columns and for the natural-key column name — references
   with no constraint behind them.
5. Whether `pkid` is actually `IDENTITY`, and its exact type.

## The traps, each with the feature that hit it

- **A table that is an FK target needs a delete guard.** Almost no FK here declares
  `ON DELETE`, so deleting a referenced row raises an FK violation — a `500`, which the
  409 rule forbids. Carry usage-count subqueries in every SELECT and gate `DELETE` behind an
  `IsInUseAsync`, returning `409`. `PublishStatus`, `CourseGroup`, `Partner` and `Course` all
  do this; copy any of them (`Partner` is the five-count example). Check
  `delete_referential_action_desc` first: the two `Course` junctions **do** cascade and must
  *not* be counted, or nothing with a certification could ever be deleted.
- **A `UNIQUE` index can exist that the DDL never declares.** `course.sql` shows only
  `PK_Course`, but the live table carries `IX_Course_UniqueCourseId`. A duplicate `CourseId`
  is therefore a *database* violation, not just an application rule — without a
  `CourseIdExistsAsync` the INSERT 500s. Query `sys.indexes WHERE is_unique = 1` before
  deciding which columns get a `*ExistsAsync`.
- **An outbound FK can cascade the wrong way.** `FK_Course_CourseGroup` is
  `ON DELETE CASCADE`: deleting a `CourseGroup` row at the SQL level deletes every course in
  it. Only the `CourseGroup` feature's application guard stands between a stray `DELETE` and
  1084 courses. Never "simplify" that guard away.
- **Do not assume a "name" column is unique.** `CourseGroup.Description` has no `UNIQUE`
  constraint and the live table holds duplicates (215 rows, 213 distinct). Adding the usual
  duplicate check there would contradict the schema *and* make the existing twin rows
  uneditable — each would 409 against its own duplicate. The distinct-vs-total probe cuts
  both ways: on `Partner` it rules a duplicate check *out* for `Name` (66 / 64 distinct) and
  *in* for `AppKey` (66 / 66), in the same table.
- **Not every `pkid` is `IDENTITY`.** `PublishStatus.pkid` is a plain `tinyint`: the client
  supplies it on create, `INSERT` writes it explicitly, there is no `SCOPE_IDENTITY()`, and a
  duplicate is a `409`. Where the PK *is* IDENTITY, cast `SCOPE_IDENTITY()` to the column's
  own type (`smallint` for `CourseGroup`, not `int`).
- **A reference can exist with no `FOREIGN KEY` behind it.** `Seminar.Partner_pkid` points
  at `Partner.pkid` across 364 live rows, but `sys.foreign_keys` returns **0** constraints
  for `Seminar`. A guard built from the `.sql` files alone misses it, and deleting a partner
  referenced only by `Seminar` succeeds, orphaning the rows silently — `Partner` 122 is
  exactly such a row. **The reference may not even be by pkid:** `CourseRecomm` points at
  courses through the `CourseId` *string* (3118 rows, 895 already orphaned), so grep for the
  natural-key column name as well.
- **An FK-target PK is immutable** — never write it in `UPDATE`, and disable the control in
  the edit form. Same hazard as `AppRole.RoleId`, and the same reason `Course.CourseId` is
  frozen after creation.
- **A declared unique index on a composite key is still a 409, not a 500.**
  `FeaturedPromoItem` has `IX_FeaturedPromoItem_UniqueDateLocSlot` on three columns;
  `SlotTakenAsync` checks it before INSERT and UPDATE (excluding self). Reordering rows
  under such an index needs a park value inside one transaction (see its feature note).
- **Compatibility level is 100.** No `OPENJSON`, `STRING_AGG`, or `OFFSET/FETCH`;
  `TOP (@n)` is fine. Parse JSON config in C#.

## Junction table, or entity in its own right?

"Two FK columns and a name containing the parent table" is not enough to call something an
N-N junction. `PartnerCourseGroup` matches that pattern but has its own `pkid IDENTITY`, its
own payload (`DisplayOrder`, `Description`), and — decisively — `Promotion2` holds an FK to
*its* pkid. Delete-then-reinsert would hand every row a new pkid and orphan 387 live
`Promotion2` rows. Before treating a table as a junction, check that nothing FKs to it and
that it has no surrogate key of its own; a true junction here (`AppUserRole`,
`CourseInCertification`, `CourseJobCategories`) keys on the FK pair.
