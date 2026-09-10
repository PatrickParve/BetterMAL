## Context

This is the second of four changes (01–04) that make my data travel between my two devices. 01 (`log-only-my-own-edits`, archived) made each device's log hold only what was done on that device. This change adds only what 03 (export) and 04 (import) need from the schema. Nothing is exported or imported here.

**The ranking** is `TopAnimeSelections`: one row per anime, holding `AnimeId`, `Position` and `SelectedAt`.
- `TopAnimeSelectionRepository.ReplaceOrderAsync` is the only writer. Its three callers are in `AnimeRankingService`: the editor's tier save, a series-page move, and the score placement that runs on every score save.
- Each call deletes every row and re-adds them all with one `now`, so `SelectedAt` holds a single value repeated 455 times (2026-09-09 07:59:51 UTC).
- Nothing reads `SelectedAt`. `SeasonRepository` joins the table for `Position` only.

**The activity log** is `ActivityLogs`, keyed by a `long` identity `Id` that Postgres assigns separately on each database.
- The feed orders by `Timestamp` desc, then `Id` desc.
- One save stamps every row it writes with a single `now`, and 94 timestamps on this device are shared by more than one row. For those rows, the order they were stored in is what `ActivityFeedComposer.FindCompletionScoreMerges` reads to fold a completion and its score into one row.
- Seven sites in four files build log rows. `EntryActivityRecorder` is only one of them.

**Constraints:**
- The backend runs `db.Database.Migrate()` at startup.
- The tests use the EF in-memory provider. It enforces neither unique indexes nor SQL. There is no Postgres test harness.
- The local SDK is 9.0 and the backend targets .NET 10, so building and `dotnet ef` run in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy of the source under `/private/tmp`.

## Goals / Non-Goals

**Goals:**
- One ranking last-modified time, written in the same save as every write of the order, and backfilled from the current `SelectedAt`.
- A method that replaces the stored order exactly and records a time it is given.
- A GUID on every log row: backfilled for existing rows, present on every new row whatever code builds it, and unique.
- No visible change: the same ranking, the same feed and the same merged rows.

**Non-Goals:**
- Export, import, or any endpoint that exposes the new values.
- A reader of the ranking time. That arrives with 03.
- Any change to `ReplaceOrderAsync`'s merge, or to the ranking's callers.
- Moving the log's primary key to the GUID.
- Unifying the log writers behind `EntryActivityRecorder`. 01 made that a non-goal too.

## Decisions

### D1. One time for the ranking, on a singleton row

`Models/RankingState.cs` has the same shape as `AiringRefreshState`: an `int Id` and a `DateTimeOffset? ModifiedAt`. It gets a `RankingStates` `DbSet`. There is one row ever. The row, or its absence, is the whole state: absent, or null, means never arranged on this database.

The time lives here rather than per position for two reasons:
- Every write rewrites every position, so a per-position time can only ever be one value copied N times.
- An emptied ranking has no positions left to carry a time, yet 04 still needs to know when it was emptied.

*Alternatives:*
- Keep `SelectedAt` and read `max(SelectedAt)`. Rejected: it loses the empty case, and it keeps 455 copies of one value.
- Put the time on some existing table. Rejected: no table owns the ranking as a whole.

### D2. The repository writes the time, in the same save, on every write

Both public write methods end in one private method, `WriteOrderAsync(existingRows, finalOrder, modifiedAt)`. That method:
1. removes the existing rows
2. adds the final order with positions `0..n-1`
3. gets or creates the `RankingState` row and sets `ModifiedAt`
4. calls `SaveChangesAsync` once

Putting the time write in the one place every order write passes through is what makes "every write sets the time" structural rather than a convention each caller must follow. It also keeps the ranking's callers untouched.

The get-or-create mirrors `EpisodeScheduleRefreshService.GetOrCreateStateAsync`. The migration does not seed a row on an empty ranking, so the first write on a fresh database creates it.

`ReplaceOrderAsync` passes `DateTimeOffset.UtcNow`. It compares nothing against the previous order and does not ask which caller it is serving. So a score placement stamps the ranking exactly as a hand reorder does.

The delete and re-add stay inside one `SaveChanges` call, as today, rather than using `ExecuteDelete`. `ExecuteDelete` runs outside the save's transaction, and the in-memory provider the tests use does not support it.

### D3. `ReplaceAllAsync(animeIds, modifiedAt)` records the time it is given

It is added to `ITopAnimeSelectionRepository`, with a doc comment that sets it against `ReplaceOrderAsync`:
- **Validation:** unknown ids are rejected, reusing the existing check and `UnknownAnimeIdsException` (factored into a shared helper). The check runs before any row is touched, so a rejected call leaves the order and the time unchanged.
- **Duplicates:** `Distinct()` keeps each id's first occurrence, the same rule as `ReplaceOrderAsync`.
- **Write:** the result goes to `WriteOrderAsync` with the caller's `modifiedAt`.

The time is a parameter because adopting another device's ranking is not arranging one. Suppose the import stamped "now":
1. A exports its ranking, then reorders.
2. B imports A's file.
3. B's copy now looks newer than A's later reorder.
4. A imports B's export.
5. A's reorder is overwritten by its own older order.

Carrying the adopted time avoids this. It is the same rule a series rebuild follows when it moves a choice with its timestamp.

`ReplaceAllAsync` does not compare the given time with the stored one. Whether the incoming ranking is newer is 04's decision, and a repository that silently ignored older input would hide that decision.

*Alternative:* `ReplaceAllAsync(animeIds)`, stamping now. Rejected for the lost-edit case above.

### D4. The interface grows, so the test doubles grow

`ITopAnimeSelectionRepository` gains the method. Every hand-written test double implements the interface directly, and each gets a `ReplaceAllAsync` stub that throws `NotImplementedException`, matching the existing stubs. None of those tests exercise ranking writes.

*Alternative:* a second interface for the replace method. Rejected: it is the same table and the same repository, and 04 will inject the one it already knows.

### D5. `EventId` is assigned on the entity, not by any writer

`ActivityLog` gains `public Guid EventId { get; set; } = Guid.NewGuid();`.
- It is mapped to a `uuid` column with a unique index (`IX_ActivityLogs_EventId`), configured in `AnimeTrackerDbContext`.
- It is not a key and not value-generated, so EF always sends the value the object holds.
- 04's import sets the property from the file, and the stored row keeps it.

The brief asked for the GUID to be generated in `EntryActivityRecorder`, so that there is a single writer. The recorder isn't the only writer, though. `UserAnimeEntryEditService` (×3), `AiringWatchStatusService` (×2) and `HeldChangeService` (×1) build rows inline. The property initializer is the one place every current and future `new ActivityLog` passes through. That meets the brief's intent, one generator in application code, without touching any writer.

The final schema has **no database default**:
- EF never omits the column, so a default would never fire for the app.
- An EF-scaffolded non-null `Guid` column would ship with a `'00000000-…'` default, which would turn a forgotten value into a unique-index collision on the second row.

*Alternatives:*
- Route every writer through `EntryActivityRecorder`. Rejected: it reverses 01's non-goal, it touches five call sites, and a future inline `new ActivityLog` would still get `Guid.Empty`. That would pass the in-memory tests and fail in production.
- A `gen_random_uuid()` database default. Rejected: the value would exist only after the save, the app would have two generators, and the import would have to override it anyway.
- Version-7 GUIDs. Rejected: nothing orders by this value, and the backfilled rows are version 4 regardless.

### D6. `Id` stays the primary key and the same-timestamp tiebreak

`ActivityLogRepository`, `ActivityFeedComposer` (`Dictionary<long, CompletionScoreMerge>`) and `ProfileService` are unchanged.

The model's comments now say:
- `Id` is local to this database and never exported.
- `EventId` is the identity that travels.

A comment at the `ThenByDescending(l => l.Id)` ordering records that this is a same-timestamp tiebreak by storage order, which 04 must preserve when it inserts (see Open Questions).

### D7. One migration, hand-ordered, with no default left behind

`MakeRankingAndLogPortable` is scaffolded with `dotnet ef migrations add` in the SDK 10 container, then edited by hand. EF would otherwise order the operations so that `SelectedAt` is dropped before the backfill can read it, and would give `EventId` a zero-GUID default.

`Up`, in order:
1. `CreateTable("RankingStates")`
2. `INSERT INTO "RankingStates" ("ModifiedAt") SELECT max("SelectedAt") FROM "TopAnimeSelections" HAVING count(*) > 0;`
   - `HAVING` makes an empty ranking produce no row, so an absent row keeps meaning "never arranged". A bare `max()` would insert a row holding null.
   - Expected on this device: one row, 2026-09-09 07:59:51.127889+00.
3. `DropColumn("SelectedAt", "TopAnimeSelections")`
4. `AddColumn<Guid>("EventId", "ActivityLogs", type: "uuid", nullable: true)`
5. `UPDATE "ActivityLogs" SET "EventId" = gen_random_uuid();` Built into Postgres since 13; the server runs 17. Expected: 913 rows.
6. `AlterColumn` to `nullable: false`, with no default.
7. `CreateIndex("IX_ActivityLogs_EventId", unique: true)`

`Down` mirrors it:
1. drop the index and `EventId`
2. re-add `SelectedAt` as nullable
3. set every position to `coalesce((SELECT "ModifiedAt" FROM "RankingStates" LIMIT 1), now())`
4. make it non-null
5. drop `RankingStates`

Every position ends up holding one shared value, which is the state it was in before `Up`. The `now()` fallback only covers a database whose ranking was never arranged but has positions. That cannot arise from `Up`, but `Down` must not fail on it. The GUIDs are discarded, which is harmless until 03 has exported any.

The generated model snapshot is checked for the absence of `SelectedAt`, for `RankingState`, and for `EventId` as a required `uuid` with a unique index and no default.

### D8. Rehearse the SQL on a copy, since no test can run it

The in-memory tests cannot exercise the unique index, `gen_random_uuid()` or `HAVING`. So before deploying:
1. Generate the migration's SQL with `dotnet ef migrations script RemoveActivityLogSource MakeRankingAndLogPortable`, and the reverse script.
2. Restore the pre-deploy dump into a throwaway `postgres:17-alpine` container.
3. Run `Up` there and check the counts and values.
4. Run `Down` and check the positions' restored time.

This catches ordering and SQL mistakes before the live database sees them.

## Risks / Trade-offs

- **[Every score save bumps the ranking time]** A device where I only scored something has a "newer" ranking than an earlier, careful reorder on the other device, and 04's rule that the newer side wins wholesale drops the reorder. → Accepted by the brief, which asks for no distinction between hand reorders and placements. Import the other device's file before editing on this one.
- **[A row inserted outside `new ActivityLog`, e.g. raw SQL, has no GUID]** → The column is `NOT NULL` with no default, so such an insert fails loudly instead of storing a colliding value.
- **[In-memory tests can't catch index or SQL mistakes]** → D8's rehearsal on a restored copy, then the post-deploy checks below.
- **[`Down` discards every GUID]** → Harmless until 03 ships. Once a device has exported, rolling back this migration would orphan GUIDs held in export files. Roll back only before 03 is deployed.
- **[The table is rewritten on `UPDATE`]** → 913 rows. Negligible.

## Migration Plan

1. Dump the running database to a location outside the repository, and confirm `pg_restore --list` reads it.
2. Rehearse on a restored copy (D8):
   - after `Up`: `RankingStates` holds one row equal to the dump's `max("SelectedAt")`; `TopAnimeSelections` count and positions are unchanged; `ActivityLogs` count, `count(DISTINCT "EventId")` and `max("Id")` are unchanged and not null
   - after `Down`: every position's `SelectedAt` equals that time
3. `docker compose up -d --build backend`. The migration applies on start.
4. Verify on the live database:
   - the same three checks as the rehearsal
   - the profile feed and the full history render the same rows, in the same order, with the same "Completed — Score" merges
   - a new edit produces a row with a fresh `EventId`
   - a ranking reorder advances `RankingStates."ModifiedAt"`
5. Repeat 1–4 on the second device before 03 lands. Its expected values are its own pre-deploy counts and its own `max("SelectedAt")`.

**Rollback:** restore the dump, check out the previous commit, and rebuild. `Down` alone is also lossless for the ranking (D7).

## Open Questions

- **For 03/04, not this change: same-timestamp order must survive the move.** `Id` never travels, but the merged "Completed — Score" row depends on the score row having been stored after the completion. The export should write rows in `Timestamp, Id` order. The import should insert the rows new to this device in file order, so the fresh local numbers reproduce each save's internal order.
- **For 04, carried over from 01:** both devices run `AiringWatchStatusService` independently, so the same automatic completion can be logged once on each device. The two rows carry different GUIDs, so a GUID union keeps both. The GUID removes transport duplicates, not a second recording of the same event.
