## Context

The Anime Tracker is an ASP.NET Core 10 + EF Core/Postgres backend (`backend/AnimeTracker.Api`) with a React 19 + TS + react-router v7 frontend (`frontend/`). MAL is the only external data source; all page reads come from Postgres (no live API call on render). Investigation of `anime-tracker-known-issues.md` shows most user-visible breakage stems from one MAL-client field bug, plus several isolated per-page fixes. This design covers the whole set because they share a data foundation (new persisted fields + a corrective re-sync) that must land before the dependent page fixes.

## Goals / Non-Goals

**Goals:**
- Import status/score/episodes correctly and include NSFW-rated titles.
- Persist and display English title, episode duration, and source.
- Correct existing mis-imported rows without discarding local unsynced edits.
- Each anime appears in exactly its premiere season.
- Populate the Airing page and Home dashboard from correct data.
- Top anime to rank 500 with 50/page pagination.
- Profile feed limited to additions + collapsed episode increases.
- Navbar includes My List; Season selection survives back-navigation; carousel arrows only when overflowing; detail Info gains Status/Duration/Source with "No info" fallbacks; AniList link removed.

**Non-Goals:**
- Integrating AniList (the link is removed, not fixed via id mapping — a deliberate product decision).
- Fetching real per-episode air dates (episode numbers stay derived from `AiredFrom` as today).
- Reworking the reconciliation review flow, editor overlay, or auth.
- Changing the dashboard from a curated set of slices into a full my-list view (My List page already serves that).

## Decisions

### D1. Fix `list_status` + `nsfw` at the request layer, not the mapping
`MalMappingExtensions.ToUserAnimeEntry` already maps `list_status` fields correctly; they were simply never fetched. Add `list_status{...}` and `nsfw=true` to the animelist query in `MalClient.GetUserAnimeListAsync`. This is the root-cause fix — the mapping's null-defaults were only masking absent data. *Alternative rejected:* changing the mapping defaults would hide, not fix, the problem.

### D2. New persisted fields via one additive migration
Add `EnglishTitle`, `AverageEpisodeDurationSeconds`, `Source` to `AnimeMetadata` and `PreviousEpisodesWatched` (nullable) to `ActivityLog`. Add the corresponding properties to `MalAnimeNode` (`AlternativeTitles{En}`, `AverageEpisodeDuration`, `Source`) and map them in `ApplyTo` (all) and `ApplyLeanTo` (English title only, since listings should show it too). `alternative_titles{en}` goes in `DefaultAnimeFields` (covers search/season/ranking/list); duration+source in `FullDetailAnimeFields`. One EF migration, applied on startup (`Program.cs:118-122`). All additive/nullable → no backfill required for the schema itself.

### D3. Corrective re-sync as an upsert pass, guarded by `pending_sync`
`InitialImportService` skips already-cached anime, so it can't repair the 577 bad rows. Add a re-sync path that, for each MAL list edge, upserts `AnimeMetadata` (`ApplyTo` via a full-detail fetch, backfilling English title/duration/source/broadcast/`AiredFrom`) and updates the `UserAnimeEntry` from the now-correct `list_status` — **skipping entries with `PendingSync == true`** (mirrors the guard at `ReconciliationService.cs:44`). Exposed as a Settings action/endpoint and run once after deploy. Reuses the existing ~1 req/s pacer. *Alternative rejected:* wipe-and-reimport (loses local edits and history); rely on nightly reconciliation only (slow, and its diff is held for manual review rather than auto-applied).

### D4. Season "exactly one season" — gate on premiere + read-time filter
Two complementary changes so both new and already-cached data are correct:
- **Write:** in `SeasonBrowseService.FetchAndCacheAsync`, add a `SeasonAnimeListing` only when `SeasonCalendar.GetSeasonFor(ParseMalDate(node.StartDate)) == (year, season)`, and populate `AiredFrom` from the node's `start_date` (already in `DefaultAnimeFields`). Prevents future pollution.
- **Read:** in `SeasonRepository.GetPageAsync`, filter listings to those whose joined `AnimeMetadata.AiredFrom` resolves to the requested (year, season). Heals existing pollution (e.g. One Piece in 2026) immediately for all past seasons without re-fetching each. Anime with an unknown start date fall back to shown.

`SeasonCalendar.GetSeasonFor` (`SeasonCalendar.cs:9`) already maps date→season. *Alternative rejected:* write-only fix (leaves already-cached past seasons polluted since they never re-fetch).

### D5. Airing populated by re-sync + gate on currently-airing
The frontend is correct; slots are empty because broadcast fields are unpopulated (broken import + lean paths skip broadcast). D3 populates them. Additionally gate `BroadcastLocalTimeConverter.ResolveForWeek` on `AiringStatus == "currently_airing"` so finished shows don't reappear weekly (matches the dashboard's existing `NextBroadcastInstant` gating).

### D6. Top anime to 500 in a single call; paginate client-side
Raise `TopAnimeService.RankingSize` from 100 to 500 (MAL ranking accepts `limit=500` in one request). Storage and read path already order by rank — no migration. The API returns the full ranked list; the frontend paginates at 50/page with a new reusable `components/Pagination.tsx`. *Alternative rejected:* server-side offset paging adds API surface for a bounded 500-item list that renders fine client-side.

### D7. Profile feed: record direction at write time, shape at read time
Add `PreviousEpisodesWatched` on `ActivityLog` and set it in `UserAnimeEntryEditService.ApplyEpisodesWatched`. Keep the full history complete; do all feed shaping in `ProfileService.GetProfileAsync` (feed only, not the history overlay): pull a larger recent window, filter to `Added` + `EpisodeIncremented` where new > previous, collapse consecutive same-anime increments into one item, then take N. *Alternative rejected:* only logging increases (loses decrease history and can't distinguish direction for existing rows).

### D8. Frontend title display via a shared helper + AnimeCard prop
Add `pickDisplayTitle(title, englishTitle)` (prefer English when present). Add `englishTitle` to the frontend DTO types and the backend DTOs that carry titles. Thread it through `AnimeCard` (covers Home current-season, currently-watching, Season grid at once) and call the helper in the ~6 non-card render sites (Top, Detail, MyList, Profile, Airing, Search). No shared display helper exists today, so this centralizes the rule.

### D9. Season selection in URL state
Move year/season/sort in `SeasonPage` from `useState` into URL query params via `useSearchParams`, defaulting to `currentSeasonTarget()` when absent. This makes selection survive back-navigation (the page remounts from the URL, not a reset default) and keeps the navbar link as bare `/season`. Add native `<select>` year/season quick-jump controls beside the existing steppers (matches the app's existing `<select>` sort control; no shared Dropdown component exists).

### D10. Carousel arrows gated on measured overflow
In `CurrentlyWatchingCarousel`, compare `track.scrollWidth > track.clientWidth` and render arrows only when true; recompute via `ResizeObserver` and when `items` change.

### D11. Anime detail Info + link
Render Type, Status (map existing `airingStatus` to a friendly label), Source (prettify e.g. `light_novel` → "Light novel"), Duration (format seconds → "N min"), Studio, Aired, Genres — each showing "No info" when missing (replacing current `—`/`Unknown`/`?`). Remove the AniList `<a>`; keep MyAnimeList.

## Risks / Trade-offs

- **Re-sync cost** (~1 req/s × ~600 anime ≈ 10 min) → acceptable one-time run; reuses existing pacer and is resumable in spirit (upsert is idempotent).
- **AiredFrom precision from partial MAL dates** (e.g. `"1999"` → Jan 1 → winter) → premiere-season classification may be off by a quarter for shows MAL only dates to a year; acceptable and rare, and still yields exactly one season.
- **Read-time season filter needs AiredFrom populated** → re-sync (D3) and the season fetch (D4 write) both populate it; anime with a genuinely unknown start date fall back to shown rather than hidden.
- **Existing ActivityLog rows lack PreviousEpisodesWatched** (null) → feed treats null-delta `EpisodeIncremented` as best-effort increases; only affects pre-migration history, which is minimal (import doesn't write per-episode logs).
- **English title in `DefaultAnimeFields`** slightly enlarges every listing response → negligible; MAL supports the field on all node endpoints.

## Migration Plan

1. Ship backend: MAL field changes, DTO/model fields, EF migration (auto-applied on startup), season write+read fixes, airing gate, top-anime 500, profile write+read shaping, re-sync endpoint.
2. Ship frontend: DTO types, title helper + AnimeCard, navbar, SeasonPage URL state + quick-jump, carousel overflow, Top pagination, detail Info + link removal.
3. **Run the re-sync once** from Settings to correct existing rows and backfill new fields/broadcast data.
4. Rollback: revert app; the additive migration is backward-compatible (new columns simply go unused), so no down-migration is required for a code rollback.

## Open Questions

- None blocking. The two product decisions (remove AniList link; force one-time re-sync) are resolved. Whether to split into two smaller changes (`fix-mal-sync-data` vs `fix-tracker-pages`) is an optional packaging choice, not a design question.
