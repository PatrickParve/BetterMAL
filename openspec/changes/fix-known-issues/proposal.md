## Why

The personal Anime Tracker has ~13 reported defects (see `anime-tracker-known-issues.md`). Most trace back to a small set of MAL API-client data bugs: the user-list request never asks for `list_status`, so every imported entry lands as "plan to watch" with no score or episodes, which in turn empties the Home dashboard and Airing page. The remaining issues are isolated season/pagination/navigation/display fixes. This change corrects the data foundation first, then fixes the pages that depend on it.

## What Changes

- **MAL user-list request** now includes `list_status{…}` and `nsfw=true`, so status, score, episodes, and NSFW-rated titles import correctly (fixes "all plan to watch" and the 577-vs-595 gap).
- **New cached fields**: English title, average episode duration, and source are fetched from MAL and stored; `ActivityLog` records the previous episode count.
- **One-time re-sync**: a non-destructive upsert re-fetches the whole list to correct already-imported rows (preserving entries with pending local edits).
- **Season browser**: each anime appears in exactly one season (its premiere season), instead of every season it is still airing during.
- **Airing page** shows currently-airing list anime once broadcast data is populated; finished shows are excluded.
- **Top anime** extends to rank 500 with 50-per-page pagination (page numbers + prev/next arrows, top and bottom).
- **Profile "Latest updates"** feed shows only list additions and episode increases (consecutive increments collapsed), never decreases or drops.
- **Navbar** gains "My List" and is reordered to Home · My List · Top · Season · Airing.
- **Season page** persists the selected season/sort through back-navigation (URL state) and gains a quick-jump year/season dropdown.
- **Home** hides the currently-watching carousel arrows unless the row actually overflows.
- **Anime detail** Info section adds Status, Duration, and Source, shows "No info" for missing fields, and **removes the AniList link** (kept only MyAnimeList).
- **English titles** are shown wherever a title renders, when MAL provides one.

## Capabilities

### New Capabilities
<!-- None — all changes modify behavior of existing capabilities. -->

### Modified Capabilities
- `mal-api-integration`: request `list_status`, `nsfw=true`, `alternative_titles{en}`, `average_episode_duration`, and `source`.
- `initial-import`: add a re-sync/upsert mode that corrects already-imported entries.
- `data-persistence`: add `EnglishTitle`, `AverageEpisodeDurationSeconds`, `Source` to anime metadata and `PreviousEpisodesWatched` to the activity log.
- `season-browser`: each anime appears only in its premiere season; add persisted selection and a season quick-jump.
- `airing-schedule`: surface only currently-airing list anime with broadcast data.
- `library-views`: Top anime covers up to rank 500 with 50-per-page pagination.
- `profile-stats`: the latest-updates feed shows only additions and episode increases (collapsed).
- `navigation-and-search`: navbar includes "My List" and is reordered; titles prefer the English name.
- `main-dashboard`: currently-watching carousel arrows appear only when the row overflows.
- `anime-detail`: Info adds Status/Duration/Source with "No info" fallbacks; AniList link removed.

## Impact

- **Backend** (`backend/AnimeTracker.Api`): `Services/Mal` (client, DTOs, mapping), `Services/Import`, `Services/Sync`, `Services/Season`, `Services/Airing`, `Services/Library`, `Services/Profile`, `Services/Entries`; `Models/AnimeMetadata` + `Models/ActivityLog` (one EF migration); assorted DTOs gain `englishTitle`/`duration`/`source`.
- **Frontend** (`frontend/`): `api/types.ts`, `components/{Navbar,AnimeCard,CurrentlyWatchingCarousel}`, new `components/Pagination`, `pages/{SeasonPage,TopAnimePage,AnimeDetailPage,ProfilePage}`, and a shared `pickDisplayTitle` util.
- **Data**: one migration; a one-time re-sync run after deploy corrects existing rows. No breaking API changes (additive DTO fields).
- **External**: MyAnimeList API only; no new dependencies (AniList link removed rather than integrated).
