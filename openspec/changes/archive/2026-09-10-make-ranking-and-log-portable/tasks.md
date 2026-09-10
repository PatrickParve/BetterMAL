## 1. The ranking's last-modified time

- [x] 1.1 Add `Models/RankingState.cs` in the `AiringRefreshState` shape: `int Id` and `DateTimeOffset? ModifiedAt`. Give it a doc comment: a singleton bookkeeping row for the ranking; an absent row or a null time means the ranking has never been arranged on this database. Add `DbSet<RankingState> RankingStates` to `AnimeTrackerDbContext` (design D1)
- [x] 1.2 `Models/TopAnimeSelection.cs`: remove `SelectedAt`, and add to the class comment that the ranking's last-modified time lives on `RankingState`, not per position
- [x] 1.3 `Data/Repositories/TopAnimeSelectionRepository.cs` (D2):
  - Factor the known-id check into a private helper that throws `UnknownAnimeIdsException`.
  - Add a private `WriteOrderAsync(existingRows, finalOrder, modifiedAt)`. It removes the existing rows, adds the final order at positions `0..n-1`, gets or creates the `RankingState` row, sets its `ModifiedAt`, and calls `SaveChangesAsync` once.
  - Make `ReplaceOrderAsync` end in `WriteOrderAsync` with `DateTimeOffset.UtcNow`. Leave its merge otherwise untouched.
- [x] 1.4 Add `ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default)` to `ITopAnimeSelectionRepository` (D3):
  - Its doc comment contrasts it with `ReplaceOrderAsync` and says why it records the given time rather than now.
  - Implement it: validate before touching any row; `Distinct()` keeps each id's first occurrence; write through `WriteOrderAsync` with the caller's time; no comparison against the stored time.
- [x] 1.5 Add a `ReplaceAllAsync` stub that throws `NotImplementedException` to each of the 12 test doubles (D4):
  - `Controllers/RecapControllerTests`
  - `Services/Recap/RecapServiceTests`
  - `Services/Profile/ProfileService{ActivityFeed, EpisodeProgress, FavouriteSeasonsAndYears, OpinionDivergence, RewatchedOrdering, RewatchedSeries, Stats, TopAnimeRanking, TopSeriesBackfill, TopSeries}Tests`
- [x] 1.6 Drop `SelectedAt =` from the seeds in `SeasonRepositoryTests`, `TopAnimeSelectionRepositoryTests` and `AnimeRankingKeyOrderingParityTests`

## 2. The activity log's portable identifier

- [x] 2.1 `Models/ActivityLog.cs` (D5, D6):
  - Add `public Guid EventId { get; set; } = Guid.NewGuid();`. Its comment: the identity that travels between devices, assigned when the object is created, and kept when set explicitly.
  - Comment `Id`: it is local to this database, never exported, and only breaks ties between rows that share a timestamp.
- [x] 2.2 `Data/AnimeTrackerDbContext.cs`: add a unique index on `ActivityLog.EventId`, with no default value configured
- [x] 2.3 `Data/Repositories/ActivityLogRepository.cs`: at both `ThenByDescending(l => l.Id)` orderings, comment that `Id` breaks same-timestamp ties by storage order on this database, and that an import must preserve that order (D6)
- [x] 2.4 Confirm no log writer changed: `grep -rn "EventId" backend/AnimeTracker.Api` hits only `Models/ActivityLog.cs`, `AnimeTrackerDbContext.cs` and `Migrations/`, and `git diff --stat` shows no service that builds log rows

## 3. Migration

- [x] 3.1 Scaffold `MakeRankingAndLogPortable` with `dotnet ef migrations add` in the `mcr.microsoft.com/dotnet/sdk:10.0` image (D7):
  - rsync `backend/` to `/private/tmp`, excluding `bin/` and `obj/`
  - install `dotnet-ef` 10.x in the container and run the command there
  - copy the migration, its Designer file and the updated `AnimeTrackerDbContextModelSnapshot.cs` back
- [x] 3.2 Hand-order `Up`:
  1. Create `RankingStates`.
  2. `INSERT INTO "RankingStates" ("ModifiedAt") SELECT max("SelectedAt") FROM "TopAnimeSelections" HAVING count(*) > 0;`, with a comment on why it uses `HAVING` and the expected value on this device.
  3. Drop `SelectedAt`.
  4. Add `EventId` as a nullable `uuid` with no default, replacing any scaffolded zero-GUID `defaultValue`.
  5. `UPDATE "ActivityLogs" SET "EventId" = gen_random_uuid();`, with the expected 913 rows.
  6. Alter the column to non-null, with no default.
  7. Create the unique index `IX_ActivityLogs_EventId`.
- [x] 3.3 Write `Down`:
  1. Drop the index and `EventId`.
  2. Re-add `SelectedAt` as nullable, fill it from `coalesce((SELECT "ModifiedAt" FROM "RankingStates" LIMIT 1), now())`, then make it non-null.
  3. Drop `RankingStates`.
  4. Add a comment that the GUIDs are discarded.
- [x] 3.4 Check the model snapshot:
  - no `SelectedAt`
  - `RankingState` present
  - `EventId` is a required `uuid` with a unique index and no `HasDefaultValue`

## 4. Tests

- [x] 4.1 `Data/Repositories/TopAnimeSelectionRepositoryTests` for `ReplaceOrderAsync`:
  - no `RankingState` row exists before any write
  - after a write, `ModifiedAt` falls between `UtcNow` taken before and after the call, including when the written order equals the stored one
  - a second write advances it
  - the brief's example pins the merge: `[A, B, C]` merged with `[C, A]` gives `[C, B, A]`
- [x] 4.2 `Data/Repositories/TopAnimeSelectionRepositoryTests` for `ReplaceAllAsync`:
  - `[A, B, C]` replaced with `[C, A]` gives `[C, A]`
  - the given past time is stored, not now
  - `[A, B, A]` gives `[A, B]`
  - an empty list leaves no rows and the given time
  - an unknown id throws `UnknownAnimeIdsException` and leaves both order and time unchanged
  - it works when no `RankingState` row existed yet
- [x] 4.3 `Services/Ranking/AnimeRankingServiceTests`: `ApplyTierOrderAsync` and `MoveAdjacentAsync` each advance `RankingState.ModifiedAt`, and a `MoveAdjacentAsync` that returns early (different scores) leaves it untouched
- [x] 4.4 `Services/Entries/UserAnimeEntryEditServiceRankingPlacementTests`: saving a new score advances `RankingState.ModifiedAt`, and re-saving the same score does not
- [x] 4.5 `Data/Repositories/ActivityLogRepositoryTests`: rows saved without setting `EventId` read back with distinct, non-empty values, and a row saved with an explicit `EventId` reads back with that value
- [x] 4.6 `Services/Entries/EntryActivityRecorderTests`: every row `Diff` and `Added` produce carries a non-empty `EventId`, and the rows of one `Diff` are pairwise distinct

## 5. Verify and deploy

- [x] 5.1 Build the backend and run the full test suite in the `sdk:10.0` container
- [x] 5.2 Take a dump before rebuilding: `docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > <outside the repo>/bettermal-before-make-ranking-and-log-portable.dump`, and confirm `pg_restore --list` reads it, including `TopAnimeSelections` and `ActivityLogs`
- [x] 5.3 Rehearse on a copy (D8):
  1. Generate `dotnet ef migrations script RemoveActivityLogSource MakeRankingAndLogPortable`, and the reverse script, in the SDK container.
  2. Restore the dump into a throwaway `postgres:17-alpine` container and apply `Up` with `psql`.
  3. Confirm `RankingStates` holds one row equal to the dump's `max("SelectedAt")`.
  4. Confirm the `TopAnimeSelections` count and positions are unchanged.
  5. Confirm the `ActivityLogs` count, `count(DISTINCT "EventId")` and `max("Id")` are all unchanged, with no null `EventId`.
  6. Apply `Down` and confirm every `SelectedAt` equals that time.
  7. Remove the container.
- [x] 5.4 `docker compose up -d --build backend`, then on the live database:
  - repeat the three `Up` checks from 5.3
  - confirm Latest updates and the full edit history show the same rows, order and "Completed — Score" merges as before
  - a new edit writes a row with a fresh `EventId`
  - a ranking reorder advances `RankingStates."ModifiedAt"`
- [x] 5.5 On the second device, before 03 lands: record its `TopAnimeSelections` count, its `max("SelectedAt")` and its `ActivityLogs` count, then repeat 5.2 and 5.4 against those values
