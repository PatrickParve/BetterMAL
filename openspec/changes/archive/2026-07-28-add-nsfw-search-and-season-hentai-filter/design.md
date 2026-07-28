## Context

Three related problems, one shared root cause in how the app talks to MAL and how browse surfaces page.

1. **Search drops NSFW titles.** `MalClient.GetSeasonAsync` and `GetUserAnimeListAsync` both pass `nsfw=true`; `SearchAnimeAsync` does not. MAL's default is to silently omit `r+`/`rx`-rated entries, so a title that is visible on the season page cannot be found by searching for it. Both search surfaces (navbar type-ahead and the full search page) go through that one method, so one flag fixes both.

2. **The search page paginates while the season page scrolls.** `SearchPage.tsx` holds `page` in the URL and renders `<Pagination variant="full">`. The backend already caps the candidate set at 100 MAL matches (`AnimeSearchService.SearchPageAsync`, `MaxResults = 100`), sorts that whole set server-side, and slices it by `offset`/`limit` — so "all results" for a query is a bounded, already-globally-sorted list of at most 100.

3. **No way to keep hentai out of the season browser.** Nothing in the cache records a content rating: `AnimeMetadata` stores `MediaType` and `AiringStatus` but not MAL's `rating`, and the lean listing fields (`DefaultAnimeFields`) never request it. `Genres` exists but is populated only by full detail fetches, so it is null for most season rows.

Constraints that shape the design: MAL is paced at 1 request/second globally (`MalRequestPacer`), so extra round-trips are expensive; season reads are strictly cache-only (`GetPageAsync` never touches MAL) and paging is server-side with a `totalCount`, so any filter must be applied in the database query, not on the client; frontend preferences in this app live in `localStorage` behind a context (`ScoreVisibilityContext`), not on the server.

## Goals / Non-Goals

**Goals**

- NSFW-rated anime appear in both search surfaces, unconditionally.
- The search results page loads as one continuous scroll with no pagination controls, keeping the sort global across everything reachable.
- A Settings toggle, default off, removes hentai (`rating == "rx"`) — and only hentai — from the season browser, applied server-side so counts and infinite scroll stay correct.
- No additional MAL requests in the steady state; ideally fewer for search than today.

**Non-Goals**

- Deepening search past MAL's first 100 matches.
- Applying the hide-NSFW filter anywhere other than the season browser (search, my list, top anime, airing, home, and detail pages are explicitly out of scope — confirmed with the user).
- A server-persisted or per-account preference; a blur/click-to-reveal treatment; multiple severity levels ("hide R+ too").
- Backfilling `Rating` for already-cached anime with a one-off job.

## Decisions

### 1. `nsfw=true` on the search request, not a per-caller flag

`SearchAnimeAsync` gets `&nsfw=true` unconditionally, mirroring the comment already on `GetSeasonAsync`. Alternative considered: an `includeNsfw` parameter so the type-ahead could stay filtered. Rejected — the two surfaces would then disagree about whether a title exists, and the hide-NSFW setting is deliberately scoped to the season browser, so nothing needs the filtered variant.

### 2. Search: fetch the whole candidate set once, reveal it in chunks

The client requests `offset=0, limit=100` **once per (query, sort)** and reveals the accumulated array in chunks of 48 as an `IntersectionObserver` sentinel scrolls into view. `SearchController` clamps `limit` to 50 today; that clamp rises to 100 to match `SeasonController` and the service's own `MaxResults`.

Alternative considered: keep server-side paging and issue a request per chunk, exactly like `SeasonPage`. Rejected — the search endpoint has no cache behind it, so every chunk re-runs the live MAL search for the same query. At 48 per chunk that is 3 MAL round-trips (≈3s of pacer time) to scroll one query's results, versus 1 today. Fetch-once is strictly fewer requests than the current paginated page, and it makes "the sort is global across all loaded results" true by construction rather than by relying on the server re-sorting an identical candidate set each time.

The endpoint's `offset`/`limit` contract is unchanged — no backend paging behavior is removed, the client simply stops using it. `totalCount` still comes from the server and still drives "is there more to reveal".

### 3. Reveal state is component state, not URL state

Only `q` and `sort` stay in the URL. The number of revealed chunks resets on every query/sort change and is not restored on back-navigation — the same behavior `SeasonPage` already has. A stale `?page=N` from an old link is simply ignored rather than redirected away; it has no effect on rendering.

### 4. `rating` as the hentai signal, stored on `AnimeMetadata`

MAL's `rating` field is the exact vocabulary the requirement is phrased in: `g`, `pg`, `pg_13`, `r`, `r+`, `rx`, where `rx` *means* Hentai. `rating` is added to `DefaultAnimeFields`, which means every path that already persists lean listing data (season, top-anime ranking, user animelist, and full detail via `FullDetailAnimeFields`) starts recording it with no extra request. Stored raw and lowercase as a nullable string, consistent with how `MediaType`/`AiringStatus` keep MAL's vocabulary rather than mapping to an enum — an unrecognized value must not break ingestion.

Alternatives considered:
- **MAL's `nsfw` field (`white`/`gray`/`black`)** — coarser and fuzzier; `gray` covers ordinary ecchi, and the boundary between `gray` and `black` is not the boundary the user asked for.
- **The `Hentai` genre** — `Genres` is populated only by full detail fetches, so it is null for essentially every season row that hasn't had its detail page opened. Useless as the primary signal.

`Rating` is written by **both** `ApplyTo` (rich) and `ApplyLeanTo` (lean). This does not violate the lean-never-clobbers-rich rule from `CODE_GUIDE.md` §5: `rating` is present in both field sets, so a lean refresh writes a real value rather than a null.

### 5. Absent rating means "not hentai"

Rows cached before this change have `Rating = null` until their season is next refreshed (at most one local day away, since visiting a season refreshes it once per day). A null rating is treated as not-hentai, so the filter under-filters briefly rather than hiding titles it cannot positively identify. No backfill job: the existing daily refresh is the backfill, and the setting is off by default so nobody is relying on it the moment it ships.

The EF predicate is `l.Anime.Rating != "rx"`. EF Core applies C# null semantics to `!=` against a non-null constant and emits `("Rating" IS NULL OR "Rating" <> 'rx')`, which is the wanted behavior — but this is exactly the shape that silently drops NULL rows if the translation is ever wrong, so the implementation verifies the generated SQL against a season containing a null-rated row.

### 6. The filter is a server-side query param, not a client-side `.filter()`

`hideHentai` is threaded `SeasonPage` → `getSeasonPage` → `GET /api/season/{y}/{s}?hideHentai=` → `SeasonBrowseService.GetPageAsync` → `SeasonRepository.GetPageAsync`, where the `Where` is applied **before** `CountAsync` — the same shape as the existing `includeMyList` filter (`design` precedent from the improve-season-browsing change). A client-side filter would leave `totalCount` overstating the result set, which breaks the `items.length < totalCount` test that drives infinite scroll and would render a short final page.

### 7. Preference lives in a new `ContentFilterContext`

A new `context/ContentFilterContext.tsx` with `{ hideHentai, toggleHideHentai }`, persisted at `localStorage` key `bettermal.hideNsfw`, mounted in `AppShell` alongside the existing providers. Alternative considered: adding a third field to `ScoreVisibilityContext`. Rejected — that context is about score display; content filtering is an unrelated concern that will read oddly there and in `CODE_GUIDE.md` §4.

On `SeasonPage`, `hideHentai` joins the **read** effect's dependencies (so toggling re-reads from cache) and is deliberately kept out of the debounced **refresh** effect's key, preserving the existing rule that only a season change triggers a MAL fetch. The refresh effect's post-refresh re-read reads it through a ref, exactly as it already does for `sort` and `inMyList`.

## Risks / Trade-offs

- **Search results are capped at 100 and now feel "complete" because there are no page numbers.** → Accepted deliberately (confirmed with the user). The cap is pre-existing — page 3 of the old UI was already empty. The server still returns `totalCount`, which the page keeps showing.
- **One 100-item response instead of a 48-item one on first paint.** → The payload is a few KB of JSON; only the first 48 cards are rendered, so image loading is unchanged from today.
- **EF null semantics on `Rating != "rx"` could exclude null-rated rows if translated as plain SQL `<>`.** → Verified against generated SQL and against a real season during implementation; falls back to an explicit `Rating == null || Rating != "rx"` if the translation is ever wrong.
- **`rating` widens every lean MAL response, including the full my-list import.** → One short string per node; no change to request count or pacing.
- **Ratings are stale/missing for up to a local day per season after deploy.** → Under-filtering only, and the setting ships off by default. Manually refreshable by clearing that season's `SeasonFetchLogs` row.
- **`?page=N` bookmarks silently land on the first chunk.** → Called out as breaking in the proposal; the search page is a transient surface and the query itself still resolves correctly.
- **The season page can now show an empty grid where results existed** (a season that is entirely hentai, with the setting on). → The existing "No anime found for this season." empty state already covers it; the `neverCached` loading branch is unaffected because `lastFetchedAt` is independent of the filter.

## Migration Plan

1. Ship the `Rating` column migration (additive, nullable — no backfill, no lock of consequence). `Program.cs` runs `db.Database.Migrate()` at startup, so deploy applies it.
2. Backend and frontend deploy together; the `hideHentai` query param defaults to `false`, so an older frontend against a newer backend behaves exactly as today.
3. Ratings populate season by season on each season's next daily refresh. To populate immediately for a season, delete its `SeasonFetchLogs` row and revisit.

**Rollback**: revert the frontend to restore pagination; the backend changes are backward compatible on their own (an unused query param and an unused nullable column), so the migration does not need to be reverted.

## Open Questions

None — scope (season-only filter), search depth (100-result cap kept), and the hentai signal (`rating == "rx"`) were confirmed with the user before writing this design.
