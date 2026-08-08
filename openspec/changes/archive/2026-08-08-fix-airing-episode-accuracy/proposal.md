## Why

The "episodes aired so far" number and the airing schedule tied to it are wrong for shows that take breaks (One Piece, *Re:ZERO* Season 4). Today the number is *computed* — weeks elapsed since MAL's start date, one episode per week — whenever AniList's per-episode data isn't in the in-memory cache, and that cache is empty on every container start and only covers a 150-day window even once warm. So the count starts wrong, self-corrects when the background refresh lands, and goes wrong again on the next restart or whenever the view reaches outside the cached window. Paging back on the Airing page recomputes the same estimate per week, which is why one show showed "Episode 15" three weeks running and One Piece jumped from ~1,100 to 1,300 between adjacent weeks.

## What Changes

- **Confirmed-aired episodes become persisted rows.** A new table stores one row per episode per anime (anime id, episode number, confirmed air instant, source). It survives restarts, so a boot no longer resets every count to a guess.
- **The Airing page's past view reads stored rows only.** No per-render recalculation. A past week with no stored episode renders empty rather than showing a fabricated episode number. **BREAKING** for the deep-past view: weeks AniList has no data for go from wrong-but-populated to correctly empty.
- **The aired-so-far count comes from stored rows.** Highest episode number whose confirmed air instant has passed. When no rows exist, the count is reported as unknown instead of estimated. The weekly-cadence estimator (`EstimateAiredFromCadence` / `EstimateOnLocalDate`) is deleted, not kept as a fallback — it is the defect.
- **AniList owns episode timing; MAL keeps the rest.** Per-episode air instants, the aired-so-far count, and the next-episode instant come from AniList only. MAL continues to supply total episode count, airing status, and all static metadata (title, synopsis, genres, cover, studio, score, rank). The MAL-total clamp on the aired count is removed so a stale MAL total can no longer suppress a confirmed episode.
- **Full-history backfill ships with the fix.** A one-time migration-triggered pass pages AniList's `airingSchedules` for every tracked anime's entire run (One Piece ≈ 23 paged calls; most shows 1) and overwrites existing rows, so bad data already in the DB is corrected rather than only future data.
- **Refresh triggers replace fixed polling.** The 20s-after-boot + every-6h loop is removed. Refresh now happens on boot when the last successful refresh wasn't today, on an elapsed-time daily check (~24h since last success, evaluated hourly, so an intermittently-running Docker container can't skip a day), when an airing/upcoming anime is added to the list, at season boundaries, and on the anime page's manual refresh.
- **Incomplete-data shows get their own recheck cadence.** When AniList reports a `nextAiringEpisode`, recheck 1 month before it, 1 week before, and on the day. Past that with no new confirmed data — or when AniList reports no `nextAiringEpisode` at all — recheck every 3 days.

## Capabilities

### New Capabilities
- `episode-airing-data`: The persisted per-episode confirmed-airing record — what a confirmed episode is, that AniList is its only source, how rows are written and overwritten, the backfill, and the full set of refresh triggers including the incomplete-data recheck cadence.

### Modified Capabilities
- `airing-schedule`: Weekly view slots are read from stored confirmed-episode rows instead of being resolved per-date by a schedule service that falls back to an estimate; weeks with no stored data render empty.
- `main-dashboard`: The "Aired-episode count for followed airing shows" requirement drops the weekly-cadence estimate and the MAL-total clamp; the count comes from stored rows or is unknown. "Airing today" and the next-episode countdown likewise read stored rows.
- `anime-detail`: The aired count shown for a currently-airing title comes from stored rows; the on-demand refresh action also re-fetches that anime's airing data immediately.
- `metadata-refresh`: The on-demand single-anime refresh is no longer a single MAL call — it additionally re-fetches AniList airing data for that anime.
- `data-persistence`: Adds the confirmed-episode-airing entity and the airing-refresh bookkeeping (last successful refresh instant, per-anime next-recheck instant).

## Impact

**Backend** (`backend/AnimeTracker.Api`):
- New: `Models/EpisodeAiring*.cs` (entity), `Models/AiringRefreshLog.cs`, EF migration, `Data/Repositories/IEpisodeAiringRepository.cs`.
- Rewritten: `Services/Airing/EpisodeScheduleService.cs` (estimator removed, reads rows), `Services/Airing/AiringScheduleService.cs`, `Services/Airing/EpisodeScheduleRefreshService.cs` (full-history paging, overwrite semantics, recheck scheduling), `Services/Airing/AniList/AniListClient.cs` (paging + `nextAiringEpisode`).
- Replaced: `Services/Airing/EpisodeScheduleRefreshBackgroundService.cs` (trigger-based, not fixed-interval).
- Deleted: `Services/Airing/EpisodeScheduleCache.cs` / `IEpisodeScheduleCache.cs` (superseded by the table).
- Touched: `Services/Dashboard/MainDashboardService.cs`, `Services/Detail/AnimeDetailService.cs`, `Services/Library/MyListService.cs`, `Services/Entries/UserAnimeEntryEditService.cs` (episode-watched cap), `Services/Season/SeasonBrowseService.cs` if it triggers adds, `Controllers/MetadataRefreshController.cs`, `Program.cs`.

**Frontend** (`frontend/src`): `AiringPage.tsx` and `HomePage.tsx` must render an unknown aired count and an empty past week without falling back to a placeholder number.

**External**: AniList GraphQL call volume rises once during backfill (bounded by its 30 req/min limit, paced), then falls below today's every-6h polling.

**Data**: One additive migration. No existing column is dropped; `AnimeMetadata.TotalEpisodes`, `AiredFrom`, `AiredTo`, `BroadcastDayOfWeek`, and `BroadcastTime` remain MAL-sourced and are still used for display and non-timing logic.
