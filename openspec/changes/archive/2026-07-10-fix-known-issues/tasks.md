## 1. MAL client & data model foundation

- [x] 1.1 In `Services/Mal/MalClient.cs` `GetUserAnimeListAsync`, add `list_status{status,score,num_episodes_watched,start_date,finish_date,num_times_rewatched,is_rewatching}` and `&nsfw=true` to the query
- [x] 1.2 In `MalClient.cs`, add `alternative_titles{en}` to `DefaultAnimeFields`; add `average_episode_duration,source` to `FullDetailAnimeFields`
- [x] 1.3 In `Services/Mal/Dto/MalAnimeNode.cs`, add `AlternativeTitles` (`{ En }`), `AverageEpisodeDuration` (int? seconds), and `Source` (string?)
- [x] 1.4 In `Models/AnimeMetadata.cs`, add `EnglishTitle`, `AverageEpisodeDurationSeconds`, `Source`
- [x] 1.5 In `Models/ActivityLog.cs`, add nullable `PreviousEpisodesWatched`
- [x] 1.6 In `Services/Mal/MalMappingExtensions.cs`, map English title/duration/source in `ApplyTo` and English title in `ApplyLeanTo`
- [x] 1.7 Add an EF Core migration for the new columns and confirm it applies on startup (`Program.cs:118-122`)

## 2. Corrective re-sync

- [x] 2.1 Add a re-sync/upsert method (extend `Services/Import/InitialImportService.cs` or a new service) that upserts `AnimeMetadata` via `ApplyTo` and updates each `UserAnimeEntry` from `list_status`, skipping entries where `PendingSync == true`
- [x] 2.2 Expose a re-sync trigger (controller endpoint + Settings page action) and reuse the existing paced fetch path
- [x] 2.3 Verify a run upserts existing rows (status/score/episodes) and backfills English title/duration/source/broadcast/`AiredFrom`

## 3. Season browser (exactly one season)

- [x] 3.1 In `Services/Season/SeasonBrowseService.cs` `FetchAndCacheAsync`, populate `AiredFrom` from `node.StartDate` and add a `SeasonAnimeListing` only when `SeasonCalendar.GetSeasonFor(ParseMalDate(node.StartDate)) == (year, season)`
- [x] 3.2 In `Data/Repositories/SeasonRepository.cs` `GetPageAsync`, filter listings to those whose `AnimeMetadata.AiredFrom` resolves to the requested (year, season); fall back to shown when start date is unknown
- [x] 3.3 Verify a long-running anime (e.g. One Piece) appears only in its premiere season and not in later seasons

## 4. Airing schedule

- [x] 4.1 In `Services/Scheduling/BroadcastLocalTimeConverter.cs` `ResolveForWeek`, gate on `AiringStatus == "currently_airing"`
- [x] 4.2 Verify (after re-sync) that currently-airing my-list anime appear on their broadcast days and finished shows do not

## 5. Top anime rank 500

- [x] 5.1 In `Services/Library/TopAnimeService.cs`, raise `RankingSize` to 500 and confirm `GetRankingAsync(limit: 500)` returns up to 500 ranked rows

## 6. Profile latest-updates feed

- [x] 6.1 In `Services/Entries/UserAnimeEntryEditService.cs` `ApplyEpisodesWatched`, set `PreviousEpisodesWatched` on the logged episode change
- [x] 6.2 In `Services/Profile/ProfileService.cs` (feed path only), fetch a larger recent window, filter to `Added` + `EpisodeIncremented` where new > previous, collapse consecutive same-anime increments into one item, then take N; leave the full-history overlay unchanged
- [x] 6.3 Verify the feed shows only additions and collapsed increases, never decreases/status changes/drops

## 7. Backend DTOs

- [x] 7.1 Add `englishTitle` to all title-bearing DTOs (season, top, detail, my-list, profile items, airing, search, dashboard)
- [x] 7.2 Add `source` and `averageEpisodeDurationSeconds` to `AnimeDetailDto` (and confirm `airingStatus` is exposed)

## 8. Frontend — shared pieces

- [x] 8.1 Add `englishTitle` to the relevant types in `frontend/src/api/types.ts`
- [x] 8.2 Add a `pickDisplayTitle(title, englishTitle)` util (prefer English when present)
- [x] 8.3 Thread `englishTitle` through `components/AnimeCard.tsx` and call `pickDisplayTitle` at the non-card sites: `TopAnimePage`, `AnimeDetailPage`, `MyListPage`, `ProfilePage`, `AiringPage`, `AiringTodayList`, `SearchBar`
- [x] 8.4 Add a reusable `components/Pagination.tsx` (page-number buttons + left/right arrows)

## 9. Frontend — page fixes

- [x] 9.1 `components/Navbar/Navbar.tsx`: add `{ to: '/my-list', label: 'My List' }`, reorder to Home · My List · Top · Season · Airing, relabel "Seasonal" → "Season"
- [x] 9.2 `pages/SeasonPage.tsx`: move year/season/sort into URL query params via `useSearchParams`, defaulting to `currentSeasonTarget()`; add year + season quick-jump `<select>` controls beside the steppers
- [x] 9.3 `components/CurrentlyWatchingCarousel.tsx`: render arrows only when `scrollWidth > clientWidth`; recompute via `ResizeObserver` and on `items` change
- [x] 9.4 `pages/TopAnimePage.tsx`: paginate client-side at 50/page using `Pagination`, with controls at the bottom and arrow controls at the top-right
- [x] 9.5 `pages/AnimeDetailPage.tsx`: add Status/Source/Duration rows, use "No info" for missing Info fields, and remove the AniList link (keep MyAnimeList)

## 10. Verification

- [x] 10.1 `cd backend && dotnet build` (and `dotnet test` if present) pass; migration applies on startup — local SDK is 9.0 and the project targets net10.0, so built via `docker compose build backend` instead (uses `mcr.microsoft.com/dotnet/sdk:10.0`); build succeeded; the running `bettermal-backend-1` container (current code, clean git tree) is live proof migrations applied on startup
- [x] 10.2 `cd frontend && npm run build` passes — local Node is 16.20.2 (Vite needs 20.19+/22.12+), so built via `docker compose build frontend` instead (uses `node:22-alpine`); build succeeded
- [x] 10.3 Run the app and walk the checklist in the design's Migration Plan (Home populated + arrows conditional, navbar, season persistence + one-season, top pagination to 500, airing populated, detail Info + no AniList, profile feed, English titles) — verified against the live running instance and current source (no browser tool available in this session, so verified via API responses + code, not a manual click-through):
  - Home populated: `GET /api/dashboard` returns real currentlyWatching/airingToday/currentSeason data with `englishTitle`
  - Carousel arrows conditional: `CurrentlyWatchingCarousel.tsx` gates on `scrollWidth > clientWidth` via `ResizeObserver`
  - Navbar: `Navbar.tsx` is Home · My List · Top · Season · Airing, as specified
  - Season persistence + one-season: `SeasonPage.tsx` uses `useSearchParams`/`currentSeasonTarget`; `SeasonRepository.GetPageAsync` filters listings to the requested (year, season)
  - Top pagination to 500: `GET /api/top-anime` returns 500 items; `TopAnimePage.tsx` paginates client-side at `PAGE_SIZE = 50`
  - Airing populated: `GET /api/airing` returns real weekly slots with converted local times
  - Detail Info + no AniList: `GET /api/anime/38101` (a fully-synced anime) returns `source: "manga"`, `averageEpisodeDurationSeconds: 1445`, `airingStatus`, genres, sequel link; `AnimeDetailPage.tsx` has no AniList reference
  - Profile feed: `GET /api/profile.recentActivity` shows only `EpisodeIncremented`/`Added` entries, never decreases/status changes
  - English titles: present throughout (`englishTitle` on dashboard/detail/season/top/airing responses)
