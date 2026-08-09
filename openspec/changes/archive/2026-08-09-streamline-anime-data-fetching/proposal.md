## Why

Opening one anime detail page (e.g. Legend of the Galactic Heroes) currently produces three network calls: `GET /api/anime/{id}` twice, then `POST /api/anime/{id}/related-anime/refresh`. Top Anime shows the same doubling. Two separate problems sit behind that:

1. **Duplicate reads are not collapsed anywhere.** The doubling itself comes from React's `StrictMode` double-invoking mount effects (dev only — a production build fires once), but it exposes a real fault: `GET /api/anime/{id}` and `GET /api/top-anime` are *not* pure reads. Both live-fetch from MAL and upsert when their cache is cold or stale, and neither collapses concurrent identical requests. Two overlapping calls therefore mean two MAL fetches (a full-detail fetch, or a 500-row ranking fetch) and two racing writes. The season browser already solves exactly this with `SeasonRefreshGate` and a spec requirement; detail and top-anime never got the same treatment.

2. **The related-anime media-type backfill fires at the wrong time, at the wrong cost.** The `anime-detail` spec says opening the **More** overlay triggers the backfill. The implementation triggers it from a page-load effect (`AnimeDetailPage.tsx:147`) whenever *any* relation lacks a media type — so every visit to a franchise anime spends up to 20 sequential, MAL-paced (~1/s) **full-detail** fetches for anime the user never asked to see, even if More is never opened. The 20-per-request cap is also why relations show "Unknown" on one visit and are correct on the next: the first visit caches 20, the second visit caches the next 20.

The user-visible ask ("the media type is already in the anime fetch") is half right and worth stating precisely: `GET /api/anime/{id}` already returns `mediaType` for every related anime **we have cached** — joined from our own `AnimeMetadata` at read time, since `AnimeRelatedAnime` stores no media type and MAL's `related_anime` payload carries none. It is `null` only for related anime we have never fetched. The database bears this out exactly: of 383 relation rows across 127 anime, **0** cached rows lack a media type and **51** (13%) point at an anime we have never cached. The third call is not redundant in principle — it is mistimed, over-scoped, and more expensive per entry than it needs to be.

## What Changes

**Frontend request layer**
- Add in-flight de-duplication for GETs in `api/client.ts`: an identical GET issued while one is already in flight returns the same promise instead of opening a second request. Kills the StrictMode double-fetch, double-mounts, and rapid re-navigation duplicates in one place, for every page, without introducing a response cache.
- Move the related-anime backfill trigger out of the page-load effect and onto the **More** overlay opening, matching the spec.
- Detail page: stop full-reloading `GET /api/anime/{id}` after the completion-score prompt closes. `CompletionScoreOverlay` already knows the saved entry; hand it back so the page patches state instead of re-reading (and possibly re-triggering a MAL fetch). Home and My list keep reloading — a completion changes which section their items belong to.

**Backend live-fetch paths**
- Generalize the season browser's single-flight gate into a shared keyed gate, and apply it to the two remaining visit-triggered live fetches: the anime-detail cold-row fetch and the top-anime daily ranking fetch. Waiters re-check the freshness condition after acquiring the gate, so the second caller serves the now-warm cache instead of fetching again.
- Related-anime backfill uses **lean** listing fields per entry rather than a full-detail fetch. It only needs the media type, and `metadata-refresh` already reserves full-detail fetches for import, the tiered nightly refresh, and a detail-page visit — a related anime the user never opened qualifies for none of those.
- The backfill becomes a bounded batch (10 per request) that the client repeats while the overlay is open and each round is still resolving new entries, so rows fill in progressively instead of appearing all at once after ~20 s — and no relation is left "Unknown" just because it fell beyond a per-open cap. Dragon Ball Z (35 relations, 26 uncached, above today's cap of 20) is the case that cannot resolve in one visit today.
- The backfill covers only the relations the overlay lists. The prequel, sequel, and main-series buttons show no media type, so no MAL call is spent resolving them — today one is.
- Verify first whether MAL honours nested field selection on `related_anime` (e.g. `related_anime{node{media_type}}`). If it does, media types come free with the detail fetch, the backfill endpoint is deleted outright, and this becomes a much smaller change. The design below assumes it does not (as `RelatedAnimeDto`'s comment asserts) and treats the deletion as the better outcome to check for, not to assume.

## Capabilities

### New Capabilities
- `frontend-data-loading`: How the web client issues reads — one request per distinct page load, concurrent identical GETs collapsed, no refetch to learn something a mutation response already returned.

### Modified Capabilities
- `anime-detail`: The related-anime media-type backfill is triggered by opening the More overlay (never by the page load), fetches lean fields, and runs in progressive batches until every listed relation is resolved rather than stopping at a per-open cap.
- `metadata-refresh`: Visit-triggered live fetches (anime detail, top-anime ranking) collapse concurrent duplicate requests into a single MAL fetch, matching the guarantee the season browser already makes. The related-anime backfill is added to the list of paths that must use lean fields, not full detail.
- `library-views`: The Top Anime ranking's daily refresh gets the same at-most-one-in-flight guarantee the season browser's already has.

## Impact

- **Frontend**: `api/client.ts` (dedup wrapper), `pages/AnimeDetailPage.tsx` (backfill trigger, completion reload), `components/RelatedAnimeOverlay.tsx` (progressive fill), `context/CompletionPromptContext.tsx` + `components/CompletionScoreOverlay.tsx` + `api/types.ts` (`onCompleted` carries the saved entry).
- **Backend**: `Services/Season/SeasonRefreshGate.cs` (generalized/moved), `Services/Detail/AnimeDetailService.cs`, `Services/Library/TopAnimeService.cs`, `Services/Metadata/IMetadataRefreshService.cs` + `MetadataRefreshService.cs` (lean single-anime refresh), `Services/Mal/MalClient.cs` (expose the lean field set), `Program.cs` (DI registration).
- **API contract**: `POST /api/anime/{id}/related-anime/refresh` keeps its response shape and gains an optional request body naming the related-anime ids to resolve; omitting it preserves today's behavior. No breaking changes.
- **External**: strictly fewer MAL requests per page visit. No database migration — no schema change unless the MAL nested-field check succeeds, in which case `AnimeRelatedAnime` gains a `MediaType` column.
