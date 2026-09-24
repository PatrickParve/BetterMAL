## Why

Every picture the picker offers today comes from MyAnimeList, and MAL serves only its own `large` rendition, a few hundred pixels wide. Most of the "the art looks soft" complaints trace back to MAL's source images, not to the app. TMDB hosts full-resolution posters and textless backdrops for most anime. TMDB can't look anything up by MAL id, though, so the app first needs a MAL → TMDB id mapping. The same third-party mapping also carries IMDb ids, which gives the detail and series pages an IMDb link for free.

## What Changes

- **A weekly MAL → TMDB/IMDb id mapping.** The app downloads the community-maintained Fribb `anime-list-mini.json` (plain GET, no auth) and keeps only these fields for each MAL id:
  - the TMDB TV id
  - the TMDB season number (`season.tmdb`)
  - the TMDB movie ids
  - the IMDb ids

  The brief's field list left out the season number, but `GET /tv/{id}/season/{n}/images` can't be called without it. Nothing else from the file is stored: no raw JSON, no `type`, no AniList id. The sync runs as one more step in the existing hourly airing-refresh tick, which also runs at start-up. It fires when the last successful sync is more than 7 days old, so no new scheduler is added.
- **A hand-edited custom mapping.** A `backend/custom/id-mapping.json` file of Fribb-shaped entries fills ids the source lacks and, with `override`, corrects values it has wrong. It is merged when the mapping is read, so the weekly sync neither removes nor changes it, and an entry the source later fills in is ignored without any edit.
- **TMDB images fetched and cached by TMDB id.** Posters and backdrops come from `/tv/{id}/images`, `/tv/{id}/season/{n}/images` (posters only) and `/movie/{id}/images`, always with `include_image_language=ja,en,null`. Logos are dropped and never stored. There are three keyed caches:
  - series images, keyed by TV id
  - season images, keyed by (TV id, season)
  - movie images, keyed by movie id

  Each image keeps its file path, language, kind (poster or backdrop), width and height. The URL `https://image.tmdb.org/t/p/original{file_path}` is built when read, never stored. A set is fetched in a follow-up request the first time a page needs it, then refreshed once it is **30 days** old or when you press **Refresh data**. The API key lives in `.env` (`TMDB_API_KEY`). Without a key the feature stays off and everything else works as before.
- **Old TMDB sets are deleted.** TMDB's terms forbid caching what it returns for more than 6 months, and a set nobody opens is never refetched, so every hourly tick deletes each set last fetched more than 150 days ago. A set someone opens again is fetched anew. A picture you picked is not touched: it is stored on the anime or series, not in the cache.
- **TMDB images can be picked (confirmed).** They go into the existing Choose-picture overlay, which gains labelled sections.
  - On an anime's picker: **MyAnimeList** first, then TMDB **Series / Season N / Movie**, each split into **No language / Japanese / English**.
  - On a series' picker: MyAnimeList, then TMDB split by language only, with series, season and movie images mixed together.

  A picked TMDB image becomes that anime's or series' picture everywhere, just like a MAL pick. The rule "only pictures MAL publishes may be chosen" widens to also allow the TMDB images in the anime's or series' own TMDB sets.
- **The series page aggregates the whole franchise (assumption confirmed, with one correction).** A BetterMAL series is the whole story component: main line plus extras such as movies, OVAs, specials and spin-offs. The correction is that a series **often spans more than one TMDB TV id**. 50 of the 279 series stored in your database do, for example Naruto with 4, Dragon Ball with 7 and the Gundam series with 36. So the series page draws on:
  - the series-level images of **every TV id on the main line**
  - the posters of **every season of those shows** that a member maps to (confirmed)
  - the images of **every movie id anywhere in the series**

  Fetching is capped at 20 TMDB calls per visit. Later visits fetch the rest.
- **IMDb links.** The detail page shows one IMDb link per mapped IMDb id, which is almost always exactly one. The series page links its root entry's id, the same entry its other external links point at. No link is shown when no id is mapped.
- **Device transfer.** When an imported choice is a TMDB image that this device hasn't cached, the import refetches that anime's or series' TMDB sets, then checks the choice again. It already does the same for MAL picture sets.

## Capabilities

### New Capabilities

- `external-id-mapping`: the weekly sync of the third-party MAL → TMDB/IMDb mapping. It covers which fields are kept, keying on the MAL id, first match winning on a duplicate, the 7-day elapsed-time cadence on the existing tick, the failure backoff, and the guard against a truncated file.
- `tmdb-artwork`: fetching, caching and refreshing TMDB posters and backdrops keyed by TMDB id (series, season, movie). It covers the language filter, how the URL is built, which sets an anime or a series draws from, the per-visit budget on the series page, optional key configuration, failure and rate-limit handling, the deletion of sets older than 150 days, and TMDB attribution.

### Modified Capabilities

- `artwork-selection`:
  - TMDB images join the option sets: an anime gets its own scopes, a series gets its franchise set.
  - The "only MAL pictures" rule widens and is renamed.
  - The series option set gains franchise TMDB images, and that requirement is renamed.
  - The picker shows MAL and TMDB in labelled sections: scope × language on the anime picker, language only on the series picker.
- `anime-detail`:
  - IMDb link(s) join the external links.
  - The Choose-picture control counts TMDB options.
  - The detail read carries the anime's TMDB pictures and flags when a follow-up fetch is due.
  - **Refresh data** also refetches the anime's TMDB sets.
- `series-page`:
  - The root's IMDb link(s) join the external links.
  - The picture control counts TMDB options.
  - The series read carries the franchise's TMDB pictures and a pending count, with a bounded follow-up fetch.
- `metadata-refresh`: the on-demand single-anime refresh also refetches that anime's TMDB sets. A TMDB failure never fails the action.
- `device-transfer`: when a refused picture is a TMDB image, the one extra check fetches it from TMDB rather than MAL.
- `deployment`: `.env` gains an optional `TMDB_API_KEY`, read by both the Docker and the native run.
- `settings-page`: a Credits group after Account, holding TMDB's logo and the notice its terms require.

## Impact

- **Backend:**
  - New `Services/IdMapping/`: download, parse and sync.
  - New `Services/Tmdb/`: client, options, URL builder, artwork service, cache purge.
  - `EpisodeScheduleRefreshBackgroundService` gains the mapping step and the cache purge.
  - Also changed: `ArtworkSelectionService`, `AnimePicture`, `SeriesPicturePool`, `AnimeDetailService`/`AnimeDetailDto`, `SeriesService`/`SeriesDto`, the on-demand refresh path, `TransferImportRunner`, `Program.cs` and `DevDotEnvOverlay`.
- **New endpoints:**
  - `POST /api/anime/{id}/tmdb-pictures/refresh`
  - `POST /api/series/by-anime/{animeId}/tmdb-pictures/refresh`
- **Schema, one migration:**
  - New tables: `AnimeIdMappings`, `AnimeIdMappingSyncStates`, `TmdbTvImageSets`/`TmdbTvImages`, `TmdbSeasonImageSets`/`TmdbSeasonImages`, `TmdbMovieImageSets`/`TmdbMovieImages`.
  - No existing column changes.
  - A picked TMDB image is stored in the existing `SelectedPictureUrl` as its full URL (design D9).
- **Frontend:** `PicturePickerOverlay` (sections), `AnimeDetailPage`, `SeriesPage`, `api/client.ts` and `api/types.ts`. Also `SettingsPage` (a Credits group) and a new `TmdbAttribution` component with TMDB's logo file.
- **Config and docs:** `.env.example`, `docker-compose.yml`, the backend `Dockerfile` (which copies the new `backend/custom/` folder into the image), README and SETTINGS.md (key setup, plus the attribution TMDB's terms require), and CODE_GUIDE.md.
- **External services:**
  - GitHub raw: one anonymous download a week.
  - TMDB v3 API: needs a free API key per installation, allows non-commercial use, requires attribution.
  - No new NuGet or npm packages.
- **Out of scope:** the existing AniList `idMal` flow (untouched), `anidb_type`, `anilist_id`, logos, episode stills, `episode_offset` and `GET /configuration`.
