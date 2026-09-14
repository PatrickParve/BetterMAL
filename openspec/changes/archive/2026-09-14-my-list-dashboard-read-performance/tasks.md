Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`. Line numbers are as of `72696f7`, so check them before editing.

Groups 1–3 are N4 and groups 4–6 are R5 (design D9). N4 changes no requirement.

## 1. N4: the lean read (design D1, D2)

- [x] 1.1 In `Data/Repositories/IUserAnimeEntryRepository.cs`, add `GetAllForListViewAsync(CancellationToken ct = default)` as a default interface method returning `GetAllAsync(ct)`, with the doc comment from design D2. The comment should cover:
  - the 11 fields
  - that the other `AnimeMetadata` properties stay at their defaults
  - never attach these objects
  - the four pages that use it
  - `ListViewReadParityTests`, and extending the projection and that test's fixture together
  - that any implementation backed by a database MUST override it

  Leave `GetAllAsync` and `GetByAnimeIdAsync` as they are.
- [x] 1.2 In `Data/Repositories/UserAnimeEntryRepository.cs`, add the public `GetAllForListViewAsync` exactly as design D1 shows:
  - `AsNoTracking()`, then a member-init `Select` copying all 10 `UserAnimeEntry` scalar properties
  - `Anime = new AnimeMetadata { … }` with exactly the 11 fields
  - `ToListAsync(ct)`

  No `OrderBy`, the same as `GetAllAsync`. Add a one-line comment: `GetAllAsync` stays the full read for every other caller.
- [x] 1.3 Switch the seven calls, changing only the method name:
  - `Services/Library/MyListService.cs:16`
  - `Services/Dashboard/MainDashboardService.cs:18`
  - `Services/Profile/ProfileService.cs:46,93,101,168`
  - `Services/Recap/RecapService.cs:16`

  Change nothing else in those files.
- [x] 1.4 Grep `backend/AnimeTracker.Api` for `GetAllAsync(` on the entry repository. Exactly 10 calls must remain: `AiringScheduleService`, `AnimeRankingService` ×4, `RecapAvailabilityService`, `EpisodeScheduleRefreshService` ×3, `SeriesBulkBuildBackgroundService`.

## 2. N4: tests (design D3, D4)

- [x] 2.1 In `Data/Repositories/UserAnimeEntryRepositoryTests.cs`, add a static helper `FullyPopulatedAnime(int id)`. It returns an `AnimeMetadata` with every public writable property set to a distinctive non-default value (`Synopsis = $"SYNOPSIS-{id}"`, `Rank = 4200 + id`, `Genres = ["GENRE-…"]`, every `DateTimeOffset?`/`DateOnly?`/`TimeOnly?` set, and so on), except the `RelatedAnime` and `UserEntry` navigations. Make it `internal` so `ListViewReadParityTests` can reuse it, or put it in a small shared test fixture class next to it.
- [x] 2.2 In the same file, add **the lean read carries exactly the list-view fields** (design D4):
  - Seed three entries. One uses `FullyPopulatedAnime`, and every entry has all 10 of its own fields non-default.
  - Clear the tracker, then call both `GetAllForListViewAsync` and `GetAllAsync`.
  - The `AnimeId` sets are equal.
  - For each entry, the 10 entry fields and the 11 anime fields are equal.
  - On the lean result, every other `AnimeMetadata` property equals its default, found by reflection over the same property list 2.1 uses, `RelatedAnime` is empty and `UserEntry` is null.
  - `db.ChangeTracker.Entries()` is still empty after both reads.
- [x] 2.3 Add `Services/Library/ListViewReadParityTests.cs` (design D3), with:
  - a `SeedAsync(AnimeTrackerDbContext db, DateTimeOffset now)` fixture
  - a `FullReadRepository(UserAnimeEntryRepository inner) : IUserAnimeEntryRepository` that delegates everything and sends `GetAllForListViewAsync` to `inner.GetAllAsync`
  - a `RunBothAsync` helper that seeds two fresh InMemory databases, builds the service under test over each one's repository, and returns both results together with each store's entries (`AnimeId`, `Status`, `CompletedAt`, `PendingSync`), activity-log rows (`AnimeId`, `ChangeType`, `ChangeDetail`) and recorded sync and series-build ids

  Use the real `TopAnimeSelectionRepository`, `ActivityLogRepository`, `AiringWatchStatusService`, `SeriesRankingLookup` and `AnimeRankingService`. Use private fakes for `IEpisodeScheduleService`, `IBroadcastLocalTimeConverter`, `IEntrySyncScheduler` and `ISeriesBuildTrigger`. Copy the existing fakes' shape from `MainDashboardServiceReopenTests` and `ProfileServiceTopSeriesBackfillTests`.
- [x] 2.4 Build the fixture so it reaches every branch design D3 lists:
  - a re-open candidate and a complete candidate
  - hand-ordered, `music` short-form and dropped ranked entries, each with a score and a stored `TopAnimeSelection` position for the hand-ordered one
  - plan-to-watch and `not_yet_aired` unranked entries
  - a `movie` with a duration, a `tv` with a null duration, and a rewatched entry
  - an anime aired in the current season, and air dates across at least three years
  - `MalScore` on several scored entries, enough for `ScoreDivergence.TryCompute` to return a context
  - a `CompletedAt` and an episode-progress `ActivityLog` row inside the recap periods
  - a currently-airing entry with a null total

  At least one anime uses `FullyPopulatedAnime`.
- [x] 2.5 Add **the fixture sets every column**: for every public writable `AnimeMetadata` property except `RelatedAnime` and `UserEntry`, at least one seeded anime holds a non-default value (not null, not zero, not `default`, not an empty list). The failure message names the property and says to extend the fixture.
- [x] 2.6 Add one parity `[Fact]` per call, each asserting equal `JsonSerializer.Serialize` output and equal store snapshots from 2.3:
  - `GetMyListAsync`
  - `GetDashboardAsync`
  - `GetProfileAsync`
  - `GetTopAnimeSectionAsync("all")` and `("movie")`
  - `GetRewatchedSectionAsync("all")`
  - `GetTopSeriesSectionAsync`, also comparing the enqueued series-build ids
  - `GetRecapAsync` for `RecapPeriod.MultiYear` with `aired`, a yearly `watched` period, and a season

  In the My List and dashboard facts, also assert that the fixture's re-open and complete candidates did move, so the settle branches are known to have run.
- [x] 2.7 Put a class comment on `ListViewReadParityTests`:
  - it guards `GetAllForListViewAsync`'s field list for the four pages
  - a failure means a page started reading a column the projection leaves out
  - the fix is to add the field to the projection and to D2's doc comment, not to loosen the test
  - a fifth service moved onto the lean read belongs here too
- [x] 2.8 Confirm the ~20 `FakeUserAnimeEntryRepository` classes compile unchanged, and that `MyListServiceRankTests` and `MyListServiceReopenTests` (both on the real repository) pass unchanged.

## 3. N4: build, test and docs

- [x] 3.1 Copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`), then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass.
- [x] 3.2 In `CODE_GUIDE.md` (local, gitignored), `Data/Repositories/`, change `UserAnimeEntryRepository (by-anime, all-with-anime, sync status)` to add the lean read:
  - `GetAllForListViewAsync` is a projection of every entry column plus the 11 anime fields My List, Home, Profile and Recap read
  - those four pages use it, and every other caller keeps `GetAllAsync`
  - `ListViewReadParityTests` guards the field list
- [x] 3.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern Phase 4 used:
  - replace the N4 entry with a short blockquote pointing to the resolved file
  - update the Summary table's "Still present" count and the "Resolved 2026-09-14 (code fixes)" row
  - mark item 9 of Phase 5 done
  - update the Memory note appendix row "2. Unprojected entries query"
- [x] 3.4 *(Optional, real Postgres.)* **Before** running `docker compose build backend`, while the old image is still up, save `GET /api/my-list`, `/api/dashboard`, `/api/profile`, `/api/profile/top-anime?mediaType=all`, `/api/profile/rewatched?mediaType=all` and `/api/recap?mode=multiYear&from=2000&to=2026&filter=aired` from `http://localhost:5050` to files under the scratchpad. Rebuild and restart the backend, fetch the same URLs again, and diff them.
  - Only the dashboard countdowns, or an episode that aired in between, may differ.
  - Don't edit dev data to make entries qualify for settling. That would push real changes to MyAnimeList.

## 4. R5: the batched settle (design D5, D6, D7)

- [x] 4.1 In `Services/Entries/AiringWatchStatusService.cs`, add a private `enum Direction { Reopen, Complete }`. Rewrite `SettleAsync`'s loop into pass 1 from design D5:
  - walk `entries` in order and add `(entry, Direction.Reopen)` or `(entry, Direction.Complete)` to `picked`
  - move the two conditions at `:30-32` and `:44-47`, and their comments, over **verbatim**
  - return straight away when `picked` is empty, before any database access
- [x] 4.2 Replace `ReopenOneAsync` and `CompleteOneAsync` with private non-async methods:
  - `ActivityLog? TryReopen(UserAnimeEntry tracked)`: the status re-check, the mutation, `PendingSync = true`, and `db.ActivityLogs.Add`
  - `ActivityLog? TryComplete(UserAnimeEntry tracked, DateOnly today)`: the same, with `CompletedAt ??= today` and its "never overwrites" comment

  Each keeps its existing re-check comment and returns the queued log row, or null when the re-check fails.
- [x] 4.3 After pass 1, load the picked ids (distinct) in one tracked query: `db.UserAnimeEntries.Where(e => ids.Contains(e.AnimeId)).ToDictionaryAsync(e => e.AnimeId, ct)`. Then run pass 2 in caller order:
  - skip a missing id
  - call `TryReopen` or `TryComplete` by direction
  - add each non-null log to `applied[animeId] = (tracked, log, direction)`
- [x] 4.4 Add `SaveDroppingConflictsAsync` as design D6 shows. The loop runs while `applied` is non-empty. It catches only `DbUpdateConcurrencyException`, and rethrows when `ex.Entries` names no `UserAnimeEntry` or names one outside `applied`. For each conflicting entry it:
  - removes it from `applied`
  - calls `db.ActivityLogs.Remove(log)`
  - calls `await db.Entry(entry).ReloadAsync(ct)`
  - logs today's message for its direction ("was reopened by a concurrent read" or "was settled by a concurrent read")

  The catch comment must say:
  - the failed save rolled back every entry in it, so the ones that didn't conflict need saving again
  - EF names only the first conflicting command, so a second conflict surfaces on the next attempt
  - the loop ends because every attempt removes an entry or rethrows
  - there's no re-apply after a reload, the same as before, since the next read settles it
  - it relies on `SaveChangesAsync`'s own transaction, so it must not run inside a caller's explicit transaction
- [x] 4.5 After the save, call `syncScheduler.ScheduleSync(animeId)` for each key still in `applied`. Then, for each picked caller entry whose id was loaded, set `callerEntry.Status = trackedById[id].Status` (design D5, D7).
- [x] 4.6 Update the doc comments:
  - the `AiringWatchStatusService` class, or the top of `SettleAsync`: one read and one save for any number of entries, with nothing touched when nothing qualifies
  - `IAiringWatchStatusService.SettleAsync`'s summary: the same, plus "a conflicting concurrent change drops only that entry"

  Don't change the `CompletedAt` sentence yet (6.2).

## 5. R5: tests (design D8)

- [x] 5.1 In `Services/Entries/AiringWatchStatusServiceTests.cs`, add the conflict-simulation helper first, and check it compiles and throws as intended. It's a `ScriptedConflictInterceptor : SaveChangesInterceptor`, built from a list of attempts, each naming an anime id or passing. On a scripted attempt, `SavingChangesAsync`:
  - writes the competing change (a status value and a `PendingSync` value the test chooses) through a second `AnimeTrackerDbContext` on the same InMemory database name
  - throws `new DbUpdateConcurrencyException("simulated", [entry.GetInfrastructure()])` for the `UserAnimeEntry` with that id, before anything is written

  Wrap `GetInfrastructure()` in `#pragma warning disable EF1001`, with a comment on why: InMemory neither bumps `xmin` nor rolls back, so a real conflict there would partly apply the batch. If that doesn't compile against EF Core 10, find another public way to get the entry's `IUpdateEntry`, and record it in the same comment.
- [x] 5.2 Add the counting helpers:
  - a `CreateDb(string? name = null, params IInterceptor[] interceptors)` overload
  - a `SaveLog` that subscribes to `db.SavingChanges` and `db.SavedChanges` and to `db.ChangeTracker.Tracked` (only `FromQuery`), recording one ordered event list
  - an extended `RecordingEntrySyncScheduler` that also records `SaveLog`'s completed-save count at each `ScheduleSync` call

  Clear the tracker after seeding in the new tests.
- [x] 5.3 Add **several completions**, **several re-opens** and **a mix** (design D8 cases 1–3). Each asserts:
  - the stored status, `CompletedAt` (completions), `PendingSync`, and exactly one log row of the right `ChangeType` for each moved entry
  - the caller objects' statuses
  - exactly one `SavingChanges`
  - every picked entry's `Tracked` event comes before that save, and none after it
  - the scheduled ids are exactly the moved entries, each once

  The mix interleaves re-opens and completions in caller order.
- [x] 5.4 Add **nothing qualifies touches no database** (case 4). Build the service over a `db` that's already been disposed. Pass entries none of which qualify: a Rewatching entry at its total, a Completed entry on a `finished_airing` anime, a Watching entry below its total, and a Watching entry with an unknown total. `SettleAsync` completes without throwing, and nothing is scheduled.
- [x] 5.5 Add **moved before the batched read is left alone** (case 5):
  - Three entries qualify to complete.
  - After the untracked read, set one to `Dropped` and save.
  - That entry stays `Dropped`, has no log row and isn't scheduled, and its caller object reads `Dropped`.
  - The other two complete, with one save.
- [x] 5.6 Add **a conflict at the save drops only that entry** (case 6) and **two conflicts across attempts** (case 7), using the 5.1 interceptor.
  - **Case 6:** the scripted competing change leaves B `Watching` with `PendingSync = false`, standing in for a push. B has no log row and isn't scheduled, its stored and caller statuses are `Watching`, and A and C are completed, logged and scheduled. Exactly two `SavingChanges`.
  - **Case 7:** A, then B, conflict. Only C is moved, with exactly three `SavingChanges`.
- [x] 5.7 Add **syncs are scheduled only after the final save** (case 8): every `ScheduleSync` call recorded a completed-save count equal to the final count. Cover both the plain batch and the case 6 setup.
- [x] 5.8 Confirm the existing 11 tests in `AiringWatchStatusServiceTests.cs` pass unchanged, including both idempotency tests and both held-entry tests.
- [x] 5.9 In `Services/Library/MyListServiceReopenTests.cs`, add **a mixed batch settles on one read**. Use the real `UserAnimeEntryRepository`, `TopAnimeSelectionRepository` and `AiringWatchStatusService`, with a fake schedule service that returns a per-anime aired dictionary. Seed two re-open candidates and one complete candidate. The response rows show Watching, Watching and Completed, and the store matches with three log rows.
- [x] 5.10 In `Services/Dashboard/MainDashboardServiceReopenTests.cs`, add **several re-opened shows reach Currently watching on the same read**: three re-open candidates all appear in `CurrentlyWatching` with `Status == Watching`, and the store matches. Widen the fake schedule service to take a dictionary if it only takes one value.

## 6. R5: build, test and docs

- [x] 6.1 Copy `backend/` to `/private/tmp/bm-build/` again, then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`. Everything must pass, including group 2's parity tests, which now run over the batched settle.
- [x] 6.2 Ask the user about design's Open Question: `CompletedAt` isn't written back onto the caller's objects. Do what they choose. The default is (a): correct the sentence in `IAiringWatchStatusService.cs:23-27` so it says only `Status` is written back, and add a note to `docs/ISSUE_TRIAGE.md`. If they pick (b), keep it separate from the R5 work in groups 4–5.
- [x] 6.3 In `CODE_GUIDE.md`, `### Services/Entries/`, add a bullet for `AiringWatchStatusService`. It should say:
  - re-open and complete are settled on My List, the dashboard, the detail page and the schedule refresh job
  - entries are picked in memory, then there's one tracked read and one save, and nothing happens when nothing qualifies
  - a conflict drops only the conflicting entries, and the rest are saved again
  - syncs are scheduled after the save
- [x] 6.4 Run `openspec validate my-list-dashboard-read-performance --strict`, and fix anything it reports.
- [x] 6.5 In `docs/ISSUE_TRIAGE.md`:
  - replace the R5 entry with a short blockquote pointing to the resolved file
  - update the Summary table
  - mark item 10 and Phase 5 of the Fix order done
  - update the Memory note appendix row "3. Settle N+1 writes"
- [x] 6.6 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, under the code-fixes heading, add `## N4.` and `## R5.` sections that name this change.
  - **N4:** the lean read and its 11 fields, the four pages, the parity test as the guard, and the follow-ups (`AnimeRankingService`, `RecapAvailabilityService`, the schedule job's own full read).
  - **R5:** one read and one save, why the conflict handling needs a loop (rollback, and EF naming only the first conflict), the no-re-apply rule carried over, and that the conflict path is tested only with a simulated exception.
