## 1. Series storage (backend schema)

- [x] 1.1 Add `backend/AnimeTracker.Api/Models/Series.cs` — `Id` (identity), `RootAnimeId`, `BuiltAt`, `IsPartial`, `IsTruncated`, `Members` navigation — with a doc comment stating the id is stable across rebuilds because a later "my top series" feature ranks by it.
- [x] 1.2 Add `backend/AnimeTracker.Api/Models/SeriesMember.cs` — `AnimeId` (PK), `SeriesId`, `IsMainLine`, `Order`, `Anime` navigation — noting that `AnimeId` as the PK is what enforces "an anime belongs to at most one series".
- [x] 1.3 Configure both entities in `Data/AnimeTrackerDbContext.cs`: `DbSet`s, `SeriesMember` keyed by `AnimeId`, cascade-delete FKs to `Series` and to `AnimeMetadata`, unique index on `Series.RootAnimeId`, index on `SeriesMember.SeriesId`.
- [x] 1.4 Generate the `AddSeries` migration through the `sdk:10.0` Docker image (the local SDK is 9.0) and confirm it only creates tables — no changes to existing ones.

## 2. Series graph builder

- [x] 2.1 Add `Services/Series/SeriesRelations.cs` (or a constant in the builder) holding the traversal set — `sequel`, `prequel`, `side_story`, `parent_story`, `summary`, `full_story`, `spin_off`, `alternative_version` — with a comment on why `alternative_setting` and `character` are excluded. Export it in a form the detail DTO/API can also expose for the frontend's link check if that route is taken in task 6.3.
- [x] 2.2 Add `Services/Series/SeriesGraphBuilder.cs` with a BFS from a seed anime id: for each node load its metadata (with `RelatedAnime`), collect neighbours from both its own relation rows and rows where `RelatedAnimeId == node` (both filtered to the traversal set), enqueue unvisited neighbours.
- [x] 2.3 Implement the fetch budget: nodes with no metadata row spend an `IMetadataRefreshService.RefreshOneAsync` call (limit 8 for a visit build, 20 for an explicit rebuild); nodes with a lean row are admitted as members without a fetch but are not expanded; a node needing a fetch that the budget can't cover is skipped and sets `IsPartial`.
- [x] 2.4 Enforce the 60-member cap, setting `IsTruncated` when the BFS stops early with a non-empty frontier.
- [x] 2.5 Classify the main line: build the `sequel`/`prequel` subgraph over the members (undirected), take the largest chain — tie-broken by the chain containing the earliest-aired member — and drop members whose `MediaType` is `special` or `music`. Everything else is an extra.
- [x] 2.6 Order the main line by `AiredFrom` ascending, nulls last, MAL id as tiebreak, and assign `Order` 0..n; order extras by media-type group (Movie, OVA, ONA, Special, Music, TV, Other) then `AiredFrom`, assigning `Order` within each group.
- [x] 2.7 Set `RootAnimeId` to the first main-line entry. Handle the degenerate cases: a component of one member returns "no series"; a component with no main-line members after the media-type filter falls back to the earliest-aired member as root and treats the largest chain as main line unfiltered (so a specials-only franchise still renders).
- [x] 2.8 Persist: find `Series` rows overlapping the computed component, keep the one with the largest overlap (preserve its `Id`, update `RootAnimeId`/`BuiltAt`/flags), delete the others, replace the member set wholesale — all in one transaction.

## 3. Series read model and API

- [x] 3.1 Add `Services/Series/SeriesDto.cs`: `SeriesDto` (seriesId, rootAnimeId, title, englishTitle, pictureUrl, status, firstYear, lastYear, builtAt, isPartial, isTruncated, scores, stats, mainLine, extras) and `SeriesEntryDto` (animeId, title, englishTitle, pictureUrl, mediaType, airingStatus, totalEpisodes, averageEpisodeDurationSeconds, airedFrom, malScore, relationType, order, entry) reusing `UserAnimeEntryDto` from `Services/Entries`.
- [x] 3.2 Add `SeriesScoresDto` — `malMain`/`malAll`/`mineMain`/`mineAll`, each a value plus `scoredCount`/`totalCount`; unweighted means over entries that have a score, my score of 0 excluded; nulls when nothing is scored, unrounded values.
- [x] 3.3 Add `SeriesStatsDto` — main-line episode total, runtime seconds, extras episode total and runtime seconds, `hasUnknownEpisodeCounts`, my watched episodes/seconds, entries completed, member counts, longest-gap days with its two anime ids, highest MAL-scored and my highest-scored anime ids, studios, genres. Runtime uses `AverageEpisodeDurationSeconds` with the 24-min fallback (reference `ProfileService.AssumedMinutesPerEpisode` rather than a second literal); watched time ignores rewatch count.
- [x] 3.4 Derive the status pill server-side: `Ongoing` when any member is currently airing; `Upcoming` when none has finished and one is not yet aired; otherwise `Finished`, flagged as having an upcoming member when one has not yet aired.
- [x] 3.5 Add `Services/Series/SeriesService.cs` (+ interface): resolve a `SeriesMember` by anime id, decide build-vs-cache (no row / `IsPartial` / `BuiltAt` older than 30 days), run the build under `RefreshGate` (`series:{seriesId}` when known, else `series:anime:{seedId}`) re-checking the predicate after acquiring the gate, then project the stored series to `SeriesDto`.
- [x] 3.6 Add `SeriesNotFoundException` for the one-member/no-relations case and map it to 404 in the controller.
- [x] 3.7 Add `Controllers/SeriesController.cs` — `GET /api/series/by-anime/{animeId}` and `POST /api/series/by-anime/{animeId}/rebuild` (rebuild forces a build with the larger budget and returns the same DTO).
- [x] 3.8 Register the builder and service in `Program.cs`.
- [x] 3.9 Verify the averages read joins members to `AnimeMetadata` and `UserAnimeEntry` at request time and that nothing caches them, so a score edit shows up on the next page load.

## 4. Frontend API layer

- [x] 4.1 Add the series DTO shapes to `frontend/src/api/types.ts`, mirroring task 3.1–3.3 exactly.
- [x] 4.2 Add `getSeries(animeId)` and `rebuildSeries(animeId)` to `frontend/src/api/client.ts` using the existing `fetchJson` wrapper so both report into `connectionStatus`; `getSeries` must surface a 404 distinctly from a transport failure so the page can say "not part of a series" rather than "couldn't load".

## 5. Series page

- [x] 5.1 Add the `/series/:animeId` route to `frontend/src/AppShell.tsx` and create `pages/SeriesPage.tsx` loading through `usePageData('series:{animeId}', …)` so back/forward restore works like the other pages.
- [x] 5.2 Build the header: root picture, series title via `pickDisplayTitle`, status pill, year span (single year when first and last match), and a "Series" label so the title isn't mistaken for a single anime's.
- [x] 5.3 Build the score boxes: MAL main / MAL all / mine main / mine all, each `8.42 · 5 of 6 scored`, two decimals, "No score" when nothing is scored, MAL values rendered through `ScoreValue` so the hide toggle applies.
- [x] 5.4 Build the stats block: main-line episodes and runtime (`4d 6h 30min`, suffixed to read as a lower bound when `hasUnknownEpisodeCounts`), extras episodes/runtime as separate figures, my progress (episodes watched/total with `ProgressBar`, entries completed, time watched, time left), longest gap with its two entries, highest MAL entry, my favourite entry, studios and genres.
- [x] 5.5 Add `components/SeriesEntryRow.tsx` + `.css`: poster thumb, title, media type, year, episode count, `ScoreValue` MAL score, my score, status pill, edit button; the row links to `/anime/{id}` with the edit control outside the link (same split `AnimeCard` uses), and the hover highlight matches the my-list/top-anime row treatment.
- [x] 5.6 Render the main line as numbered rows in watch order and the More section grouped by media type in the fixed order, omitting More entirely when there are no extras.
- [x] 5.7 Wire row editing through `useEntryEditor()` and apply the saved entry back into the loaded series with `setData` (no refetch), so the row and all four averages update in place.
- [x] 5.8 Add the score comparison strip across the main line — MAL vs mine per entry — and make the MAL side render nothing at all (a short note in its place) while `useScoreVisibility()` reports hidden, since a blurred bar still leaks its magnitude.
- [x] 5.9 Add the Rebuild control with an in-progress state, plus the partial/truncated notice next to it.
- [x] 5.10 Handle the empty states: 404 → "This anime isn't part of a series"; transport failure → the same "Couldn't load" treatment the other pages use.
- [x] 5.11 Write `pages/SeriesPage.css` following the detail page's box/spacing vocabulary so the page reads as part of the app.

## 6. Anime detail page link

- [x] 6.1 In `pages/AnimeDetailPage.tsx`, add a **Series** link to the relations row pointing at `/series/{detail.animeId}`.
- [x] 6.2 Show it only when `detail.relatedAnime` contains a relation in the traversal set — no probe request — and confirm the existing Prequel/Sequel/Main series/More controls are unchanged.
- [x] 6.3 Keep the traversal set in one place across the stack: either expose it from the API or mirror it in `frontend/src/utils/anime.ts` with a comment pointing at the backend constant, so the two can't silently drift.

## 7. Verification

- [x] 7.1 Build the backend through the `sdk:10.0` Docker image and the frontend with Node 22 (`nvm use 22`).
- [x] 7.2 On a fully cached multi-season franchise: the series builds with zero MAL calls, main line holds every season and story movie in release order, specials/OVAs/music land in More.
- [x] 7.3 On a franchise with members never fetched: the first visit returns within the fetch budget marked partial, and reopening it advances until it is no longer partial.
- [x] 7.4 Open the series from a special's detail page and from the first season's — both resolve to the same series with the same main line.
- [x] 7.5 Edit a score from a series row: the row and all four averages update without a reload, and the value persists after a refresh.
- [x] 7.6 With the hide toggle on: every MAL score and MAL average is blurred with the value absent from the DOM, and the strip's MAL side is not rendered at all.
- [x] 7.7 Navigate away and back with the browser's back button: the series page restores from the page-state snapshot without a visible reload flash.
- [x] 7.8 Check a standalone anime's detail page shows no Series link, and that an anime whose relations resolve to nothing else returns the "not part of a series" state rather than an error.

## 8. Documentation

- [x] 8.1 Update `CODE_GUIDE.md`: the endpoint table (two new series endpoints), the `Models/` and `Services/` sections (series tables, `Services/Series/`), the frontend pages table (`SeriesPage`), and the conventions note that series composition is derived from MAL relations and cached with a 30-day rebuild.
