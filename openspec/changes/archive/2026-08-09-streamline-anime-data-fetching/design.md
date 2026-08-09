## Context

Three network calls fire when the Legend of the Galactic Heroes detail page opens: `GET /api/anime/44` twice, then `POST /api/anime/44/related-anime/refresh`. Top Anime doubles the same way. Tracing each:

**The doubling** is React `StrictMode` (`main.tsx:8`) double-invoking mount effects, which is dev-only — a production build fires each effect once. But it surfaces a real defect rather than being pure dev noise, because neither of these GETs is a pure read:

- `AnimeDetailService.GetDetailAsync` (`AnimeDetailService.cs:45-58`) live-fetches full detail from MAL and upserts whenever the row is missing, has no genres, or predates the related-anime migration.
- `TopAnimeService.EnsureFreshAsync` (`TopAnimeService.cs:39-57`) fetches a 500-row ranking from MAL and upserts when the last fetch was not today.

Neither collapses concurrent duplicates, so two overlapping requests both pass the freshness check before either writes, and the app spends two MAL calls and races two upserts. The season browser already solved exactly this — `SeasonRefreshGate` (a `ConcurrentDictionary<(int, string), SemaphoreSlim>`) plus the `season-browser` spec's "Concurrent visits share one refresh" scenario. Detail and top-anime never got it.

**The third call** is the related-anime media-type backfill. Two things are wrong with it:

- *When.* The `anime-detail` spec says opening the More overlay triggers it. `AnimeDetailPage.tsx:147-170` triggers it from a page-load effect whenever any relation has `mediaType === null`. So every visit to a franchise anime spends up to 20 MAL calls on relations the user may never look at.
- *How much.* `RefreshRelatedMediaTypesAsync` (`AnimeDetailService.cs:84-111`) calls `RefreshOneAsync` per uncached relation — a **full-detail** fetch (genres, synopsis, background, related_anime, …) to read one field, `media_type`. `metadata-refresh` reserves full-detail fetches for import, the nightly tiered refresh, and a detail-page visit; a related anime the user never opened is none of those.

The `MaxRelatedBackfillCount = 20` cap explains the reported "shows Unknown, then correct after leaving and coming back": visit one caches 20 relations, visit two caches the next 20 and the first 20 now resolve instantly from cache.

One premise in the report needs correcting, because it changes what the fix can be: the media type is **not** already in the anime fetch for every relation. `GET /api/anime/{id}` returns `mediaType` for relations we have cached (`AnimeDetailService.cs:74` joins them from our own `AnimeMetadata` at read time — `AnimeRelatedAnime` has no `MediaType` column), and `null` for relations we have never fetched, because MAL's `related_anime` payload carries only `id`, `title`, and `main_picture` per node. So the third call is mistimed and overpriced, not redundant.

The production database confirms this exactly. Across 383 relation rows on 127 anime: **0** cached rows lack a media type, and **51** relations (13%) point at an anime we have never cached — those are the only source of "Unknown". Legend of the Galactic Heroes now shows all 9 media types because all 9 are cached, and its rows show both ways that happens:

- ids 36370, 36371, 42886, 51805 — `LastSyncedAt` unset, `Genres` null: cached **leanly** by ordinary Season or Top-Anime browsing. `ApplyLeanTo` writes `MediaType` while deliberately leaving `LastSyncedAt` alone, so the row stays detail-incomplete and the anime still gets a proper full-detail fetch if its own page is opened. This is precisely the shape the backfill should be producing, and D4 below makes it do so.
- ids 3016, 3371, 3665, 31433, 36369 — `LastSyncedAt` = today, `Genres` populated: **full-detail** fetched, i.e. the third call the report is about. Note 3016 is the prequel, which has a dedicated button and never appears in the More overlay at all — a full-detail MAL call spent on a media type nothing renders.

Legend of the Galactic Heroes will therefore no longer reproduce the bug. **Dragon Ball Z (id 813) is the live repro**: 35 relations, 26 of them uncached — above today's `MaxRelatedBackfillCount = 20`, so one visit cannot resolve them all however long it runs.

Constraints: MAL requests are globally paced at ~1/s by `MalRequestPacer`, so every avoided call is real latency saved for every other call. `metadata-refresh`'s "No live API calls on page render" is already superseded for these paths by "Lean, visit-triggered refresh for browsed anime" and the season browser's cache-first requirement — this change does not revisit that.

## Goals / Non-Goals

**Goals:**
- One network request per distinct page read, whatever `StrictMode`, remounts, or fast navigation do.
- No duplicate MAL fetch or racing upsert when two identical visit-triggered reads overlap, enforced server-side and independent of client behavior.
- The related-anime backfill runs only when the user opens the More overlay, costs lean fields per entry, and resolves *every* listed relation rather than 20 per visit.
- No refetch to learn something a mutation response already returned.

**Non-Goals:**
- No client-side response cache, no query library (TanStack Query et al.), no `staleTime` semantics. De-duplication covers overlapping requests only; navigating back to a page still re-reads, which is the behavior we want.
- Not turning off `StrictMode` — it is doing its job by exposing this.
- Not changing when the app refreshes data from MAL (nightly tiers, per-day visit refresh, event-driven airing refresh all stay exactly as specified).
- No new websocket/SSE channel for backfill progress; batched polling from the overlay is enough.
- Not touching the `improve-airing-coverage-and-connection-feedback` change in flight on this branch.

## Decisions

### D1. De-duplicate in-flight GETs in `api/client.ts`, not per page

A module-level `Map<string, Promise<Response>>` keyed by request URL, consulted by `fetchRaw` for GET requests only, entry deleted in a `finally`. Every existing `getX()` helper inherits it with no call-site change.

Two details that matter:
- The map stores the `Response` **promise**, and each joining caller must read the body from its own clone (`res.clone().json()`), since a `Response` body can only be consumed once. Alternative — caching the parsed JSON promise instead — was rejected because `fetchRaw` is also used directly by `getPendingReconciliationDiff` and friends, which branch on `res.status` before parsing.
- Entries are removed on settle, so nothing is cached: a read after the previous one completed is a fresh request. This is deliberately weaker than a cache and needs no invalidation story.

GET-only, because two identical in-flight PATCHes are two intended edits (a double-click on "+2 episodes" must apply twice); the existing per-button `pending` flags remain the right guard there.

*Alternative considered:* fixing each page's effect individually (abort controllers, mount refs). Rejected — `AnimeDetailPage`'s effect is already correct as written; the duplication is structural, and eight pages each solving it separately is how it stays broken.

### D2. Generalize `SeasonRefreshGate` into a keyed single-flight gate

Rename/move it to a shared `RefreshGate` keyed by `string` (`$"season:{year}:{season}"`, `$"anime:{id}"`, `"top-anime"`), keep it a singleton, keep the `IDisposable` releaser shape. Season's call site changes only in how it builds its key.

Both new call sites use double-checked locking:

```
if (!NeedsFetch(cached)) return cached;
using (await gate.LockAsync(key, ct)) {
    cached = await Reload();              // re-read after acquiring
    if (!NeedsFetch(cached)) return cached;  // winner already did the work
    await Fetch();
}
```

The re-check is the load-bearing part: without it the second waiter fetches again the moment the first releases, which is precisely today's bug serialized instead of parallelized. For top-anime the re-check is `GetLastFetchedAsync()` against today's local date; for detail it is the same missing/genre-less/pre-migration predicate already at `AnimeDetailService.cs:45`.

A failed fetch releases without recording success, so the next request retries — matching the season spec's "a failed fetch does not consume the day".

*Alternative considered:* a Postgres advisory lock. Rejected — single-process deployment, and the in-memory gate is what the codebase already uses.

*Note:* the semaphore map grows one entry per anime id ever fetched cold. Bounded in practice by the library size and each entry is ~50 bytes; if that ever matters, evict on release when `CurrentCount == 1`. Not worth the race-prone code now.

### D3. Backfill on overlay open, in progress-driven batches

Frontend: delete the page-load effect and its `relatedBackfillAttemptedFor` ref. `handleOpenRelatedOverlay` starts the loop; a `cancelled` flag set by `onClose` stops it.

```
while (overlay open && some listed relation has mediaType === null) {
    const refreshed = await refreshRelatedAnimeMediaTypes(animeId);
    merge into detail.relatedAnime;          // rows update in place
    if (no newly-resolved mediaType) break;  // no progress → stop
}
```

Backend: `MaxRelatedBackfillCount` drops from 20 to a batch size of 10 and stops being a per-open ceiling — it is now how much work one request does. The loop's own termination conditions (all resolved / no progress / overlay closed) replace it as the bound.

The progress check, not a round counter, is the termination guard: it terminates on MAL failures, on anime MAL reports without a media type, and on entries MAL 404s, without needing to distinguish those cases. Worst case for a 60-relation franchise is 6 sequential requests of ~10 s each while the user has the overlay open — the same total MAL work as today, but resolving fully in one sitting and only when asked for.

The endpoint gains a request body carrying the related-anime ids the caller actually needs, and backfills only those. Today it backfills *every* uncached relation of the anime, including the prequel, sequel, and parent-story rows that have dedicated buttons and never appear in the overlay — LOGH's prequel (3016) is a full-detail MAL call spent on a media type nothing renders. The server cannot derive the overlay's set itself, because which relation gets a button is a client-side rule (first prequel, first sequel, first parent story — a *second* prequel does land in the overlay). So the caller sends the ids; the server keeps its own uncached-filter and batch cap over them. Omitting the body preserves today's all-relations behavior, so the endpoint stays usable without a client.

*Alternative considered:* one unbounded request that backfills everything. Rejected — a 60 s POST holding a connection with no intermediate UI update, which is the "everything appears at once, or not at all" behavior being complained about.

### D4. Lean fetch per backfilled relation

Add `IMetadataRefreshService.RefreshLeanAsync(int animeId)`: one `GetAnimeDetailsAsync(animeId, leanFields)` call, then `ApplyLeanTo` / `ToLeanAnimeMetadata` — the same mapping season and top-anime browsing already use, which by construction leaves detail-only fields (including stored `RelatedAnime`) untouched. `MalClient.DefaultAnimeFields` needs exposing as a public read-only field or a `MalAnimeFields` constants class, since `GetAnimeDetailsAsync` takes an explicit field collection.

The saving is not bytes — the pacer makes every call cost ~1 s regardless — it is correctness against `metadata-refresh`: a leanly-cached row stays detail-incomplete, so opening that anime's own page later still triggers its proper full-detail fetch (`AnimeDetailService.cs:45`'s genres check catches it). Today's full-detail backfill silently marks 20 never-visited anime as detail-complete.

### D5. Completion prompt hands back its saved entry

`CompletionScoreOverlay` calls `updateEntry` itself (`CompletionScoreOverlay.tsx:31`) and reports back through a bare `onClose()`, so the only way `AnimeDetailPage` can learn the new score is `onCompleted: load` (`AnimeDetailPage.tsx:225`) — a full `GET /api/anime/{id}` that can re-enter the live-fetch path, to observe one field.

Change `onClose` / `onClosed` / `onCompleted` to carry `UserAnimeEntryDto | null` (null when the user skips without scoring). `AnimeDetailPage` patches `detail.entry`. `HomePage` and `MyListPage` keep reloading — completing an anime changes which server-computed section it belongs to, which no mutation response describes; `frontend-data-loading` states that exception explicitly.

### D6. Verify MAL's nested field selection before building D3/D4

`RelatedAnimeDto.cs:9-12` asserts MAL's `related_anime` node "has no field-selection support". MAL v2 *does* support nested selection elsewhere (`alternative_titles{en}`, `list_status{...}` are both in `MalClient`), so the assertion is worth one `curl` against `anime/44?fields=related_anime{node{media_type}}` — and the variants `related_anime{media_type}` and `related_anime{node{id,media_type}}` — before writing any of this.

If any variant returns media types, this change collapses: add a `MediaType` column to `AnimeRelatedAnime`, populate it in the full-detail mapping, delete `RefreshRelatedMediaTypesAsync`, the endpoint, the client helper, and the overlay's loading state entirely — zero extra fetches, ever. That is a strictly better outcome, so it is task 1 and the design is written to be abandoned in favour of it. If MAL ignores the nesting (the expected result, and what the code comment claims), D3/D4 proceed unchanged and the comment gets the concrete evidence appended.

## Risks / Trade-offs

- **[D1 masks a real double-submit somewhere]** → GET-only, and GETs in this app are reads whose duplication is never intended. Mutations keep their existing per-action `pending` guards.
- **[Response-clone bug in the dedup wrapper]** → Consuming a `Response` body twice throws `TypeError: Body is already used`, which would break *every* joined read — the highest-blast-radius part of this change. The repo has no test runner (frontend or backend), so this is verified by hand under `StrictMode`, which double-invokes every page's load effect and therefore exercises the joined path on every page in the app.
- **[Double-checked locking gets the re-check predicate wrong]** → A too-loose predicate re-fetches (today's behavior, no regression); a too-tight one serves a stale row for one request. The detail predicate is lifted verbatim from the existing check rather than rewritten.
- **[D3's loop spins on a franchise MAL keeps failing]** → The no-progress break stops it after one wasted batch; failures are already caught per-entry and logged server-side.
- **[Overlay open for a large franchise now issues several sequential POSTs]** → Accepted and specced: they are user-initiated, bounded by the overlay staying open, terminate on completion or no progress, and total no more MAL work than today's per-visit backfill — while removing the same backfill from every visit that never opens the overlay.
- **[The gate map grows unboundedly across a long uptime]** → One `SemaphoreSlim` per distinct anime id fetched cold; bounded by library size in practice. Revisit only if it shows up in memory.
- **[Dev-only symptom, non-trivial change]** → The visible doubling is dev-only; the underlying non-idempotent GET with no in-flight collapsing is not, and would fire for real on two open tabs, a double-click on a nav link, or a browser retry. D2 fixes that case regardless of D1.

## Migration Plan

No database migration and no API contract change (unless D6 succeeds, which adds one `AnimeRelatedAnime.MediaType` column and removes an endpoint). Frontend and backend changes are independent and each is safe alone: the dedup wrapper works against today's backend, and the gates work against today's frontend. Rollback is per-commit.

## Open Questions

- **Does MAL honour `related_anime{node{media_type}}`?** Resolved by task 1; determines whether the rest of D3/D4 is built or deleted.
- **Batch size of 10** is a guess at "long enough to be worth a round trip, short enough for visible progress". Worth a look at real timings for a large franchise once it runs; trivially tunable, not worth blocking on.
