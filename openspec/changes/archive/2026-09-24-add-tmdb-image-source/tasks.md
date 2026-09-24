## 1. Configuration and plumbing

- [x] 1.1 Add `backend/AnimeTracker.Api/Services/Tmdb/TmdbOptions.cs`: `SectionName = "Tmdb"`, `ApiKey` (string, default `""`), and `IsConfigured => !string.IsNullOrWhiteSpace(ApiKey)`. Bind it in `Program.cs` beside `MalOptions`. Add `"Tmdb": { "ApiKey": "" }` to `appsettings.json`.
- [x] 1.2 In `Services/Infrastructure/DevDotEnvOverlay.cs`, add `SetIfNonEmpty(result, "Tmdb:ApiKey", env.GetValueOrDefault("TMDB_API_KEY"))` to `ToConfiguration`. In `DevDotEnvOverlayTests`, add cases for a key that is present, empty, and absent (absent or empty sets nothing).
- [x] 1.3 In `docker-compose.yml`, pass `Tmdb__ApiKey: ${TMDB_API_KEY:-}` to the backend service. In `.env.example`, add an optional `# --- TMDB (optional) ---` block with `TMDB_API_KEY=`. Explain that it is a v3 API key from themoviedb.org → Settings → API, that it is only needed for TMDB pictures, and that the app runs fully without it.
- [x] 1.4 In `Program.cs`, register a named `HttpClient` `"anime-id-mapping"` (2-minute timeout). Also register `AddHttpClient<ITmdbClient, TmdbClient>` with base address `https://api.themoviedb.org/3/` and a 15-second timeout (design D2, D4).

## 2. Persistence

- [x] 2.1 Add `Models/AnimeIdMapping.cs` with these properties (design D1). Add a class comment saying there is deliberately no FK to `AnimeMetadata`, as with `AnimeRelatedAnime.RelatedAnimeId`, because most mapped ids are never cached.
  - `AnimeId`: PK, the MAL id
  - `TmdbTvId`: `int?`
  - `TmdbSeasonNumber`: `int?`, stored as given, including 0
  - `TmdbMovieIds`: `List<int>`, non-null, default `[]`
  - `ImdbIds`: `List<string>`, non-null, default `[]`
- [x] 2.2 Add `Models/AnimeIdMappingSyncState.cs`, a singleton bookkeeping row modelled on `AiringRefreshState`: `Id`, `LastSyncedAt?`, `LastAttemptAt?`.
- [x] 2.3 Add the six TMDB cache entities (design D5):
  - `TmdbTvImageSet (TvId)` and `TmdbTvImage (TvId, FilePath)`
  - `TmdbSeasonImageSet (TvId, SeasonNumber)` and `TmdbSeasonImage (TvId, SeasonNumber, FilePath)`
  - `TmdbMovieImageSet (MovieId)` and `TmdbMovieImage (MovieId, FilePath)`

  Each set row carries `FetchedAt` (`DateTimeOffset`) and an `Images` navigation. The image columns `Kind` (enum `TmdbImageKind { Poster, Backdrop }`, stored as its name), `Language` (`string?`), `Width`, `Height` and `Position` live on a plain C# base class, which is not mapped as an EF hierarchy.
- [x] 2.4 Register the eight `DbSet`s in `Data/AnimeTrackerDbContext.cs` and configure them:
  - composite keys
  - `Kind` stored with `HasConversion<string>()`
  - cascade delete from each set to its images
  - column types `integer[]` and `text[]`, following the `PictureUrls`/`Genres` precedent

  Generate migration `AddTmdbImageSource` with `dotnet ef migrations add`. Read the generated `Up`/`Down` and confirm that it only creates the eight new tables and touches no existing one.

## 3. External id mapping

- [x] 3.1 Add `Services/IdMapping/FribbEntry.cs`. Only four properties are declared: `mal_id`, `themoviedb_id { tv, movie[] }`, `season { tmdb }` and `imdb_id`. Every other property is skipped by System.Text.Json. Give `imdb_id` a converter that accepts a JSON array or a bare string, a string becoming a one-element list (design D3). Any other unexpected shape throws.
- [x] 3.2 Add `Services/IdMapping/AnimeIdMappingParser.cs`. `ParseAsync(Stream, CancellationToken)` streams through `JsonSerializer.DeserializeAsyncEnumerable<FribbEntry>` and returns `Dictionary<int, AnimeIdMapping>`:
  - skip an entry with no MAL id
  - the first entry wins for a MAL id (tracked with a `HashSet<int>`)
  - keep `season.tmdb` only when `tv` is present
  - keep IMDb ids only when they match `^tt\d+$`, in order and without duplicates
  - skip an entry left with nothing to keep
- [x] 3.3 Add `AnimeIdMappingParserTests` built from JSON copied verbatim from real entries:
  - 16498: tv, season 1, one IMDb id
  - 40456: movie only, TheTVDB-only season
  - 20: tv with no season
  - 6840: IMDb list `["tt1092390", ""]`
  - two entries sharing one MAL id, where the first wins
  - an entry without `mal_id`
  - an entry whose only id is AniList, which produces no row
  - a bare-string `imdb_id`, which is accepted
  - a `themoviedb_id` given as a bare number, where the parse throws
  - an entry carrying `type`, `anilist_id` and `episode_offset`, none of which appears anywhere in the result
- [x] 3.4 Add `Services/IdMapping/IAnimeIdMappingSyncService` and `AnimeIdMappingSyncService`. `SyncIfDueAsync(ct)`:
  - reads or creates the state row
  - is due when `LastSyncedAt` is null or older than 7 days, and not within 6 hours of a failed `LastAttemptAt > LastSyncedAt`
  - downloads from `https://raw.githubusercontent.com/Fribb/anime-lists/master/anime-list-mini.json`
  - parses the file, then applies the guard: when the new count is below half of the stored count and the stored count is above 0, it abandons the sync
  - diff-applies additions, value changes (reassigning the arrays only when the sequence differs) and removals in one `SaveChangesAsync`
  - stamps `LastSyncedAt = LastAttemptAt = now` on success, or only `LastAttemptAt` on failure
  - catches everything except cancellation, logs, and returns an outcome enum; it never throws (design D2/D3)

  Register it as scoped.
- [x] 3.5 Add `AnimeIdMappingSyncServiceTests`, using EF InMemory and a fake `HttpMessageHandler`:
  - the first sync stores the rows
  - three days later there is no request
  - eight days later it syncs
  - a failed download leaves the rows and `LastSyncedAt` untouched and stamps `LastAttemptAt`
  - a retry within 6 hours is skipped, and 7 hours later it runs
  - a truncated file changes nothing
  - a file under half the stored count changes nothing
  - a changed TV id is updated
  - a vanished MAL id is removed
  - only the mapping URL is ever requested, with no TMDB host
- [x] 3.6 In `Services/Airing/EpisodeScheduleRefreshBackgroundService.cs`, at the start of each loop iteration, create a **separate** scope, resolve `IAnimeIdMappingSyncService` and await `SyncIfDueAsync` before the airing tick's own scope is created (design D2). Extend the class summary with one sentence about the weekly mapping step and why it has its own scope. Add a focused test, or a test of the loop's extracted step, showing that a failing mapping sync still lets the airing tick run.

## 4. TMDB client and URLs

- [x] 4.1 Add `Services/Tmdb/TmdbImageUrl.cs` (design D6):
  - `OriginalBase = "https://image.tmdb.org/t/p/original"`
  - `Original(filePath)`, which prepends `/` only when it is missing
  - `IsTmdbImage(url)`, which checks for the prefix `https://image.tmdb.org/`

  Add tests: `/abc.jpg` gives `…/original/abc.jpg` with no double slash; a path with no slash is fixed; a MAL CDN URL is not a TMDB image.
- [x] 4.2 Add `Services/Tmdb/ITmdbClient.cs`, `TmdbClient.cs` and wire DTOs (design D4):
  - `GetTvImagesAsync`, `GetSeasonImagesAsync` and `GetMovieImagesAsync`, each requesting `…/images?include_image_language=ja,en,null&api_key={key}`
  - the response DTO declares only `posters`/`backdrops` with `file_path`, `iso_639_1`, `width` and `height`
  - after parsing, drop any language that is not `ja`, `en` or null
  - return `TmdbImagesResult` (`Images(posters, backdrops)` | `NotFound` | `Failed`)
  - on 429, wait `Retry-After` (at most 10 s, 2 s by default) and retry once
  - on 401, log an error that never includes the key or the request URI
  - no request at all when `!TmdbOptions.IsConfigured`
- [x] 4.3 Add `TmdbClientTests`, using a fake handler that records requests:
  - the exact path and query for each of the three endpoints
  - a `logos` array is ignored
  - an `fr` image is dropped
  - a season response with no `backdrops` key parses to empty backdrops
  - 404 gives `NotFound`
  - one 429 followed by a 200 gives `Images` after two requests
  - two 429s give `Failed`
  - 401, 500 and a timeout each give `Failed`
  - no key sends no request

## 5. TMDB artwork service

- [x] 5.1 Add `Services/Tmdb/TmdbAnimeScopes.cs`, a pure `For(AnimeIdMapping?)` that returns the Series, Season and Movie keys (design D7). Test these cases:
  - tv plus season 3 gives Series and Season 3
  - tv with no season gives Series only
  - season 0 gives Series only
  - one movie id gives a Movie scope
  - two movie ids give one Movie scope with both ids
  - IMDb only, or null, gives nothing
- [x] 5.2 Add `Services/Tmdb/TmdbFranchiseKeys.cs`, a pure `For(mainLine, allMembers, mappingsById)` that returns ordered TV, season and movie keys (design D8). Test these cases:
  - the Demon Slayer shape: one TV id, seasons 1–5, and the Mugen Train movie as an extra
  - the Naruto shape: two main-line TV ids
  - a spin-off extra with its own TV id, which is excluded
  - an extra mapped to a main TV id's numbered season, which is included
  - a movie extra, which is included
  - ordering follows main-line order and then the extras
- [x] 5.3 Add `Services/Tmdb/TmdbPictureDtos.cs`: `TmdbPictureDto`, `TmdbLanguageGroupDto`, `TmdbScopeDto`, `AnimeTmdbPicturesDto(HasMapping, Scopes)` and `SeriesTmdbPicturesDto(HasMapping, Languages, PendingCount)` (design D11).
- [x] 5.4 Add `Services/Tmdb/ITmdbArtworkService` and `TmdbArtworkService`, read side only, making no network calls:
  - `GetAnimePicturesAsync(animeId)` returns null unless the anime is in my list
  - `GetSeriesPicturesAsync(seriesId, mainLine, allMembers)`
  - `IsAnimeFetchDueAsync(animeId)`
  - `GetAnimeOptionUrlsAsync(animeId)`
  - `GetSeriesOptionUrlsAsync(seriesId)`

  Build the groups in the fixed orders (Series → Season → Movie; none → ja → en; posters before backdrops, each by `Position`). Build every URL with `TmdbImageUrl.Original`. Deduplicate by file path, first occurrence winning. Omit empty groups and scopes. A set is due when it has no set row or `FetchedAt` is more than 30 days old. `PendingCount` is the number of due keys, or 0 when `!IsConfigured`.
- [x] 5.5 Add the fetch side (design D10):
  - `RefreshAnimeAsync(animeId, force, ct)`: a no-op when the key is missing or the anime is not in my list. It fetches the anime's due keys, or all of them when forced.
  - `RefreshSeriesAsync(seriesId, budget, force, ct)`: fetches due keys in key order up to `budget` and returns how many remain due.

  Each key is fetched under `RefreshGate.LockAsync("tmdb:tv:{id}" / "tmdb:season:{id}:{n}" / "tmdb:movie:{id}")`, and dueness is re-checked inside the lock. The outcomes are:
  - `Images`: replace the set's image rows wholesale and set `FetchedAt = now`, in one save
  - `NotFound`: store an empty set with `FetchedAt = now`
  - `Failed`: leave the set untouched and log

  Register the service as scoped.
- [x] 5.6 Add `TmdbArtworkServiceTests` (InMemory DB and a fake `ITmdbClient`):
  - two anime sharing a TV id cause one client call
  - a set 29 days old is not refetched, and one 31 days old is
  - `force` refetches a fresh set
  - `NotFound` stores an empty set that is not re-requested
  - `Failed` keeps the previous images
  - an anime not in my list, or a missing key, makes no calls
  - a series budget of 20 against 45 due keys makes 20 calls and returns 25
  - concurrent `RefreshAnimeAsync` calls for anime sharing a key produce one call
  - the grouping, ordering and deduplication rules
  - `HasMapping` is false with no mapping row

## 6. Picture option sets and validation

- [x] 6.1 In `Services/Artwork/AnimePicture.cs`, skip the `SelectedPictureUrl` upsert when `TmdbImageUrl.IsTmdbImage` is true. A TMDB choice then never lands in the MyAnimeList option list (design D13). In `SeriesPicturePool` and in `SeriesService.ProjectAsync`'s "append the current selection" step, apply the same guard. Update the doc comments and extend `AnimePictureTests` and `SeriesPicturePoolTests`.
- [x] 6.2 Inject `ITmdbArtworkService` into `ArtworkSelectionService`:
  - `ValidateAnimePictureAsync` accepts MAL options ∪ `GetAnimeOptionUrlsAsync` ∪ the current choice
  - `ValidateSeriesPictureAsync` accepts the MAL pool ∪ `GetSeriesOptionUrlsAsync` ∪ the current choice
  - the my-list check is unchanged

  Extend `ArtworkSelectionServiceTests`:
  - an anime's own cached Season poster is accepted, and `SelectedPictureUrl` equals its full `original` URL
  - a TMDB URL from another show's set is rejected
  - an anime not in my list is rejected with a TMDB URL
  - a series accepts a cached movie image from an extra
  - re-picking the current TMDB choice after its set has dropped it is accepted

## 7. Anime detail backend

- [x] 7.1 In `Services/Detail/AnimeDetailDto.cs`, add `List<string> ImdbIds`, `AnimeTmdbPicturesDto? Tmdb` and `bool TmdbFetchPending`, each with a comment in the file's style. In `AnimeDetailService`, fill them:
  - `ImdbIds` comes from the mapping row for every anime
  - `Tmdb` is set only for an anime in my list
  - `TmdbFetchPending` is true only when the key is configured, the anime is in my list, and at least one key is due
- [x] 7.2 In `Controllers/AnimeDetailController.cs`, add `POST api/anime/{animeId:int}/tmdb-pictures/refresh`:
  - it returns 404 when the anime isn't cached
  - it calls `RefreshAnimeAsync(force: false)`
  - it returns `{ tmdb, tmdbFetchPending }`, which is `{ null, false }` without fetching when the anime is not in my list

  Add a summary in the style of the MAL `RefreshPictures` action.
- [x] 7.3 In `Controllers/MetadataRefreshController.RefreshOne`, add a third failure-isolated step after the AniList one: `await tmdbArtwork.RefreshAnimeAsync(animeId, force: true, ct)` inside `try/catch`, logging a warning (design D15). Update the action's summary. `MetadataRefreshService` stays untouched.
- [x] 7.4 Add tests:
  - `AnimeDetailServiceTests`: an anime in my list with a due Season set is flagged; one with fresh sets carries images and no flag; an anime not in my list carries IMDb ids but no TMDB and no flag; with no key there is no flag
  - the new endpoint's three behaviours
  - `RefreshOne` still returns 204 when the TMDB step throws
  - `MetadataRefreshServiceTests`' fakes confirm that the tiered and single refresh paths never touch `ITmdbClient`

## 8. Series backend

- [x] 8.1 In `Services/Series/SeriesDto.cs`, add `List<string> ImdbIds` (the root's) and `SeriesTmdbPicturesDto Tmdb`, with comments. In `SeriesService.ProjectAsync`, fill both fields: `Tmdb` from `ITmdbArtworkService.GetSeriesPicturesAsync(seriesId)`, which loads the members' mapping rows in one `IN` query and computes `TmdbFranchiseKeys` itself, and `ImdbIds` from the root's own mapping row.
- [x] 8.2 In `Controllers/SeriesController.cs`, add `POST api/series/by-anime/{animeId:int}/tmdb-pictures/refresh`. It returns 404 when the anime belongs to no series. It calls `RefreshSeriesAsync(seriesId, budget: 20, force: false)` and returns the series' `SeriesTmdbPicturesDto` with `PendingCount`. Name the budget constant (`TmdbArtworkService.SeriesFetchBudget = 20`).
- [x] 8.3 Add tests:
  - projection: languages mix series, season and movie images; there is no Season/Movie sub-grouping; posters come before backdrops; the root's IMDb ids are used; `PendingCount` counts due keys
  - endpoint: the budget and remaining count; 404 with no series

## 9. Device transfer

- [x] 9.1 In `Services/Transfer/TransferImportRunner.cs`'s picture pre-check, route by host when a choice is refused (design D14):
  - `TmdbImageUrl.IsTmdbImage(url)` → `RefreshAnimeAsync(animeId, force: true)` for an anime, or `RefreshSeriesAsync(seriesId, int.MaxValue, force: true)` for a series
  - otherwise → the existing MAL `RefreshOneAsync(evenIfFetched: true)` calls

  Keep the progress accounting (`AddToTotal`/`ReportProgress`) consistent for both routes.
- [x] 9.2 Add tests:
  - a TMDB choice refused at first is accepted after the forced TMDB refresh, and no MAL call is made for it
  - with no key, a TMDB choice is reported and not stored
  - the MAL route is unchanged for a MAL URL

## 10. Frontend

- [x] 10.1 In `frontend/src/api/types.ts`, add:
  - `TmdbPictureDto`, `TmdbLanguageGroupDto`, `TmdbScopeDto`, `AnimeTmdbPicturesDto` and `SeriesTmdbPicturesDto`. The last two carry `configured` (whether a key is set), which the backend gained in this section: see 10.5
  - `imdbIds`, `tmdb` and `tmdbFetchPending` on `AnimeDetailDto`
  - `imdbIds` and `tmdb` on `SeriesDto`

  In `frontend/src/api/client.ts`, add `refreshAnimeTmdbPictures(animeId)` and `refreshSeriesTmdbPictures(animeId)` through the existing `fetchJson` POST path, so the cross-site guard header is sent.
- [x] 10.2 In `frontend/src/utils/anime.ts`, add:
  - `isTmdbImageUrl`
  - `imdbLinks(ids)`, which returns `{ href, label }` with the label `IMDb`, or `IMDb 1`, `IMDb 2`… when there are several
  - `TMDB_LANGUAGE_LABELS` (`none` → "No language", `ja` → "Japanese", `en` → "English")

  Guard the MAL option builder `animePictureOptions` in `AnimeDetailPage` so it never includes a TMDB `selectedPictureUrl` (design D13). The series page has no client-side builder: its `pictureOptions` come from the server, already guarded by 6.1.
- [x] 10.3 Rework `frontend/src/components/PicturePickerOverlay.tsx` and its `.css` to take `sections`, `openGroups` (`Set<string>`) and `onToggleGroup(key)`, plus optional `notes` and `showTmdbAttribution` (design D12):
  - section headings with dividers; scope headings are plain labels
  - each group has a toggle heading (`<button aria-expanded>`) showing its name and count, e.g. "Japanese · 14", styled after the series page's collapsible More group headings
  - an open group is its own wrapped row at the shared `--picker-option-h`
  - a closed group renders **no `<img>` at all**, never a CSS-hidden one
  - `width`/`height` attributes when known, plus `loading="lazy"` and `decoding="async"`
  - `current` is marked in any section; a leading "Current picture" group appears when it is in none
  - the notes list, and TMDB's logo and notice (`TmdbAttribution`, compact; see section 13 for the wording, which replaced the first draft "Images from TMDB. This product uses the TMDB API but is not endorsed or certified by TMDB.")

  Keep every existing rule: whole at own proportions, common height, the wide-option clamp, and the clear control.
- [x] 10.4 Add a helper beside the overlay, `defaultOpenGroups(sections, current)`. It returns `mal` plus the key of the group holding `current` (or `current` itself when the picture is in no section). Each page seeds its `openGroups` state from it the first time the picker opens. It lives in `components/picturePickerSections.ts` with the section types and builders, and the seed/keep/reset-on-id-change state is the small hook `hooks/usePickerOpenGroups.ts` that both pages call.
- [x] 10.5 In `frontend/src/pages/AnimeDetailPage.tsx`:
  - build the picker sections: "MyAnimeList", then "TMDB · Series" / "TMDB · Season {n}" / "TMDB · Movie", each with No language / Japanese / English groups
  - own the picker's `openGroups` in plain `useState`, seeded once through `defaultOpenGroups` and kept across closing and reopening the picker; a new page visit starts fresh
  - show the Choose-picture control when the count of distinct URLs across sections and `selectedPictureUrl` is above 1
  - add a TMDB follow-up effect on `tmdbFetchPending`, ref-guarded per anime id like the MAL backfill, that merges only `tmdb`/`tmdbFetchPending`
  - show a pending note and a no-match note. `tmdb` is non-null for every my-list anime, with or without a key (cached images outlive the key), so "key configured" is read from `tmdb.configured`, added to both TMDB DTOs for this. The pending note follows the in-flight request, not the flag, so a failed fetch doesn't show "Fetching…" forever
  - render the IMDb link(s) after SeriesGraph in the link-button row
- [x] 10.6 In `frontend/src/pages/SeriesPage.tsx`:
  - build the sections: "MyAnimeList", then "TMDB" with the language groups
  - own `openGroups` the same way as the detail page
  - count the control across sections
  - add a TMDB follow-up effect on `tmdb.pendingCount > 0`, once per series visit, that merges only `tmdb`
  - change the existing MAL backfill merge to keep the current `tmdb` fields (design D12)
  - show the pending and no-match notes beside the existing MAL note
  - render the root's IMDb link(s) after SeriesGraph in `series-page__links`
- [x] 10.7 Build and lint with Node 22: `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` and `npm run lint` in `frontend/`.

## 11. Documentation

- [x] 11.1 Update `CODE_GUIDE.md`:
  - §1: add Fribb mapping and TMDB v3 subsections (endpoints, `include_image_language`, key hygiene, URL builder)
  - §2: add the two new endpoints
  - §3: add `Services/IdMapping/` and `Services/Tmdb/` sections; the `Services/Artwork/` notes on TMDB options and validation; the `Models/`/`Data/` table list
  - §5: add a background-jobs table note that the airing tick also runs the weekly mapping step, and a gotcha about `original` URLs and `SelectedPictureUrl` (design D9)
- [x] 11.2 Update `README.md` and `SETTINGS.md`: how to get and set the optional `TMDB_API_KEY`, what it enables (TMDB pictures in the picker; IMDb links work without it), and the TMDB attribution and non-commercial-use note.

## 12. Verification

- [x] 12.1 Run `dotnet build` and `dotnet test` in `backend/` and confirm the whole suite passes. (1843 passed, 0 failed.)
- [x] 12.2 Start the backend natively with a real `TMDB_API_KEY`. Confirm that the first tick downloads the mapping once (about 8.4k `AnimeIdMappings` rows, `LastSyncedAt` stamped), and that a restart makes no second download. (Verified on a scratch Postgres with the app run in `Production`: the first tick synced 8,377 rows and stamped `LastSyncedAt`, and a restart made no second download. The mapping sync needs no TMDB key, so a placeholder key was set.)
- [x] 12.3 While TMDB fetches run, capture the backend log and grep it for the key. It must not appear. If it does, raise the `System.Net.Http.HttpClient.ITmdbClient` log category to Warning (design D4) and re-check. (Verified with a placeholder key that TMDB rejects with 401: two requests reached `api.themoviedb.org`, and the key appeared 0 times in the whole log, the `IHttpClientFactory` lines reading `…/images?*`. The redaction happens on the request side, before any response, so a valid key logs the same. No fallback was needed.)
- [x] 12.4 On a my-list season (e.g. Attack on Titan Season 3), check the picker:
  - it shows MyAnimeList, TMDB · Series and TMDB · Season 3, each split by language, with the attribution line
  - with the browser's Network tab open, confirm three things: the picker opens with only MyAnimeList (and the current picture's group) open; no TMDB image downloads until a group is opened; opening one group downloads only that group's images, with no `/api/` or `api.themoviedb.org` request
  - a group opened, then the picker closed and reopened, is still open
  - a TMDB backdrop can be picked, and it shows whole on the detail page, Home and My List
  - the IMDb link opens the title

  Verified in headless Chrome against a scratch DB whose TMDB cache was seeded by SQL, with the two image hosts intercepted (synthetic images at the seeded sizes); no real TMDB images were used. The href was checked, not IMDb's own page.
- [x] 12.5 On the Naruto series page, check that the TMDB section is grouped by language only, and holds both shows' series images and the movie images. Confirm that the pending note clears across visits and that the root's IMDb link is shown.
  - Verified so far on a Naruto-shaped seeded series (headless Chrome, scratch DB): the TMDB section is grouped by language only and holds both shows' series images and the movie's, the root's IMDb link shows, and nothing is fetched when nothing is due. On an AoT-shaped series with one due set, the picker says "Fetching…" while the follow-up runs and then that a later visit continues, with exactly one POST per visit.
  - **Real `TMDB_API_KEY` check:** done. The user confirmed it at archive time (2026-09-24), so the earlier "no fetch has yet succeeded end to end" caveat no longer applies.
- [x] 12.6 Check an anime in my list with no mapping: MyAnimeList only, plus the "no match" note. Blank the key and restart: no TMDB request is made, IMDb links still work, and cached TMDB images are still offered. (Verified on the scratch stack. Unmapped anime: MyAnimeList only plus "TMDB has no match for this anime." With no key: no note at all, 0 TMDB requests in the backend log across the whole browsing run, IMDb links still there, cached images still offered and still choosable, and no follow-up request from any page.)
- [x] 12.7 Walk the brief's Definition of Done:
  - the weekly mapping sync runs on the existing tick and populates MAL → {TV id, movie ids, IMDb id(s)}, plus the season number
  - images are posters and backdrops only, `ja`/`en`/none, `original`, cached by TMDB id
  - the series page aggregates across the franchise, grouped by language only
  - the detail page groups by scope and language
  - IMDb links appear on both pages

  Then grep the diff to confirm that no `anidb_type`, `type` column, `anilist_id`, `logos` handling or episode-image code was added.

  Verified. Every grep hit is a negative case: tests that feed real Fribb entries carrying `type`, `anidb_id` and `anilist_id`, or a `logos` array, to prove they are discarded, plus comments saying so. There is no `type` property on the new models, no episode-still or `/configuration` code, and the migration only creates and drops the eight new tables.

## 13. Attribution per TMDB's current terms

Added after apply, once TMDB's API terms of use were re-read (2026-09-24): they require TMDB's logo and a prescribed notice, and the first draft had text only, in an older wording. The unenforced 6-month cache cap is not part of this section (design D18).

- [x] 13.1 Add `frontend/src/components/TmdbAttribution.tsx` and its `.css`: TMDB's logo (`frontend/assets/TMDB_Logo.svg`, the official "Alt short (blue)" SVG, unaltered) linking to `https://www.themoviedb.org/` and opening in a new tab, beside the notice word for word: "This application uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB." A `compact` form is used by the picker.
- [x] 13.2 In `PicturePickerOverlay.tsx`, render `<TmdbAttribution compact />` in place of the hard-coded old line when `showTmdbAttribution` is set, and remove that line's CSS.
- [x] 13.3 In `SettingsPage.tsx`, add a **Credits** group after Account. It holds only `<TmdbAttribution />` and is always shown, key or no key.
- [x] 13.4 Update the wording and describe the Credits group in `README.md`, `SETTINGS.md` and `CODE_GUIDE.md`. In the change's specs, rewrite the `tmdb-artwork` attribution requirement and add a `settings-page` delta that puts Credits after Account.
- [x] 13.5 Build and lint with Node 22, then check in a browser, in both colour schemes: the picker shows the logo and notice only when it offers TMDB images, the Settings page always shows them, and the logo link opens themoviedb.org in a new tab. (Verified in headless Chrome on a scratch stack, dark and light, and at phone width: 29 checks, plus a throwaway `docker build` of the frontend. The logo link's attributes were checked, not a click.)

## 14. Custom mapping overrides

Added after apply, once a hand-inserted mapping row proved to be wiped by the next weekly refresh (design D19).

- [x] 14.1 In `AnimeIdMappingParser`, extract the per-entry conversion as `ToMapping(FribbEntry)` and have `ParseAsync` use it, with no change in behaviour.
- [x] 14.2 Add `Services/IdMapping/CustomIdMappings.cs`: `CustomIdMappingEntry` (a `FribbEntry` with `override` and `note`), `CustomIdMapping`, `ICustomIdMappings` and its implementation. It reads a JSON array tolerantly (comments and trailing commas), skips and logs an invalid or duplicate entry, keeps the last good entries when the file cannot be parsed, treats a missing file as no entries, and re-reads when the file's timestamp or length changes, checked at most every two seconds. It finds `custom/id-mapping.json` in the working directory and the two folders above, unless `IdMapping:CustomFile` says otherwise.
- [x] 14.3 Add the pure `AnimeIdMappingMerge`: `Apply(synced, custom)` with the per-group precedence of D19, and `IsUnnecessary(synced, custom)`. Test it.
- [x] 14.4 Add `IAnimeIdMappingResolver` and `AnimeIdMappingResolver` (`FindAsync`, `FindManyAsync`), and use it in place of the four direct reads: `AnimeDetailService`, `SeriesService`, and `TmdbArtworkService.FindMappingAsync` and `LoadFranchiseAsync`. Register both services in `Program.cs`, and read the file once at start-up so its log line and any warnings appear at boot. Update the test helpers that build these services.
- [x] 14.5 In `AnimeIdMappingSyncService`, after a successful refresh, log every custom entry that would now change nothing, naming the MAL id, its note and the file.
- [x] 14.6 Add `backend/custom/id-mapping.json` (False Memory (2026) as a default entry; Fate/Zero season 2, Fate/stay night [Unlimited Blade Works] season 2 and Tokyo Ghoul:re 2nd Season as overrides, each with a `note`) and `backend/custom/README.md` explaining the format, and copy the folder into the backend image in the `Dockerfile`. (It was first a read-only bind mount of a root-level `custom/` folder; see 14.9.)
- [x] 14.7 Add tests: the merge rules; the file (valid, missing, malformed, a bad entry, duplicates, reload on change, keeping the last good entries); the resolver over EF InMemory; the three services using a custom-only mapping; the sync logging an unnecessary entry and leaving the file alone.
- [x] 14.8 Update `README.md`, `CODE_GUIDE.md` and the specs. Run `dotnet build` and `dotnet test`, then check on a scratch stack: a custom-only anime shows its IMDb link and TMDB groups, an override changes the season shown, an edit applies live, a typo keeps the last good entries, and a refresh removes nothing. Then confirm the Docker path with a throwaway backend image (14.9 replaced the mount this first checked). (Verified 2026-09-24: on a scratch stack the backend found the repository's file from its project folder with no configuration; a custom-only anime showed its IMDb link and TMDB groups with no synced row; an override changed Season 1 to Season 2; deleting an entry applied within seconds; a half-typed file kept the last good entries and named the problem; a due refresh removed nothing and reported only the one entry the source already covers. The bind-mount version of the Docker path was checked from a temporary folder, which is not where the project lives, and failed on the real location; see 14.9.)
- [x] 14.9 Correct the Docker path after the first real run. The first build bind-mounted a root-level `custom/` folder. On the developer's Mac, where the project is under `~/Documents`, macOS refused Docker Desktop access to it, so `docker compose up -d --build` stopped with "error while creating mount source path … operation not permitted", the backend and frontend containers were left uncreated-and-stopped, and the whole app was down. The mount had only been tested from a temporary folder. Move the folder to `backend/custom/`, Docker's build context, copy it with `COPY custom/ /app/custom/` in the `Dockerfile`, and drop the compose mount. A named build context (`additional_contexts`) over an empty fallback stage was tried in between and dropped: a rebuild reused a cached, empty copy step and built an image without the file. Update the README, `backend/custom/README.md`, the `deployment` spec, design D19, the proposal and `CODE_GUIDE.md`. (Verified 2026-09-24: a fresh image build holds the file with the repository's checksum; an edit reaches the image and a revert restores the original, that one from cache; `docker compose up -d --build` on the real stack started the backend and the frontend, and the backend, running as its non-root user, logged `Custom id mappings loaded from /app/custom/id-mapping.json: 4 entries (1 fill, 3 override)`.)

## 15. Cache purge per TMDB's 6-month limit

Added after apply, to meet the one obligation section 13 left open: TMDB's terms forbid caching its information for more than 6 months (design D18, D20).

- [x] 15.1 Add `Services/Tmdb/TmdbCachePurgeService.cs`: `ITmdbCachePurgeService.PurgeExpiredAsync` deletes every set of the three stores whose `FetchedAt` is more than `MaxAge` (150 days) old, images with it, returns how many sets it removed, and logs once, only when it removed something. Register it scoped in `Program.cs`.
- [x] 15.2 In `EpisodeScheduleRefreshBackgroundService`, run it as a step of each iteration, after the mapping sync and before the airing work, in a scope of its own, with the sync's failure handling: log and carry on, but let a cancellation through. Update the class's and the iteration's doc comments.
- [x] 15.3 Add tests. The service: each of the three stores, 151 against 149 days, a set that is stale but not expired, an empty set, sets of different shows sharing a file path, the log line and its silence, a chosen anime and series picture and the mapping left untouched, a removed set becoming due again, and `MaxAge` above the 30-day refresh and two weeks under 6 months. The tick: the step's order and scope, and that neither it nor the sync can stop the other or the airing work. Selection: a TMDB choice can be chosen again after its sets are purged.
- [x] 15.4 Update the `tmdb-artwork` spec (a new requirement, and the cached-images-outlive-the-key sentence now says "for as long as the cache keeps them"), the design (D18 closed, D20, risks), the proposal, `README.md`, `SETTINGS.md` and `CODE_GUIDE.md`.
- [x] 15.5 Run `dotnet build` and `dotnet test`, then check on a scratch stack that a tick deletes only the sets past 150 days with their images, and that a TMDB picture chosen from a deleted set still shows on My List, the profile, the home page and the detail page, and is offered as the current picture. (Verified 2026-09-24: 1919 backend tests pass. On a scratch Postgres, with a series, a season and a movie set aged 151 days (15 images), one season set at 149 days and two fresh ones, the first tick after a restart logged "Removed 3 cached TMDB image sets (15 images) last fetched more than 150 days ago". The 149-day and the fresh sets kept their images, no image row was left without its set, and the 8,377 mapping rows were unchanged. Four anime picks and two series picks, made from the deleted sets and from a kept one, were unchanged in the database, and headless Chrome showed identical pictures on Home, My List, Profile, the series browser, a series page and a detail page before and after. The picker of a show whose sets were deleted lists no TMDB section and offers the pick as "Current picture", marked current, while one with a kept set still lists it. With a placeholder key the deleted sets read as due again (`tmdbFetchPending` true, a non-zero pending count), and a later tick that found nothing old logged nothing. The key appeared in no log line.)
