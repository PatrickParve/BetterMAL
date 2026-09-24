## Context

**Where pictures come from today.** Every picture the app can show comes from MyAnimeList:
- `MalPictureUrl` is MAL's main picture.
- `PictureUrls` holds MAL's `pictures` set, fetched only for anime in my list (`artwork-selection`).
- `SelectedPictureUrl` is the choice. `PictureUrl` is the displayed value derived from these.

The only place the whole set is visible is the **Choose picture** overlay (`PicturePickerOverlay`). It appears on the detail page for an anime in my list, and on the series page for any series. The series picker's pool is the main line's MAL artwork (`SeriesPicturePool`). Choices are validated against the option set (`ArtworkSelectionService`) and carried between devices by device-transfer, which re-checks a refused choice after re-fetching MAL sets (`TransferImportRunner`).

**What a "series" is.** It is the story component of the relation graph (`SeriesGraphBuilder`): a main line (the sequel/prequel chain of eligible entries) plus extras (movies, OVAs, specials, spin-offs, folded versions, neighbour tellings). The More section also shows related entries, which are not members. So the brief's assumption that the series page aggregates a full franchise holds. Its phrase "the franchise's TV id" does not. Measured against this database's 279 stored series and the current mapping file:
- 216 series span exactly one TMDB TV id, 13 span none, and **50 span two or more**. That includes Naruto (4), Dragon Ball (7), Fate/Zero (9) and the Gundam series rooted at 0083 (36 TV ids, 134 members).
- 15 series have more than one TV id on the main line alone.

**The mapping file** (`anime-list-mini.json`, 5.8 MB, 39,304 entries, 30,781 with a MAL id, no duplicate MAL ids today). Its actual shape differs from the brief in three ways:

| Field | Brief assumed | Actual |
|---|---|---|
| `themoviedb_id` | `.tv` / `.movie[]` | Object with `tv` **or** `movie` (never both today). `movie` is a list, usually of one, rarely 2–3 (10 entries). |
| `imdb_id` | single string | **A list**, usually of one. 26 entries have 2–7 ids. Some lists contain `""`. |
| season number | not mentioned | `season.tmdb`, present on 6,852 of 6,961 TV-mapped entries. **0** (TMDB "Specials") on 1,474 of them. |

Coverage against this database's 623 list entries:
- 458 map to a TMDB TV id and 57 to movies.
- **108 (17%) have no TMDB id**, 106 because the file has no TMDB id for them and 2 because they are absent from the file.
- 110 have no IMDb id.
- 35 TV-mapped entries sit in TMDB season 0, and 11 have no TMDB season at all (whole-show entries such as Naruto).

**TMDB groups seasons differently from MAL.** Re:Zero's MAL seasons 2, 2-part-2 and 3 all map to TMDB season 1 (with episode offsets). Oshi no Ko's second season maps to TMDB season 1. The Season scope therefore shows TMDB's season art, which is not always MAL's.

**Confirmed by the user before this design:**
1. TMDB images are **pickable** in the existing picker. They are not a separate gallery.
2. The series page **includes season posters**.

## Goals / Non-Goals

**Goals:**
- A weekly, restart-proof mapping of MAL id → TMDB TV id, TMDB season, TMDB movie ids and IMDb ids, synced on the existing tick with no new scheduler.
- TMDB posters and backdrops (`ja`/`en`/none) cached by TMDB identity in three keyed stores and shared across every MAL entry that maps to them. Stored as file path, language, kind and dimensions. URLs built on read.
- Pickers that show MAL and TMDB in visibly separate sections: scope × language on an anime, language only on a series.
- The series page aggregates series, season and movie images across every TMDB id its franchise touches.
- IMDb links on both pages.
- Page reads never wait on TMDB. The app is fully functional with no TMDB key.

**Non-Goals:**
- The AniList `idMal` flow and its episode-count sync (untouched).
- Storing `anidb_type`/`type`, `anilist_id`, `episode_offset`, TheTVDB ids or the raw file.
- Logos, episode stills, `GET /configuration`, image sizes other than `original`.
- A Settings-page job row or manual trigger for the mapping sync (see Open Questions).
- Prefetching TMDB sets in the background, or pruning cached sets no mapping references any more.
- Changing how a displayed picture is drawn. `artwork-presentation` already draws wide art whole everywhere, which covers a chosen TMDB backdrop.

## Decisions

### D1. Keep the TMDB season number. Normalise IMDb to a validated list.

The kept fields are `tv`, `season.tmdb` (only alongside `tv`), `movie[]` and `imdb_id[]`. The brief's field list omits the season, but `GET /tv/{id}/season/{n}/images` is impossible without it, and the file already carries it per entry. Nothing else is read into the model: the DTO declares only these properties, and System.Text.Json skips the rest.

IMDb ids are kept only when they match `^tt\d+$`, which drops the `""` entries and anything malformed. They are stored as the full ordered list (`text[]`) rather than "the first", because the file gives no basis for picking one. For example, MAL 1441's two ids are for different releases. The rare multi-id case gets numbered links (D16).

A row is stored only for a MAL id with at least one kept value (about 8.4k rows). The absence of a row means "no mapping".

`season.tmdb` is stored as given, including 0. The skip-season-0 decision is made in one place at read time (D7), so the stored mapping stays a faithful copy of the source.

*Alternative rejected:* storing a row for all 30k MAL ids with nulls. It adds 22k rows of nothing, and "no row" is already unambiguous.

### D2. The mapping sync is a step in the existing hourly airing tick, in its own scope

The brief asks to reuse the episode-count job's elapsed-time mechanism "rather than adding a second scheduler if avoidable". That job is `EpisodeScheduleRefreshBackgroundService`: an hourly tick that runs immediately on start and compares against a stored timestamp. The mapping step is added at the **start** of that loop iteration:

- It runs in **its own DI scope, and so its own `DbContext`**. A failed mapping transaction then can't leave tracked entities behind for the airing work's `SaveChangesAsync` in the tick's shared scope.
- `IAnimeIdMappingSyncService.SyncIfDueAsync(ct)` never throws except on cancellation. It records its own outcome and logs, so neither step can stop the other (spec: "Airing work is unaffected").
- It runs first, so a slow AniList backfill (once ever, potentially tens of minutes) can't delay the weekly sync. The mapping step itself takes a few seconds: one 5.8 MB download plus one diff-write.
- Its bookkeeping is a new singleton row, `AnimeIdMappingSyncState` (`LastSyncedAt`, `LastAttemptAt`), not new columns on `AiringRefreshState`. That row is documented as the airing pipeline's own bookkeeping.

The step is due when `LastSyncedAt` is null or older than 7 days. After a failed attempt (`LastAttemptAt > LastSyncedAt`), it is not retried within 6 hours of `LastAttemptAt`. Without that backoff, a persistent upstream format change would re-download 5.8 MB every hour (about 140 MB a day).

The hosted service keeps its name. Its doc comment gains one line naming the extra step. The cache purge (D20) later became a second such step, with the same shape.

*Alternative rejected:* a new `BackgroundService` shaped like `ReconciliationBackgroundService` (delay until due). It is cleaner in isolation, and it is how every other job here is built. But the brief explicitly prefers reuse, and the coupling is contained by the separate scope and the non-throwing step. Splitting it out later is a small move. See Open Questions.

### D3. Sync write: parse fully, first MAL id wins, guard, then one diff transaction

The steps are:
1. Download through a plain named `HttpClient` (`"anime-id-mapping"`, 2-minute timeout).
2. Parse the stream with `JsonSerializer.DeserializeAsyncEnumerable<FribbEntry>`. A `HashSet<int>` skips later duplicates of a MAL id (first match wins, per the brief; zero duplicates exist today).
3. Build the complete new mapping in memory **before touching the database**.
4. Apply the parse tolerance. `imdb_id` given as a bare string is accepted as a one-element list, which is unambiguous. Any other unexpected shape fails the parse, and the sync keeps the last good mapping. This is deliberate: `themoviedb_id` once changed from a number to an object upstream, and guessing what a bare number means would silently corrupt the mapping.
5. Check the sanity guard. If the new mapping has fewer than half the currently stored rows, abandon the sync and record a failed attempt. This protects against a truncated or garbled download that still parses. The guard is skipped on the first sync.
6. Load the existing rows (tracked), then add, update where any value differs, and remove vanished ids. `SaveChangesAsync` does this in one transaction, so readers see the old or the new mapping, never a mix. On success, stamp `LastSyncedAt = LastAttemptAt = now`.

*Alternative rejected:* `TRUNCATE` plus a bulk insert. It is simpler, but it rewrites all 8.4k rows weekly, and it leaves a window between truncate and insert unless both are wrapped in a transaction. The diff is barely more code.

### D4. `TmdbClient`: a typed client using the v3 key, three endpoints, logos never modelled

`AddHttpClient<ITmdbClient, TmdbClient>` with base `https://api.themoviedb.org/3/` and a 15-second timeout. The requests are:
- `GetTvImagesAsync(tvId)` → `tv/{id}/images`
- `GetSeasonImagesAsync(tvId, n)` → `tv/{id}/season/{n}/images`
- `GetMovieImagesAsync(movieId)` → `movie/{id}/images`

Every request uses the query `include_image_language=ja,en,null&api_key=…`.

**Response parsing.**
- The response DTO declares only `posters` and `backdrops`, each image as `file_path`, `iso_639_1`, `width` and `height`. `logos` and the vote fields are never deserialised.
- After parsing, anything whose `iso_639_1` is not `ja`, `en` or null is dropped, as a guard. The filter should already guarantee this.
- A season response has no `backdrops`. That is handled naturally as an empty list.

**Status handling.**

| Response | Result |
|---|---|
| 200 | the set's images |
| 404 | `NotFound`: the caller stores the set as fetched and empty |
| 401 | `Failed`, logged as an error ("TMDB rejected the API key"), never including the key |
| 429 | wait `Retry-After` (capped at 10 s, default 2 s), retry once, then `Failed` |
| other non-success, timeout or network error | `Failed` |

There is no global pacer. TMDB's documented ceiling is about 50 requests per second, and this app's calls are visit-triggered, sequential, and capped at 20 per series visit.

**Key hygiene.** The v3 key has to travel in the query string. Nothing in this code logs a request URI. The one remaining exposure is `IHttpClientFactory`'s own request logging, which prints URIs. Since .NET 9 that logging redacts query strings by default, which this app (.NET 10) inherits, and `System.Net.Http.DisableUriRedaction` must stay off. Task 12.3 confirms this with a captured log. If the key shows anyway, the fallback is to raise the `System.Net.Http.HttpClient.ITmdbClient` log category to Warning.

*Alternative considered:* the v4 read-access token as a Bearer header, whose header values are redacted by default. It is rejected only because the brief specifies the v3 key.

### D5. The cache is three keyed stores, each a fetch-log row plus image rows

| Store | Set row (PK) | Image rows (PK) |
|---|---|---|
| Series | `TmdbTvImageSets (TvId)` | `TmdbTvImages (TvId, FilePath)` |
| Season | `TmdbSeasonImageSets (TvId, SeasonNumber)` | `TmdbSeasonImages (TvId, SeasonNumber, FilePath)` |
| Movie | `TmdbMovieImageSets (MovieId)` | `TmdbMovieImages (MovieId, FilePath)` |

A set row holds `FetchedAt`. Its existence is what distinguishes "fetched and empty" from "never fetched", and its age decides whether it is due. Image rows hold:
- `Kind` (`Poster`/`Backdrop`, stored as the enum's name, as `RelationGroup` is)
- `Language` (`ja`/`en`/null)
- `Width`, `Height`
- `Position` (TMDB's order within its kind, since Postgres rows have no inherent order)

Image rows cascade-delete with their set. A refetch replaces a set's image rows wholesale and restamps `FetchedAt`, in one save.

This is the codebase's existing fetch-log-plus-rows shape (`SeasonFetchLog`/`SeasonAnimeListing`, `TopAnimeFetchLog`/`TopAnimeRankingEntry`), and it gives exactly the three TMDB-keyed caches the brief asks for. The three image entities can share a plain C# base class for their common columns. The base is not mapped as an EF hierarchy.

*Alternatives rejected:*
- A `jsonb` image column per set row: three tables instead of six, but the codebase has no JSON-column mapping anywhere, and it would be the first.
- A single table with a nullable season: it breaks the brief's "three separate keys", and a nullable column can't be part of a primary key.

### D6. URLs are built on read in one function, with the slash fixed

`TmdbImageUrl.Original(filePath)` returns `"https://image.tmdb.org/t/p/original" + filePath`. TMDB file paths already begin with `/`, so the brief's literal template `…/original/{file_path}` would produce `original//abc.jpg`. The function prepends `/` only if it is missing. `TmdbImageUrl.IsTmdbImage(url)` (prefix `https://image.tmdb.org/`) is the one test for "is this a TMDB picture", used by D13 and D14.

Every DTO projection and every validation goes through this pair. A future size or CDN change is then a one-line edit plus the D9 rewrite for stored choices, and needs no re-fetch.

### D7. Anime scopes

The scopes come from the anime's mapping row:
- **Series:** `TvId`, when present.
- **Season:** `(TvId, SeasonNumber)`, when `SeasonNumber ≥ 1`. Season 0 is TMDB's "Specials" bucket: its posters are generic art for the bucket, and for a MOVIE-type entry mapped there (e.g. Naruto's films) they would say nothing about the entry. Seasonless whole-show entries get Series only.
- **Movie:** the union of every id in `MovieIds`, taken in array order.

Scopes with no cached images are omitted from the DTO. The mapping sometimes files a movie under its parent show (`tv` + season 0) rather than giving it a movie id. Such an anime simply offers its show's images under Series, which is what the source says.

### D8. The series key set: main-line shows, their seasons, and every member's movies

This is `TmdbFranchiseKeys.For(mainLine, allMembers, mappings)`, a pure, tested function:
- **TV ids:** distinct `TvId` over **main-line** members, in main-line order.
- **Seasons:** distinct `(TvId, n ≥ 1)` over **all** members whose `TvId` is in the set above, ordered by the TV id's order and then by `n`. This picks up, for example, a special MAL files as an extra but TMDB counts as a numbered season of the main show.
- **Movies:** distinct movie ids over **all** members, in main-line order first and then in the extras' display order.

Related entries are not members and never count. Each series view uses all main-line members, whichever version-slot branch is picked, because picture options don't vary by pick today.

Excluding extras' own TV ids keeps a chibi parody or spin-off show (e.g. *Attack on Titan: Junior High*, a separate TMDB show) out of the franchise's picture pool. That mirrors `SeriesPicturePool`'s main-line-only rule. Movies are the brief's explicit exception ("movie-level images for any movie entries belonging to the same franchise"), since franchise films are usually extras.

### D9. A picked TMDB image is stored as its full URL in `SelectedPictureUrl`

The cache stores no URLs (D5/D6). A *choice*, however, must render on every surface: home, My List, Season, Year, Search, Recap, the profile, the series pages and related tiles. Every one of them reads the resolved `PictureUrl` string. Storing the choice as its full `original` URL keeps that single display contract.

The cost: if the base URL or size ever changes, stored TMDB choices need one `UPDATE … SET "SelectedPictureUrl" = replace(…)` over `AnimeMetadata` and `Series`, followed by `ResolvePictureUrl`. No TMDB re-fetch is needed.

*Alternative rejected:* storing a `tmdb:/abc.jpg` token and resolving it on read. That token would flow through about 25 read paths and DTOs, the search index and the export file, far out of proportion to a hypothetical CDN change.

### D10. Visit-triggered follow-up fetch, 30-day staleness, single-flight per TMDB key

The brief's "fetch on first resolution of a mapping" is read as **first need**. A set is fetched the first time a page that offers it is opened, not eagerly after a sync. This mirrors MAL's visit-triggered picture backfill and avoids a burst of hundreds of calls for anime that may never be opened. The mapping spec states outright that a sync fetches no images.

`ITmdbArtworkService` has a read side and a fetch side. The read side makes no network calls:
- `GetAnimePicturesAsync(animeId)`
- `GetSeriesPicturesAsync(seriesId, members)`
- the option-URL lists used by D13

The fetch side is:
- `RefreshAnimeAsync(animeId, force)`: my-list anime only, all due scopes (at most about 4 calls).
- `RefreshSeriesAsync(seriesId, budget, force)`: returns the count still due.

Every fetch holds `RefreshGate` on `tmdb:tv:{id}`, `tmdb:season:{id}:{n}` or `tmdb:movie:{id}` and re-checks dueness inside the lock. So a season-2 and a season-3 page opened together fetch the shared series set once.

- **Due** means no set row, or `FetchedAt` older than 30 days.
- **Forced** means "Refresh data" (D15) and device-transfer (D14).
- **Budget** is 20 fetches per series follow-up, spent in key order (series sets, then seasons, then movies), so the most representative art arrives first.
- **No key configured:** every fetch is a no-op, and every pending flag or count reads false or 0.

**Why 30 days (flagged, per the brief).** TMDB image sets change mostly when a new season's key visuals are uploaded. A new season is a new `(tv, n)` key, which is fetched on first need regardless of age. What 30 days mainly delays is extra posters added to an existing show. "Refresh data" forces those whenever wanted.

### D11. DTOs and endpoints

**Shared shapes:**
- `TmdbPictureDto(Url, Kind, Width, Height)`
- `TmdbLanguageGroupDto(Language: "none"|"ja"|"en", Pictures)`, where a group lists posters then backdrops
- `TmdbScopeDto(Scope: "Series"|"Season"|"Movie", SeasonNumber?, Languages)`

Empty groups and scopes are never sent, and the fixed orders are Series → Season → Movie and none → ja → en. Pictures are deduplicated by file path, first occurrence winning.

**Detail:** `AnimeDetailDto` gains:
- `ImdbIds` (always sent; `[]` when there are none)
- `Tmdb` (`{ Configured, HasMapping, Scopes }`, null unless the anime is in my list). `Configured` is whether a key is set: cached images are offered with or without one, so `Tmdb` is non-null either way, and the picker's "TMDB has no match" note (D12) needs it to tell "no match" from "TMDB is off"
- `TmdbFetchPending`

`POST /api/anime/{id}/tmdb-pictures/refresh` runs `RefreshAnimeAsync(force: false)` and returns `{ tmdb, tmdbFetchPending }`. It returns 404 when the anime isn't cached, and `{ tmdb: null, tmdbFetchPending: false }` without fetching when the anime isn't in my list.

**Series:** `SeriesDto` gains:
- `ImdbIds`, the root's
- `Tmdb` (`{ Configured, HasMapping, Languages, PendingCount }`)

`POST /api/series/by-anime/{animeId}/tmdb-pictures/refresh` runs `RefreshSeriesAsync(budget: 20)` and returns the same `Tmdb` shape. It returns 404 when the anime belongs to no series.

Both are POSTs through `client.ts`'s existing wrappers, so they carry the cross-site guard header like every other mutating call.

### D12. The picker takes sections, and each page builds its own

`PicturePickerOverlay`'s `options: string[]` becomes `sections: PickerSection[]`:
- `PickerSection = { key, heading, groups: { key, heading?, options: { url, width?, height? }[] }[] }`
- `notes?: string[]`
- `showTmdbAttribution?: boolean`

The overlay:
- renders each section with a heading and a divider, and each group with a sub-heading and its own wrapped row at the shared `--picker-option-h`
- sets `width`/`height` attributes when known, so a backdrop reserves its landscape footprint before it decodes rather than the current `2/3` guess
- adds `loading="lazy" decoding="async"` to every option
- marks `current` wherever it appears, and when it appears nowhere, prepends a "Current picture" group

The overlay is the one place that knows every section, which is why that rule lives there. TMDB's logo and notice (`TmdbAttribution`, compact) render at the bottom when `showTmdbAttribution` is set.

**Collapsible groups.** Each group opens and closes: the MyAnimeList group, each TMDB language group, and the "Current picture" group.
- **Heading.** A group's heading is a toggle button (`aria-expanded`) showing its name and count, e.g. "Japanese · 14". It uses the same visual pattern as the series page's collapsible More groups.
- **Scope headings.** An anime's scope headings ("TMDB · Series" and so on) are plain labels, so any set of images is one click away.
- **Closed means nothing rendered.** A closed group renders **no `<img>` elements at all**, rather than hiding them with CSS. Browsers still download a `display: none` image, so CSS hiding would save nothing.
- **Opening makes no calls.** Every option's URL is already in the page data, so opening a group only mounts that group's images. The browser downloads them from `image.tmdb.org` (or MAL's CDN), which involves neither the backend nor the TMDB API. `loading="lazy"` still applies inside an opened group, and re-opening a group is normally served from the browser's cache.
- **Defaults.** The MyAnimeList group is open, since MAL images are small and this matches today's picker. So is the group holding the current picture, so the selection is visible and marked. Every TMDB group is closed.
- **Where the state lives.** Each page owns an `openGroups: Set<string>` in plain `useState`. It is seeded with the defaults when the picker first opens and passed to the overlay with an `onToggleGroup` callback. Closing and reopening the picker therefore keeps what was open, while a fresh page visit starts from the defaults. It is deliberately not restorable page state and not persisted: open groups are a browsing convenience, not a view setting.
- **Group keys** are stable strings: `mal`, `current`, `tmdb:series:{lang}`, `tmdb:season:{n}:{lang}` and `tmdb:movie:{lang}` on an anime, and `tmdb:{lang}` on a series.

**Detail page sections:** "MyAnimeList" (one unheaded group of MAL options), then one section per scope. The headings are "TMDB · Series", "TMDB · Season {n}" and "TMDB · Movie". Their groups are headed "No language", "Japanese" and "English".

**Series page sections:** "MyAnimeList", then "TMDB" with the three language groups.

**Notes:** "Fetching TMDB pictures — more may appear." while pending. "TMDB has no match for this anime/series." when a key is configured, `HasMapping` is false, and the item is eligible for TMDB (on my list for an anime, always for a series). The series page keeps its existing MAL "N members not yet fetched" note.

**Picker-control visibility** counts distinct URLs across all sections plus `current`.

**Follow-up wiring.** Each page fires its TMDB follow-up once per anime or series (a `useRef` guard, as the MAL backfills do) and merges **only** the `tmdb` fields. The series page's existing MAL backfill currently replaces the whole `series` with a re-read. It is changed to keep the current `tmdb` fields when it merges, so whichever follow-up finishes last can't roll the other back (spec: "neither one's result overwrites the other's").

### D13. Option sets and validation

- `AnimePicture.Options` (the MAL option set) stops upserting a `SelectedPictureUrl` that `IsTmdbImage`. A TMDB choice must not surface under the MyAnimeList heading. A MAL choice keeps its identity-authoritative upsert, so extension-variant matching is unchanged. `SeriesPicturePool`, and `SeriesService`'s "append the current selection" step, do the same. The frontend's `animePictureOptions`/`dedupePictureOptions` call gets the same guard.
- `ArtworkSelectionService.ValidateAnimePictureAsync` accepts the MAL options, **plus** `ITmdbArtworkService` option URLs for the anime's scopes (cache only), **plus** the current choice.
- `ValidateSeriesPictureAsync` accepts the MAL pool, plus the series TMDB option URLs, plus the current choice.
- The my-list check on anime choices is unchanged.

### D14. Device-transfer re-check routes by picture host

In `TransferImportRunner`'s pre-check, a refused choice is handled by host:
- **A TMDB image:** `RefreshAnimeAsync(animeId, force: true)`, or for a series `RefreshSeriesAsync(seriesId, budget: int.MaxValue, force: true)`, with no per-visit budget during an import. Then check again.
- **Anything else:** the existing MAL re-fetch.

With no key configured, the TMDB refresh is a no-op, so the choice stays refused and is reported like any other refused choice.

### D15. "Refresh data" chains a forced TMDB refetch in the controller, not the service

`MetadataRefreshController.RefreshOne` already calls the MAL refresh and then the AniList refresh, each failure-isolated. A third failure-isolated call is added: `tmdbArtwork.RefreshAnimeAsync(animeId, force: true)`. It is deliberately **not** put inside `MetadataRefreshService.RefreshOneAsync`, which the first visit to a lean row, relation resolution and announcements also call. That would put TMDB calls on background paths (spec: "Scheduled refreshes never call TMDB").

### D16. IMDb links

The detail page uses the anime's own ids and the series page the root's (`SeriesDto.SeriesId`'s mapping), matching the rule that every series external link targets the root. The links are:
- one id: **IMDb**
- several ids: **IMDb 1**, **IMDb 2** …
- no id: no link

There is no search fallback, because the brief asks for mapped-id links only, and an IMDb title search for anime titles is noisy. They sit in the existing link-button row after SeriesGraph, with the same fixed-size button rules.

### D17. Configuration

- `TmdbOptions` binds section `Tmdb` (`ApiKey`).
- `.env` → `TMDB_API_KEY`, which `docker-compose.yml` passes as `Tmdb__ApiKey: ${TMDB_API_KEY:-}`.
- `DevDotEnvOverlay.ToConfiguration` adds `SetIfNonEmpty("Tmdb:ApiKey", TMDB_API_KEY)`.
- `.env.example` documents it as optional.
- "Configured" means non-blank. There is no start-up validation of the key, so a wrong key surfaces as logged 401s (D4) and never blocks start-up.

### D18. Attribution follows TMDB's terms

TMDB's API terms of use (re-read 2026-09-24) require:
- **§3 Attribution:** the TMDB logo must identify the app's use of TMDB, be less prominent than the app's own marks, and imply no endorsement. This notice must be shown "prominently in or on" the app: "This [website, program, service, application, product] uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB."
- **§1.C Caching:** no TMDB information may be cached for longer than 6 months.
- **§2:** non-commercial use only.

**Attribution.** One component, `TmdbAttribution`, renders TMDB's official logo (the "Alt short (blue)" SVG, byte-identical to TMDB's download, kept at `frontend/assets/TMDB_Logo.svg`) and the notice with "application" chosen as the noun. It appears in two places:
- the picker's footer, in a compact form, whenever the picker offers TMDB images
- a **Credits** group at the end of the Settings page, always, since that page is the app's only about-style surface

The Credits group holds no control, so it sits after Account, which stays the last group of controls. The logo's gradient reads on both the light and the dark theme, so one file serves both. The first draft used an older wording and no logo.

**The 6-month cache cap** is met by D20. The cache holds image metadata only (file path, language, kind, width, height, position, fetched-at), never the pictures, but a set nobody opens is never refetched, so a purge deletes each set 150 days after its last fetch. This paragraph first recorded it as open, before it was built.

### D19. A hand-edited custom mapping, merged at read time

The synced mapping is a mirror of Fribb's file, and every refresh deletes the rows the file lacks (D3), so a row inserted by hand lasts at most a week. Two gaps in that source needed a durable fix:
- **Ids it lacks.** It can list a MAL id with no TMDB or IMDb id at all. False Memory (2026) is one: its entry in the project Fribb generates from (`Anime-Lists/anime-lists`, keyed by AniDB id) is an empty template.
- **Values it has wrong.** Upstream gives a season with an episode offset, which was right when TMDB kept a show as one long season. TMDB has since split some shows (Fate/Zero season 2 is listed as TMDB season 1, offset 13), and Fribb keeps the season but drops the offset.

**Decision.** A JSON file of Fribb-shaped entries, `backend/custom/id-mapping.json`, merged **when the mapping is read** and never written into the table. One resolver, `IAnimeIdMappingResolver`, serves the four places that read the mapping: the detail and series IMDb links, and `TmdbArtworkService`'s two reads.
- **Same shape, same code.** Entries reuse `FribbEntry` and one shared conversion in `AnimeIdMappingParser`, plus two fields of their own, `override` and `note`. The file is read tolerantly, since a person edits it: comments and trailing commas are accepted, and a bad entry costs only itself.
- **Precedence per group.** The TMDB ids with their season form one group and the IMDb ids another, so an IMDb-only source row can still take a TMDB id from the file. By default an entry fills a group only while the synced mapping has nothing for it, so a source that catches up wins with no edit. `override: true` wins regardless, for the corrections a fill-only file cannot make, because in those the source had something, just not the right thing.
- **Live reload.** The file's timestamp and length are checked at most every two seconds, and the entries are re-read on a change. A file that fails to parse leaves the last good entries in force, so a typo can't silently switch every override off.
- **Where it lives.** A `custom/` folder inside `backend/`, which is Docker's build context, so the image build copies it to `/app/custom` with an ordinary `COPY`. The native run finds it by looking in the working directory and the two folders above, which from the project folder is `backend/custom`. An edit applies live to a native run, and needs `docker compose up -d --build` in Docker.
- **Self-cleaning.** After each successful refresh the sync logs every entry that would now change nothing, defined as "applying it leaves the mapping as the source has it". That covers both a default entry the source has filled in and an override the source now agrees with.

**Alternatives rejected:**
- *Insert into the table.* The next refresh removes it.
- *Merge at refresh time.* It needs a marker column to tell copied values from the source's, and a new entry would wait up to a week for the next refresh unless a second trigger is added. Merging on read is simpler and always exact.
- *A table with a Settings editor.* It isn't versioned, is lost with the database, and needs UI for a handful of entries a year.
- *Automatic TMDB title search.* Namesakes give wrong pictures silently: TMDB 280564 is both a TV show and an unrelated 2007 movie.
- *A second mapping source.* Fribb's `anime-list-full.json` holds the same 39,304 entries and nothing more, and the other projects derive from the same upstream.
- *A read-only bind mount of a repository-root folder.* This was the first build. It gave live reload in the container too, but Docker Desktop on macOS may not read `~/Documents` until it is granted access, and a mount source the daemon cannot reach stops the backend container from starting at all (`mkdir /host_mnt/…/Documents: operation not permitted`). The first real `docker compose up` failed that way. An optional feature must not be able to keep the app from starting, and the file changes a few times a year, so a rebuild is a fair price.
- *A named build context for a root-level folder.* It kept the folder where it was, with an empty fallback stage for a plain `docker build`. A rebuild reused a cached, empty copy step and produced an image with no file in it, silently. An ordinary `COPY` from the build context has no such trap.

**The permanent fix is upstream.** An entry that is empty or wrong in `Anime-Lists/anime-lists` is corrected there, by a pull request that edits `anime-list-master.xml` only. Once Fribb regenerates, the custom entry reports itself unnecessary and can be deleted. That project merges most pull requests within a day.

### D20. Cached sets are deleted 150 days after their last fetch

TMDB's terms (§1.C, D18) forbid caching what it returns for more than 6 months. A set is refetched after 30 days only when its page is visited (D10), so a set nobody opens would stay for ever. The cache holds image metadata only (file path, language, kind, width, height, position, fetched-at), never the pictures, but that is still information obtained from TMDB.

**Decision.** `TmdbCachePurgeService.PurgeExpiredAsync` deletes every set whose `FetchedAt` is more than 150 days old, in all three stores, images with it. It is a step of the hourly airing tick, after the mapping sync and before the airing work, in a scope of its own with the sync's failure handling (D2). It is three small reads and, nearly always, no write. It needs no key and makes no request, so a removed key or a switched-off integration still ages its cache out. It logs only when it removed something.

- **Why 150 days.** Five months, so the purge lands inside the six the terms allow with room for the app having been switched off: nothing is deleted while it is off, and the first tick after a start catches up. Every set someone opens is refreshed at 30 days, so the purge only ever reaches sets left unopened for about four months after they went stale. A test pins the limit above the 30-day refresh and at least two weeks under 6 months.
- **No state row, no migration.** A set's age is its own `FetchedAt`.
- **A deleted set is "never fetched".** The next page that needs it fetches it like a new set (D10). The cost of a purge is one refetch for a show someone comes back to after months, and nothing for a show nobody comes back to.
- **A picked picture is not cache and stays.** It is stored on the anime or series as the address that was picked (D9): the person's own setting, not something the cache ages out. Every page renders that row, and none of them reads the cache. The picker offers a choice that no set lists as "Current picture" (D13), and it can be chosen again, since a stored choice is never re-validated away. For a show whose sets were deleted, only two things change: its picker lists no TMDB pictures until the next visit refetches them, and a device-transfer import carrying a TMDB choice has to refetch first (D14).
- **The id mapping stays.** It is Fribb's file from GitHub, not TMDB's data, and the TMDB ids in it are the keys the cache is stored under.
- **Cached images no longer outlive a removed key for ever**, only until 150 days after their fetch. The `tmdb-artwork` requirement says so.
- **Tracked deletes, not a bulk delete.** The tests run on EF's in-memory provider, which has no `ExecuteDelete`, and loading a set's images with it removes them explicitly rather than trusting the foreign key's cascade. Most ticks delete nothing. The worst case, an install unused for months, is a few thousand small rows once.

**Alternatives rejected:**
- *Delete a set when a page finds it expired.* A set nobody opens is never found, which is the case to cover.
- *Refetch instead of delete.* It needs a key, spends TMDB calls on shows nobody looks at, and is the background sweep the spec rules out.
- *Also clear picked TMDB pictures.* It would change what the person chose, to age out a URL string. A choice is their setting, not a cache.
- *A dedicated hosted service.* The reasons for D2 hold: a three-read step does not justify one more moving part.
- *A configurable age.* The terms allow no value above 6 months, so a constant with a test that pins it is safer than a setting.

## Risks / Trade-offs

- **Original-resolution images are heavy.** The brief mandates `original` for every image. Typical sizes are a TMDB original poster at about 2000×3000 (0.5–2 MB) and a backdrop at 3840×2160 (1–4 MB), against MAL's `large` at about 425×600 (roughly 60 KB).
  - A franchise picker can list 100+ TMDB options, and a chosen TMDB picture is served at `original` in every 60-px list row.
  - Mitigation: TMDB groups start closed in the picker and download nothing until opened (D12). Opened groups load lazily. Rows and cards already reveal pictures as they scroll in (`artwork-presentation`). Chosen TMDB pictures in list rows remain full size.
  - Follow-up if it bites: display smaller renditions (`w342`/`w780`) on thumbnails by rewriting the size segment in one frontend helper, keeping `original` as the stored identity. The D6/D9 single-builder design makes that a contained change.
- **Coverage gaps.** 17% of list entries have no TMDB id, and 18% have no IMDb id. Their pickers show MAL alone with a "no match" note, and no IMDb link is shown.
- **TMDB's season structure ≠ MAL's** (Re:Zero, Oshi no Ko). The Season scope is labelled with TMDB's number, so what's shown is honest even when it isn't MAL's split.
- **Upstream mapping drift or outage.** A failed parse, a truncated file or a GitHub outage keeps the last good mapping indefinitely (D3 guard plus the failure path). The cost is a stale mapping, never a wiped one.
- **Mapping corrections don't touch stored choices.** A choice made from a set the anime no longer draws from stays chosen and is shown as "Current picture" ("never re-validated away").
- **Series with dozens of TMDB ids** (the Gundam series: 36 TV ids, 9 movies, plus their seasons) take several visits to fill, bounded at 20 calls each. Their pickers hold hundreds of options, but those options sit in closed language groups, so opening the picker stays cheap.
- **Coupling to the airing tick.** Mitigated by the separate scope and the non-throwing step (D2). Worst case, a hung airing pass delays the next hourly check, but the weekly sync still runs on the next tick.
- **TMDB terms.** The API is free for non-commercial use. Each self-hosting user needs their own key. README and SETTINGS say so. TMDB's logo and prescribed notice are shown in the picker and in the Settings Credits group (D18). TMDB's terms also forbid caching its information for more than 6 months, which the purge of D20 keeps to.
- **Unreferenced cached sets** (an anime that left the list, a mapping that changed) are not pruned for being unreferenced. They are a few rows each, and they age out with every other set (D20).
- **A stopped app keeps its cache.** Nothing is deleted while the app is off, so an install stopped for months holds its cache that long, and the first tick after a start deletes what has aged out. The gap between 150 days and 6 months is the allowance for that (D20).
- **A stale override.** If the source later corrects a value *differently* from an override, the override stays in force until it is removed, and the log only says so when the source now agrees with it. Overrides are a handful of entries, each with a `note` saying why (D19).

## Migration Plan

1. There is one EF migration (`AddTmdbImageSource`): `AnimeIdMappings`, `AnimeIdMappingSyncStates` and the six TMDB tables. No existing table or column changes, and no backfill. `Database.Migrate()` applies it on start, as always.
2. The first start after deploy runs the mapping sync on the first tick (a few seconds). IMDb links appear immediately after it.
3. TMDB stays dormant until `TMDB_API_KEY` is set and the backend restarted. Sets then fill as pages are visited.
4. **Rollback:** revert the code and drop the eight new tables. Any TMDB picture already chosen stays in `SelectedPictureUrl` and keeps displaying, since it is a plain URL. The old picker would then list it only as the current option. Clearing it returns the anime or series to MAL's picture.

## Open Questions

These are decisions made in this design that the brief left open or that conflict with it. Please confirm or override them before `/opsx:apply`:

1. **Mapping sync placement (D2):** a step inside the airing tick, per the brief's "reuse… if avoidable". The alternative is a dedicated hosted service, which is how every other job is built.
2. **TMDB refresh interval (D10):** 30 days, visit-triggered, plus forced on "Refresh data".
3. **Season 0 (D7):** skipped for the Season scope, affecting 35 of your list entries. Those entries show their show's Series images only.
4. **Series TV ids (D8):** series-level and season images from the **main line's** shows only. A spin-off show among the extras is excluded, while movies are taken from all members.
5. **Series fetch budget:** 20 TMDB calls per visit.
6. **IMDb (D16):** every mapped id gets a link, numbered when there are several. The series page uses the root's ids only, with no fallback to another member. There is no search fallback when no id is mapped.
7. **Image size (Risks):** `original` everywhere, as the brief specifies, with the bandwidth cost noted above.
8. **Mapping sync visibility:** log-only, with no Settings row and no manual "sync now". A failed sync is invisible in the UI until someone reads the logs.
9. **Anime-side eligibility (tmdb-artwork):** TMDB sets are fetched for my-list anime only, because the anime picker exists only for them. A browsed anime's detail page shows its IMDb link but no TMDB images.
10. **Sanity guard (D3):** a sync that would shrink the mapping below 50% of its current size is abandoned.
