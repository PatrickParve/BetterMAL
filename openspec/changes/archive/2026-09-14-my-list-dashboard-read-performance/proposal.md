## Why

This change fixes two items from `docs/ISSUE_TRIAGE.md`, Phase 5 ("My List / Dashboard read performance") of its fix order.

- **N4.** The shared "all entries" read loads every `AnimeMetadata` column for every entry. My List, Home, Profile and Recap use 11 of them. The rest, free-text `Synopsis` and `Background` included, are read from the database and thrown away on every visit.
- **R5.** When a page read has to auto-complete or re-open entries, it re-reads and saves each one separately, one after another, before the response goes out. Usually 0–2 entries qualify, so nobody notices. On a day when many shows finish or start airing, one My List or Home load makes two round trips per entry, and more with a remote database. That breaks the rule `library-views` and `main-dashboard` already state: a read's round trips don't grow with my list.

Neither is the lag you feel on My List. That comes from the frontend rendering every row, which was decided on 2026-09-13 to stay as it is. Both are server-side costs that grow with the list, and they're cheap to remove now, while nothing else touches these files.

**Verified against the code** (2026-09-14, at `72696f7`). Paths without a prefix are under `backend/AnimeTracker.Api/`.

- **N4: no projection.**
  - `Data/Repositories/UserAnimeEntryRepository.cs:13-16` is `AsNoTracking().Include(e => e.Anime).ToListAsync(ct)`. That loads all of `Models/AnimeMetadata.cs`, including `PictureUrls` (`:30`), `Genres`, `Synopsis` and `Background` (`:75-77`). `RelatedAnime` is its own table and isn't `Include`d, so it isn't loaded today either.
  - There are 17 calls in 9 files (the triage doc says 18). Seven of them are inside the four services in scope, and Profile accounts for four: `Services/Library/MyListService.cs:16`, `Services/Dashboard/MainDashboardService.cs:18`, `Services/Profile/ProfileService.cs:46,93,101,168` and `Services/Recap/RecapService.cs:16`.
  - **The field set, worked out again from the code.** The four services, and every helper they pass entries or anime to, read exactly 11 `AnimeMetadata` fields: `Id`, `Title`, `EnglishTitle`, `PictureUrl`, `MediaType`, `TotalEpisodes`, `AiringStatus`, `MalScore`, `PopularityRank`, `AiredFrom` and `AverageEpisodeDurationSeconds`. The helpers are:
    - `AnimeRankingSnapshot.Build`, through `RankBandResolver.Resolve` and `AnimeRankingKey.Compare`
    - `AiringWatchStatusService.SettleAsync` and `AiredEpisodeGate`
    - `WatchMath` (the only reader of `AverageEpisodeDurationSeconds`)
    - `RecapEntrySelector`, `RecapStatsBuilder`, `RecapRankingBuilder`, `ScoreDivergence` and `TopAnimeMediaTypeScope`
    - `IEpisodeScheduleService`'s bulk reads, which read only `Id` (`Services/Airing/EpisodeScheduleService.cs:26-56`)

    Nothing on these paths reads `Anime.UserEntry`.
  - The detail page reads through `AnimeMetadataRepository.GetByIdAsync`, not `GetAllAsync`, so this change can't affect it.
- **R5: one read and one save per entry moved.**
  - `Services/Entries/AiringWatchStatusService.cs:20-51` walks the caller's list. The re-open condition is `:30-32` and the complete condition is `:44-47`. Each entry that qualifies waits on `ReopenOneAsync` (tracked read `:56`, save `:77`) or `CompleteOneAsync` (`:98`, `:119`) before the loop moves on.
  - It runs on `MyListService.cs:31`, `MainDashboardService.cs:33`, `Services/Detail/AnimeDetailService.cs:79` (one entry) and `Services/Airing/EpisodeScheduleRefreshBackgroundService.cs:117` (the whole list, from that job's own read at `:111`).
  - `UserAnimeEntry` uses Postgres's `xmin` as a concurrency token (`Data/AnimeTrackerDbContext.cs:48-58`). The two catch blocks (`:79-90`, `:121-127`) handle a lost race for one entry at a time.
  - Specs: `library-views/spec.md:955-966` and `main-dashboard/spec.md:562-580` bound the read's database reads, and the per-entry reads break that bound whenever more than one entry moves. Commit `cfb5e1d` fixed only the aired-count reads.

## What Changes

- **N4: a lean read for the four whole-list pages.**
  - `IUserAnimeEntryRepository` gains `GetAllForListViewAsync()`. It returns the same `UserAnimeEntry` objects as `GetAllAsync()`, with every entry column filled, but `Anime` holds only the 11 fields above. The other `AnimeMetadata` properties keep their defaults.
  - Only the four pages move: `MyListService`, `MainDashboardService`, `ProfileService` and `RecapService` call it instead of `GetAllAsync()`. That's seven calls, because `ProfileService` reads entries in four methods (`GetProfileAsync`, `GetTopAnimeSectionAsync`, `GetRewatchedSectionAsync` and `ScheduleMissingSeriesBuildsAsync`).
  - `GetAllAsync()` doesn't change, and neither do its other 10 calls: `AiringScheduleService`, `AnimeRankingService` (×4), `RecapAvailabilityService`, `EpisodeScheduleRefreshService` (×3) and `SeriesBulkBuildBackgroundService`.
  - No page's response changes. A new test runs each of the four services once over the lean read and once over the full read, and requires identical output. Its fixture sets every `AnimeMetadata` property, so if one of these services later reads a column the lean read leaves out, the test fails.
- **R5: settling costs one read and one save, however many entries move.**
  - `SettleAsync` still picks the entries to move from the caller's list with no database call. If it picks none, it makes no query and no save.
  - It loads the picked entries in one tracked query and re-checks each one's status under that read. For each entry that still qualifies, it sets the status, finish date, `PendingSync` and activity-log row. Then it saves once.
  - If the save hits a concurrency conflict, only the conflicting entries are dropped: their log rows are removed, they're reloaded, and the rest are saved again. Each conflict costs one more save, however long the list.
  - `ScheduleSync` runs after the save, once for each entry that actually moved.
  - Unchanged: the eligibility rules, their comments, `AiredEpisodeGate`, and what callers see, which is each settled entry's `Status` written back onto the caller's own objects.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `library-views`:
  - **Modified:** "The my-list read's database round trips do not grow with my list". The bound now covers the settling that runs on the read, reads and writes both. However many entries it moves, it costs one read and one save, and a read with nothing to move costs neither. Adds scenarios for many entries moving at once and for nothing to move.
- `main-dashboard`:
  - **Modified:** "The dashboard read's database round trips do not grow with my list". The same change and the same scenarios.
- `list-editing`:
  - **Added:** "Automatic status settling treats each entry on its own". When one read moves several entries, each gets its own log row and its own sync. A change that reaches one entry first leaves that entry as the change left it, and doesn't stop the others. Re-opens and completions can happen in the same read.
  - "A caught-up entry is completed once its full run is known" and "A completed entry re-opens when a new episode airs" don't change.
- **N4 changes no requirement.** It's internal. The existing "The rows are unchanged" (`library-views`) and "The payload is unchanged" (`main-dashboard`) scenarios already state what it has to preserve. `profile-stats` and `list-recaps` have no round-trip requirement, and this change doesn't need one.

## Impact

- **Backend** (under `backend/AnimeTracker.Api/`):
  - `Data/Repositories/IUserAnimeEntryRepository.cs` and `UserAnimeEntryRepository.cs`: the new read
  - `Services/Library/MyListService.cs`, `Services/Dashboard/MainDashboardService.cs`, `Services/Profile/ProfileService.cs` and `Services/Recap/RecapService.cs`: switched to it
  - `Services/Entries/AiringWatchStatusService.cs`: the batched settle
- **Backend tests** (under `backend/AnimeTracker.Api.Tests/`):
  - **Extended:** `Data/Repositories/UserAnimeEntryRepositoryTests.cs`, `Services/Entries/AiringWatchStatusServiceTests.cs`, `Services/Library/MyListServiceReopenTests.cs` and `Services/Dashboard/MainDashboardServiceReopenTests.cs`
  - **New:** `Services/Library/ListViewReadParityTests.cs`
  - The ~20 hand-written `IUserAnimeEntryRepository` fakes don't change. The new method has a default implementation, the same approach `IEpisodeScheduleService` already uses (design D2).
- **API, database, frontend:** no change and no migration.
- **Docs:**
  - `CODE_GUIDE.md` (local, gitignored): the `Data/Repositories/` note, plus a short note on settling under `Services/Entries/`
  - `docs/ISSUE_TRIAGE.md` and `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md` (local, gitignored): N4 and R5 move to resolved
- **Commits:** two, back to back.
  1. N4: the lean read, the four services switched to it, and its tests.
  2. R5: the batched settle and its tests, then archiving this change, which writes the three capabilities' spec deltas.
- **Follow-ups this change doesn't take on:**
  - `AnimeRankingService` and `RecapAvailabilityService` read only fields inside the 11, so they could use the lean read. Profile's Top series request still makes one full read, through `rankingService.GetSnapshotAsync` (`ProfileService.cs:117`).
  - `EpisodeScheduleRefreshBackgroundService.cs:111` does its own full `Include` read. It's a background job, not a page.
  - `IAiringWatchStatusService`'s doc comment says settling writes `CompletedAt` back onto the caller's objects. The code doesn't do that (design Open Questions).
- **Out of scope:**
  - the auto-complete and re-open eligibility rules
  - `AiringScheduleService` and every other `GetAllAsync()` caller not named above
  - the frontend's unbounded My List render
  - `EpisodeScheduleService.EpisodesAiredAsOfAsync` and the ranking snapshot logic, which this change reads as they are
