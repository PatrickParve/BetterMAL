## Context

A series is derived, not authored: `SeriesGraphBuilder` walks MAL's relation graph from a seed anime, resolves one story component, orders its main line, and persists it. Two things identify the result today:

- `Series.Id` — a Postgres identity column (`is_identity = YES, BY DEFAULT`), local to one database.
- `Series.RootAnimeId` — "the earliest main-line member; unique" (`Models/Series.cs`), with a unique index (`Data/AnimeTrackerDbContext.cs:196`).

`Series.Id` is the storage key: `SeriesMember` is keyed on `(SeriesId, AnimeId)` (`PK_SeriesMembers`) with `FK_SeriesMembers_Series_SeriesId ... ON DELETE CASCADE`, and the four selection endpoints take it in the path. `RootAnimeId` is the *display and navigation* value: the frontend routes on `/series/:animeId` and reads `/api/series/by-anime/{animeId}`, so the root anime id is what appears in URLs, while the series id only ever appears in `PUT/DELETE /api/series/{seriesId}/title|picture`, issued from a DTO the page just loaded.

Only the derived value travels. Every device that walks the same relations reaches the same root, so the root's MAL id names the same franchise everywhere; the identity counter names it only here. The choices the user has made — 105 of 255 stored series carry a chosen title or picture — hang off the counter.

Constraints this design works under:

- **The rebuild already reassigns identity.** `MatchToStoredSeries` picks the stored series overlapping the derived component the most as the survivor and deletes the rest. A merge can therefore hand the survivor a root it did not have, which under this change means a new id.
- **EF cannot update a key on a tracked entity.** `SeriesGraphBuilder.cs:1169-1172` already documents this for `SeriesMember`, and it applies with equal force to `Series.Id`. Moving a root is therefore a delete-and-insert, not an update.
- **The tests run on EF InMemory.** 73 `UseInMemoryDatabase` call sites, no shared factory. InMemory rejects `BeginTransaction` unless the warning is configured away, and does not support `ExecuteUpdate`/`ExecuteDelete` at all.
- **Backend is .NET 10 / EF Core 10 / Npgsql 10 on PostgreSQL.** The local SDK is 9.0, so builds go through the `sdk:10.0` Docker image (the `~/Documents` bind-mount caveat applies).
- **Live data has to survive.** 255 series, 1600 memberships, no orphans, every series holding a membership for its own root.

## Goals / Non-Goals

**Goals:**

- A series' id is the MAL id of the earliest main-line member, everywhere: in the table, in the membership key, in the API payloads, and in the selection endpoints' paths.
- Two machines building the same franchise from the same relation data reach the same id with no coordination, and a rebuild from an empty database reproduces the ids it had.
- One id, not two. The `RootAnimeId` column and the duplicated DTO field go.
- The 105 stored choices survive the rekey without re-picking, and survive a later root move.
- A root move is handled deliberately and atomically rather than crashing on a key update.

**Non-Goals:**

- Preventing the id from changing. A moved root moves the id; that is accepted, because the new id is equally derivable everywhere.
- Enforcing that an anime is main-line in at most one series. Explicitly excluded by the user; the relation data and the builder already produce it.
- Changing what a series *is* — traversal, main-line classification, version slots, folding, budgets and the single-flight gate are untouched.
- Exporting or syncing anything between machines. This change makes such a thing possible later; it does not build it.
- Making `Series.Id` a foreign key to `AnimeMetadata`.

## Decisions

### D1. `Series.Id` becomes the root anime id, and `RootAnimeId` is removed

The alternative — keep the identity column and treat `RootAnimeId` as "the identity at the API and storage boundary" — fails on the storage half. The thing a stored choice is really keyed on is `SeriesMember.SeriesId`, part of the membership primary key. Leaving that as the counter would mean the membership rows still cannot be carried anywhere, and would leave two answers to "which series is this" one join apart, with a unique index as the only thing keeping them in step. One column, holding the value that travels, removes the question.

*Consequence:* series ids and anime ids now share a number space, and for a root they are the same number. That is the point, but it makes `seriesId == animeId` comparisons meaningful where they were previously nonsense — see D6.

*Alternative rejected:* a content hash over member ids. Stable across machines, but changes whenever membership changes — which is far more often than the root moves — and is unreadable in a URL.

### D2. The column stops generating values; the builder supplies them

`entity.Property(e => e.Id).ValueGeneratedNever()` in `AnimeTrackerDbContext`, and `ALTER TABLE "Series" ALTER COLUMN "Id" DROP IDENTITY` in the migration. `PersistAsync` sets `Id = telling.RootAnimeId` on every insert.

*Consequence for tests:* a seeded `new Series { }` with no `Id` now means id 0, and two of them collide silently rather than getting 1 and 2. Every test that seeds a series must set `Id` — which is exactly the id the test's `RootAnimeId` used to hold, so the fix is mechanical.

### D3. `PersistAsync` gets three cases, not two

Today: matched (update in place) or unmatched (insert). Now the matched case splits on whether the derived root is the id the matched series already holds.

1. **No match** — insert with `Id = telling.RootAnimeId`, as today plus the id.
2. **Matched, root unchanged** — update in place, byte-for-byte today's path: the member rows are reconciled against `existingForTelling`, obsolete ones deleted, choices untouched.
3. **Matched, root moved** — the stored row cannot be re-keyed, and neither can its memberships. Delete the matched series row (its members cascade), then insert a fresh series at the new id with every membership rebuilt, carrying `SelectedTitle` and `SelectedPictureUrl` across from the row being deleted **before** it goes.

Case 3 is the merge case as much as the "MAL revealed an older prequel" case: a rebuild whose survivor is the largest-overlap series can perfectly well derive a root belonging to one of the series it is absorbing.

*Alternative rejected:* `ExecuteUpdateAsync` to rewrite the key columns in place, bypassing the change tracker. It works on Postgres and avoids the delete, but InMemory does not implement it, which would take every `SeriesGraphBuilder` test offline.

### D4. Case 3 saves twice, inside an explicit transaction

The id the re-rooted series must take is frequently held by a row this same build is deleting. So case 3 is: `SaveChangesAsync` the deletions (the old target plus every stale series), then add the new series and its members and `SaveChangesAsync` again, both inside `db.Database.BeginTransactionAsync`.

*Why not one `SaveChangesAsync`:* EF's batch preparer does order a delete ahead of an insert that reuses a key value, but this is a property of its internals rather than of the model, and the failure mode if it ever does not hold is a unique violation in the middle of a rebuild. Two saves make the ordering explicit and readable.

*Why the transaction:* between the two saves the franchise has no stored series. A crash there would leave the anime series-less — self-healing, since the next read rebuilds — but would lose that series' chosen title and picture, the one thing in the row that cannot be re-derived. The transaction also makes true what `PersistAsync`'s doc comment already claims about persisting a build as one unit.

*Why only case 3 opens it:* cases 1 and 2 keep their single implicit-transaction save, so the existing `SeriesGraphBuilder` tests keep working under InMemory untouched. Only the new re-root tests need `ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))` on their context options.

### D5. The id a build needs is always free by the time it takes it

The insert in case 3 targets `telling.RootAnimeId`, which is a core member of this build's component. Any stored series holding that anime as a **Core** member appears in `coreIdsByStoredSeriesId` — it is built from every membership of every anime in `fullIds`, and the root is in `fullIds` — so it is either the matched survivor or is stale and deleted in the same transaction. A stored series holding that anime only as a version neighbour is not a candidate, and cannot be rooted at it either: a series' root is a main-line member, and a main-line member is Core in its own series.

So the id is either the target's own (case 2) or is freed by the first save (case 3). This is an invariant worth a test, not just a comment. If it is ever violated the insert raises a unique violation and the transaction rolls back — loud, and with nothing half-written.

### D6. Root lookups go through the series id

`SeriesRankingIndex` finds a series' root with `members.First(m => m.AnimeId == m.RootAnimeId)`; it becomes `m.AnimeId == m.SeriesId`. The invariant it leans on — every series holds a membership for its own root — holds on the live database with no exceptions, and is structural: the root is `mainLineOrdered[0]`, always among the members persisted.

`ResolveFoldedPrimaryAsync`'s tie-break loses a query outright: it currently reads `db.Series.Where(s => s.Id == competingFold.SeriesId).Select(s => s.RootAnimeId)`, which is now `competingFold.SeriesId` itself.

Its other queries are unaffected by case 3's early delete: they only ever run against `competingFold.SeriesId`, which `rowsElsewhere` has already excluded from both the current series and the stale set, so no row they read is one the first save deleted.

### D7. The DTOs collapse to a single `seriesId`

`SeriesDto`, `SeriesListItemDto`, `SeriesSearchResultDto`, `TopSeriesItemDto` and `RewatchedSeriesItemDto` each carry `SeriesId` and `RootAnimeId`. They would now always be equal, and shipping two names for one number invites a consumer to assume they can differ. One field, `seriesId`, keeping the identity name; the surfaces that mean "the root anime" — the card link, the poster's anime id, `myanimelist.net/anime/…` on the series page — read it too, and a comment at each DTO records that the series id **is** the root entry's MAL id so the reader is not left wondering why an anime link takes a series id.

*The trade-off being taken:* if some later change ever froze the id while letting the root move, every one of those anime-flavoured reads would break at once. That is the same bet the user has already made in asking for one id, and it is recorded here rather than hidden.

*Alternative rejected:* keeping `rootAnimeId` as a duplicate for a release. Nothing consumes this API but this frontend, both ship together, and the duplicate is what the change exists to remove.

### D8. `Series.Id` is not a foreign key to `AnimeMetadata.Id`

It would be well-formed — the root always exists as a cached anime, verified across all 255 stored series — but `AnimeMetadata` deletions cascade, and this FK would turn deleting one cached anime row into deleting a whole franchise's series and memberships. The root's own `SeriesMember` row already carries an FK to `AnimeMetadata` with the narrower blast radius, and nothing needs the constraint.

### D9. The migration renumbers through negative space

A naive `UPDATE "Series" SET "Id" = "RootAnimeId"` collides: 12 stored series have a `RootAnimeId` equal to a *different* series' current `Id` (roots run as low as 1), and Postgres checks a non-deferrable unique index per row, not per statement. Negating first puts every existing key in a range no final value occupies:

```sql
UPDATE "SeriesMembers" SET "SeriesId" = -"SeriesId";
UPDATE "Series"        SET "Id"       = -"Id";
UPDATE "SeriesMembers" sm SET "SeriesId" = s."RootAnimeId" FROM "Series" s WHERE sm."SeriesId" = s."Id";
UPDATE "Series"        SET "Id"       = "RootAnimeId";
```

Every intermediate state is unique: the negated ids are distinct because the originals were; each remapped value is positive while everything not yet remapped is negative; the final values are unique because `RootAnimeId` carries a unique index, and `(RootAnimeId, AnimeId)` was verified to have no duplicate pair on the live data. The FK is dropped before this runs and re-added after — the FK would otherwise reject the very first statement.

*Alternative rejected:* dropping and re-creating the primary key as `DEFERRABLE INITIALLY DEFERRED` for the duration. More moving parts, and it leaves a differently-defined PK behind if the migration fails halfway.

### D10. `Down` restores the schema, not the original numbers

The identity values are not recoverable once overwritten, so `Down` re-adds `RootAnimeId` set from `Id`, re-adds its unique index, re-attaches identity generation and seeds the sequence past `MAX("Id")` so future inserts do not collide with the MAL-derived values it left behind. That is a working rollback of the schema with different numbering, and the doc comment says so plainly rather than implying a round trip.

## Risks / Trade-offs

- **A root move silently changes a series' id** → Nothing external holds one. The frontend re-reads the id from the DTO on every load, and the only id-bearing URLs are the selection endpoints, issued from a DTO the page just fetched. A stale id returns 404 through the existing `SeriesIdNotFoundException` path.
- **A crash mid-re-root loses a chosen title/picture** → D4's explicit transaction; the two saves commit together or not at all.
- **EF InMemory cannot open a transaction** → Only case 3 opens one, so existing tests are untouched; the new re-root tests ignore `InMemoryEventId.TransactionIgnoredWarning`.
- **`ValueGeneratedNever` turns an unset `Id` into 0** → Every series-seeding test sets `Id` explicitly, and the same edit removes `RootAnimeId` from those seeds, so the two cannot drift apart in a fixture.
- **The renumber is one-way** → Back up the Postgres volume before applying, and run the verification `SELECT`s (below) before and after. The pre-flight also proves there are no orphaned memberships, which would otherwise surface as an FK failure at the end of the migration.
- **Series ids and anime ids now share a namespace** → Deliberate. Doc comments on `Series.Id`, the DTO fields and `SeriesMember.SeriesId` say what the number is, and `SeriesIdNotFoundException`'s message keeps naming it a series id.
- **Two series could in principle both derive the same root** → Only if the same anime were main-line in two series, which the user has ruled out constraining. It is now enforced structurally anyway: the id is the primary key, so a second series at the same root cannot be written. The failure would be a unique violation on a rebuild, not silent duplication.

## Migration Plan

1. **Pre-flight, against the live database.** Record `SELECT count(*) FROM "Series"` (255), `count(*) FILTER (WHERE "SelectedTitle" IS NOT NULL OR "SelectedPictureUrl" IS NOT NULL)` (105), `count(*) FROM "SeriesMembers"` (1600); confirm zero orphaned memberships and zero series lacking a membership for their own root; snapshot the mapping `SELECT "Id", "RootAnimeId" FROM "Series" ORDER BY "Id"` and the 12 rows whose root equals another series' id.
2. **Back up the `postgres-data` volume.** The renumber is irreversible.
3. **Apply the migration** — drop FK, drop the `RootAnimeId` unique index, run D9's four statements, `DROP IDENTITY`, drop `RootAnimeId`, re-add the cascade FK.
4. **Verify.** Row counts unchanged (255/1600/105); `Series."Id"` now equals the snapshot's `RootAnimeId` for every row; every membership's `SeriesId` matches its series; the 12 collision rows landed on the right ids; spot-check that a series carrying a chosen title still carries it.
5. **Deploy backend and frontend together.** The DTO field is renamed, so a mixed deployment would leave the browser with an undefined series id.
6. **Rollback.** `Down` restores a working schema with root-derived numbering (D10); restoring the original identity values means restoring the volume backup.

## Open Questions

None blocking. One judgment call is recorded rather than asked: D7 removes `rootAnimeId` from the API payloads instead of keeping it as an alias, on the grounds that the duplicate field is the thing this change exists to delete and that the only consumer ships in the same deploy.
