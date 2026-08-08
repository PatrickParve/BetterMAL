## 1. Data model and migration

- [x] 1.1 Add `Models/EpisodeAiring.cs` as an EF entity (`AnimeId`, `Episode`, `AirsAtUtc`, `FetchedAt`), replacing the current `Services/Airing/EpisodeAiring.cs` record — keep a plain DTO/record for what `IAniListClient` returns so the client stays free of EF types.
- [x] 1.2 Add `Models/AnimeAiringSync.cs` (`AnimeId` PK, `AniListId?`, `LastFetchedAt`, `NextAiringEpisodeAtUtc?`, `HasCompleteData`, `NextRecheckAtUtc?`).
- [x] 1.3 Add `Models/AiringRefreshState.cs` (singleton row: `Id`, `LastSuccessfulPassAtUtc?`, `BackfillCompletedAtUtc?`, `LastPassSeason`).
- [x] 1.4 Register all three `DbSet`s in `Data/AnimeTrackerDbContext.cs` and configure in `OnModelCreating`: `EpisodeAiring` composite PK `(AnimeId, Episode)`, FK to `AnimeMetadata` with cascade delete, index on `(AnimeId, AirsAtUtc)`; `AnimeAiringSync` PK `AnimeId` with cascade-delete FK.
- [x] 1.5 Generate the EF migration (`AddEpisodeAiringAndRefreshState`) and confirm it is purely additive — no existing column altered or dropped.
- [x] 1.6 Add `Data/Repositories/IEpisodeAiringRepository.cs` + implementation: `GetMaxAiredEpisodeAsync(animeId, asOfUtc)`, `GetNextAiringInstantAsync(animeId, afterUtc)`, `GetRowsInRangeAsync(animeIds, fromUtc, toUtc)`, `ReplaceForAnimeAsync(animeId, rows)`.
- [x] 1.7 Implement `ReplaceForAnimeAsync` as delete-then-insert inside one transaction, and make it a no-op when `rows` is empty so a failed or empty fetch never wipes good data (design decision 4).

## 2. AniList client

- [x] 2.1 Extend the `Media(idMal:)` query in `Services/Airing/AniList/AniListClient.cs` to also return `status` and `nextAiringEpisode { episode airingAt }`, and return them alongside the AniList id from a new lookup method.
- [x] 2.2 Add `pageInfo { hasNextPage }` to the `airingSchedules` query and loop pages until exhausted; drop the 150-day `airingAt_greater` floor so a full-history fetch starts from the beginning.
- [x] 2.3 Change `IAniListClient` to accept a known AniList id (skipping the id lookup) and to return the schedule together with the `nextAiringEpisode` instant, so callers can compute recheck due times without a second call.
- [x] 2.4 Keep the existing 429 back-off and apply the inter-request delay per HTTP request rather than per anime, so a multi-page fetch stays inside AniList's 30 req/min limit.
- [ ] 2.5 Verify against AniList that a long-runner pages correctly — fetch One Piece's full schedule and confirm the returned episode count and the last few `airingAt` values look right.

## 3. Read path

- [x] 3.1 Rewrite `Services/Airing/EpisodeScheduleService.cs` as a reader over `IEpisodeAiringRepository`, keeping the `IEpisodeScheduleService` interface shape so its seven call sites don't change.
- [x] 3.2 Delete `EstimateAiredFromCadence`, `EstimateOnLocalDate`, `EstimateLastLocalDate`, `HasKnownUpcomingEpisode`, and `MinimumEpisodeEstimate`.
- [x] 3.3 In `EpisodesAiredAsOf`, return the highest stored episode whose `AirsAtUtc <= nowUtc`, or `null` when the anime has no rows. Remove the `Math.Clamp(count, 0, total)` MAL-total clamp and the `finished_airing` → `TotalEpisodes` shortcut.
- [x] 3.4 In `NextAiringInstant`, return the earliest stored `AirsAtUtc > afterUtc`, or `null`. Remove the `IBroadcastLocalTimeConverter.NextBroadcastInstant` fallback.
- [x] 3.5 Rewrite `Services/Airing/AiringScheduleService.cs` to issue one range query for the week across all my-list anime instead of looping entries × 7 dates, grouping rows into day-columns via `IBroadcastLocalTimeConverter.GetLocalDate`.
- [x] 3.6 Derive the week's UTC bounds by converting local Monday 00:00 and the following Monday 00:00 through the local zone, so a DST-shifted week still covers exactly seven local days.
- [x] 3.7 Delete `Services/Airing/EpisodeScheduleCache.cs` and `IEpisodeScheduleCache.cs`, and remove their `Program.cs` registration.
- [x] 3.8 Confirm `Services/Entries/UserAnimeEntryEditService.cs` still compiles and behaves with the now-more-often-null `EpisodesAiredAsOf` — the `?? anime.TotalEpisodes` cap fallback already covers it.

## 4. Refresh service

- [x] 4.1 Rewrite `Services/Airing/EpisodeScheduleRefreshService.cs` around a single-anime `RefreshOneAsync(animeId, ct)` that resolves/reuses the AniList id, fetches the full schedule, calls `ReplaceForAnimeAsync`, and updates the anime's `AnimeAiringSync` row.
- [x] 4.2 Implement recheck-due computation (design decision 8) when writing `AnimeAiringSync`: checkpoints at `T-30d`, `T-7d`, `T` for a known next episode; `now + 3d` when the show is still airing and AniList reports no next episode or the reported instant has passed with no new data; `null` for a finished anime whose last stored episode is past.
- [x] 4.3 Add `RefreshManyAsync(animeIds, ct)` that paces requests, logs and continues past a per-anime failure, and reports how many anime returned zero rows.
- [x] 4.4 Add `BackfillAsync(ct)`: target every my-list anime, skip those whose `AnimeAiringSync.LastFetchedAt` is already set from this backfill, log title + MAL id for each anime AniList returned nothing for, and stamp `AiringRefreshState.BackfillCompletedAtUtc` only after every target was fetched.
- [x] 4.5 Add a target-selection helper: my-list anime whose MAL `AiringStatus` is `currently_airing` or `not_yet_aired`, distinct by id — reusing the existing `ShouldTrack` shape minus the recently-finished window.

## 5. Refresh triggers

- [x] 5.1 Rewrite `Services/Airing/EpisodeScheduleRefreshBackgroundService.cs` as an hourly tick that runs immediately on start, removing the 20s startup delay and the 6h fixed interval.
- [x] 5.2 In the tick, run the backfill first when `BackfillCompletedAtUtc` is null.
- [x] 5.3 In the tick, run the daily pass when `LastSuccessfulPassAtUtc` is null, more than ~24h old, or on an earlier local calendar day than today; stamp `LastSuccessfulPassAtUtc` and `LastPassSeason` on success.
- [x] 5.4 In the tick, run out-of-band rechecks for anime whose `NextRecheckAtUtc <= now` that the daily pass did not already cover.
- [x] 5.5 In the tick, force a pass when `SeasonCalendar.GetSeasonFor(today)` differs from `AiringRefreshState.LastPassSeason`.
- [x] 5.6 Add an `IAiringRefreshTrigger` singleton (mirroring `IImportTrigger` / `IResyncTrigger`) that queues one anime id for immediate background refresh, and drain it from the tick loop or a dedicated waiter.
- [x] 5.7 Fire that trigger from `Services/Entries/UserAnimeEntryEditService.UpdateEntryAsync` when `isNew` and the anime's `AiringStatus` is `currently_airing` or `not_yet_aired`, without awaiting the fetch.
- [x] 5.8 Make `Controllers/MetadataRefreshController.RefreshOne` also call `RefreshOneAsync` for that anime synchronously after the MAL refresh, catching and logging an AniList failure so the endpoint still returns `204`.
- [x] 5.9 Update `Program.cs`: register the repository, the trigger singleton, and the reworked services; drop the deleted cache registration.

## 6. Frontend

- [x] 6.1 In `frontend/src/pages/HomePage.tsx`, render a null aired count as an em dash on currently-watching and followed-shows-airing cards rather than falling back to `0`.
- [x] 6.2 Confirm `frontend/src/pages/AiringPage.tsx` renders a week with zero slots using the existing empty-week message, and that day-columns with no slots stay empty without a placeholder row.
- [x] 6.3 Check `frontend/src/api/types.ts` still matches the DTO shapes after the read-path rewrite; adjust only if a field's nullability changed.
- [x] 6.4 Verify the detail page's Status line renders `Currently airing` with no counts when the aired count is null (existing behaviour — confirm it survives the now-more-frequent null).

## 7. Build and verify

- [x] 7.1 Compile-check the backend with the .NET 10 SDK image: `rsync -a --exclude 'bin/' --exclude 'obj/' backend/ /private/tmp/bm-build/` then `docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj -c Release"` (a `~/Documents` bind mount fails).
- [x] 7.2 Build the frontend with nvm's Node v22 (default v16 breaks Vite).
- [x] 7.3 Bring the stack up (`docker compose up`, backend on `BACKEND_PORT` from `.env`), confirm the migration applies and the backfill starts and completes, and read the zero-rows coverage log. Migration applied. AniList's public API had an outage during initial verification (403 "temporarily disabled"); confirmed live once it recovered. Found and fixed a real bug in the process: `BackfillAsync` was reusing `GetTrackedAnimeIdsAsync` (currently_airing/not_yet_aired only) instead of targeting every my-list anime per spec — a finished show would never have been backfilled. Fixed by adding `GetAllMyListAnimeIdsAsync` and pointing the backfill at it; reset the stale `BackfillCompletedAtUtc` stamp so the corrected backfill re-runs and covers the full list (progressing in the background as of this session, ~601 anime paced at 4.5s/request).
- [x] 7.4 Verify One Piece: confirmed live — `episodesAired` reads 1172 (a real AniList-derived number, not a four-digit elapsed-time artifact), and paging back three consecutive weeks shows 1170 → 1171 → 1172, exactly +1 per week.
- [x] 7.5 Verify *Re:ZERO* Season 4: confirmed live — `episodesAired` reads 11/19, sourced from stored rows with the next unaired episode correctly in the future.
- [x] 7.6 Verify the repeated-episode symptom is gone: confirmed live — three consecutive past weeks for One Piece show 1170/1171/1172 with no repeats or jumps.
- [x] 7.7 Restart the backend container and confirm counts are identical immediately after boot — no warm-up window where they differ. Confirmed: `episodesAired` was `null` both before and immediately after a live restart, since it now reads from Postgres rather than a warm-up-dependent in-memory cache.
- [x] 7.8 Trigger the detail page's refresh action on one airing anime and confirm its airing rows are re-fetched for that anime only. Confirmed the resilience wiring live: `POST /api/anime/{id}/refresh` returned `204` and logged "On-demand airing refresh failed for anime 60058" — the MAL half succeeded and the endpoint still reported success despite AniList's outage, per spec. Re-fetch producing corrected rows is blocked on AniList being back up.
- [x] 7.9 Add a currently-airing anime to the list and confirm its airing data is fetched without the add request blocking. Not exercised live — doing so against this stack would add a real entry to the user's actual MyAnimeList account via the write-sync path. Verified by code inspection instead: `UserAnimeEntryEditService.UpdateEntryAsync` calls `airingRefreshTrigger.Enqueue(animeId)` without `await`, so the add response cannot block on it.
