## Why

This change fixes the first half of **N1** from `docs/ISSUE_TRIAGE.md` ("Type-ahead reads the whole metadata cache on every request"), listed there as a judgement call under "Optional — probably skip".

The type-ahead's local stage reads every row of `AnimeMetadata` on every keystroke that settles, and the dropdown settles twice per query. Nothing about that read depends on the query, and nothing invalidates between the two reads. At a personal library's size it costs nothing; it grows with the cached catalog, which season browsing, year browsing, Top Anime and imports all add to independently of what is in the user's own list.

**Chosen fix direction.** N1's own note names two: "an in-memory index cached and refreshed when metadata changes, or a DB `ILIKE`/trigram query." This change is the first only. The DB-side option was considered and deliberately not taken — see Impact, "Out of scope".

**Verified against the code** (2026-09-18, at `44606a7`). Paths without a prefix are under `backend/AnimeTracker.Api/`.

- **The read.** `Data/Repositories/AnimeMetadataRepository.cs:19-22` is `AsNoTracking().Select(…).ToListAsync(ct)` over the whole table — no `WHERE`, no `LIMIT`. It projects five fields: `Id`, `Title`, `EnglishTitle`, `PictureUrl`, `PopularityRank`.
- **Its one caller.** `Services/Search/AnimeSearchService.cs:51`, at the top of `SearchAsync`, before any matching. `SearchAsync` filters and ranks the whole list in memory afterwards (`:52-102`). No other production code calls `GetSearchIndexAsync`; the only other references are its own interface declaration and two test fakes.
- **Twice per settled query.** `frontend/src/hooks/useAnimeSearch.ts:6-7` debounces the same trimmed query at two rates — 120 ms for the cache-only stage, 300 ms for the live-merged one (`:36-37`) — and both stages call the same endpoint, so for any query the user stops typing, the table is read twice.
- **Nothing between the two reads changes it.** The catalog changes only on a MAL fetch, an import, or a picture choice — none of which a search triggers.

## What Changes

- **A singleton in-memory cache of the search index.** A new `Services/Search/AnimeSearchIndexCache.cs`, registered `AddSingleton<IAnimeSearchIndex, AnimeSearchIndexCache>()` beside the other singletons in `Program.cs`. It holds the exact `List<AnimeTitleProjection>` `GetSearchIndexAsync` returns today, and rebuilds it by calling that same repository method through an `IServiceScopeFactory` scope — the pattern `Services/Mal/Auth/MalTokenProvider.cs:6,21-23` already uses for a singleton that needs a scoped `DbContext`.
- **No TTL.** Unlike `Services/Mal/MalSearchCache.cs`, which expires entries on a 60-second clock, this cache never expires on its own. It can only be stale between a write and the next read after it.
- **`AnimeSearchService` reads the cache instead of the repository**, at `AnimeSearchService.cs:51` and nowhere else. The return shape is unchanged — same type, same five fields, same rows, same order-independence — so the matching and ranking below it (`:52-121`) is untouched. The service keeps its `IAnimeMetadataRepository` dependency for `GetSearchFallbackIndexAsync`, which this change does not cache.
- **Seven services gain an `IAnimeSearchIndex` dependency and call `Invalidate()` after the save that commits their write.** The write surface was re-derived from the code, not taken from the triage note (see below). Invalidation goes *after* a successful `SaveChangesAsync`, never before, so a concurrent rebuild cannot re-cache pre-commit rows.
- **The rebuild is guarded.** A semaphore serializes "cache is empty, go fetch", and a generation counter makes a rebuild that a write overtook publish nothing, so two requests racing a cold cache issue one query and neither observes a half-built list.
- No change to `SearchTextMatch`, to the exact/prefix/contains ranking, to the dropdown's contents, or to the frontend's debounce timings and two-stage merge.

**The write surface, re-derived at `44606a7`.** Every place that writes `Title`, `EnglishTitle`, `PopularityRank` or the resolved `PictureUrl` onto an `AnimeMetadata` row:

| Site | What it writes | Save that commits it |
| --- | --- | --- |
| `Services/Library/TopAnimeService.cs:111` (`ApplyLeanTo`) and `:120` (`Add`) | Top Anime browsing, existing and new rows | `:156` |
| `Services/Season/SeasonBrowseService.cs:222` (`ApplyLeanTo`) and `:228` (`Add`) | season browsing, existing and new rows | `:287` |
| `Services/Sync/ReconciliationService.cs:100` (`Add`) | an anime reconciliation found that was not cached | `:162` |
| `Services/Import/InitialImportService.cs:155` (`Add`) | the initial MAL list import, one anime per call | `:159` |
| `Services/Metadata/MetadataRefreshService.cs:79` (`ApplyTo`) | the background tier refresh, a batch at a time | `:115` |
| `Services/Metadata/MetadataRefreshService.cs:149` (`Add`) and `:154` (`ApplyTo`) | `RefreshOneAsync`, insert and update branches | `:158` |
| `Services/Artwork/ArtworkSelectionService.cs:25`, `:40`, `:53` (`ResolvePictureUrl`) | set, reset and adopt an anime's picture | `:26`, `:41`, `:54` |

`ApplyTo` and `ApplyLeanTo` (`Services/Mal/MalMappingExtensions.cs:109-138,195-214`) each write `Title`, `EnglishTitle` and `PopularityRank` and call `ResolvePictureUrl()`; `ToAnimeMetadata` and `ToLeanAnimeMetadata` (`:99`, `:183`) are those same two applied to a fresh row, so every `Add` above is covered by invalidating around its save.

`MetadataRefreshService.RefreshOneAsync` is one method with three callers — the detail page's manual refresh button (`Controllers/MetadataRefreshController.cs`), `SeriesGraphBuilder`'s series build fetching missing or lean members, and `TransferImportRunner`'s device-transfer restore. Invalidating at its own save covers all three; neither `SeriesGraphBuilder.cs` nor `TransferImportRunner.cs` needs a call of its own.

**Confirmed to need no call**, each checked rather than assumed:

- `Services/Artwork/PictureRefreshService.cs:61-68` writes `PictureUrls` and `PicturesSyncedAt` through `ApplyPictureSetTo` (`MalMappingExtensions.cs:166-176`) and never calls `ResolvePictureUrl()`. The resolved `PictureUrl` the index reads is untouched.
- `ArtworkSelectionService`'s six *series*-level methods (`:61-123`) write `Series`, not `AnimeMetadata`.
- There is no `AnimeMetadata` deletion anywhere: the only `ExecuteDeleteAsync` in the codebase is `Data/Repositories/EpisodeAiringRepository.cs:56`, against `EpisodeAirings`, and there is no `ExecuteUpdateAsync` at all.

## Capabilities

### New Capabilities

None. Nothing user-visible changes.

### Modified Capabilities

- `navigation-and-search`:
  - **Modified:** "Type-ahead search, merged local + live ranked by prefix and popularity". Its cache-only stage is described as "answered from the app's own stored anime and stored series". This change lets that be served from an in-memory index rather than read from storage each time, so the requirement gains a sentence permitting that *and* binding it: the index SHALL reflect every write to a stored anime's title, English title, picture or popularity by the next query after the write is saved, and SHALL NOT go stale on a clock. Adds one scenario for a newly stored anime being matchable on the next query. The merged stage, the 5-row budget, the two series rows, the ranking and the two debounce rates are all restated unchanged.
  - **Not modified:** "Search results fall back to locally stored anime when the live search fails" (`spec.md:817`). The fallback reads its own wider projection, which this change does not cache, so its "locally stored set" wording still describes a fresh read.

## Impact

- **Backend** (under `backend/AnimeTracker.Api/`):
  - **New:** `Services/Search/IAnimeSearchIndex.cs` and `Services/Search/AnimeSearchIndexCache.cs`
  - `Program.cs`: one `AddSingleton` beside `MalSearchCache` (`:59`)
  - `Services/Search/AnimeSearchService.cs`: the dependency and line 51
  - Seven writers gain the dependency and one call each: `Services/Library/TopAnimeService.cs`, `Services/Season/SeasonBrowseService.cs`, `Services/Sync/ReconciliationService.cs`, `Services/Import/InitialImportService.cs`, `Services/Metadata/MetadataRefreshService.cs` (two methods), `Services/Artwork/ArtworkSelectionService.cs` (three methods)
  - `Data/Repositories/AnimeMetadataRepository.cs` and its interface: unchanged. The cache calls the existing method, which is what makes "the same rows as today" true by construction.
- **Backend tests** (under `backend/AnimeTracker.Api.Tests/`):
  - **New:** `Services/Search/AnimeSearchIndexCacheTests.cs` — same rows as the repository, reuse without a second query, invalidation, and a cold-cache race
  - **New:** a shared `FakeAnimeSearchIndex` recording invalidations, so each writer's tests can assert its call
  - **Extended:** the writers' existing tests. Thirteen files construct these services, 53 sites in all — 47 written as `new XService(...)` and 6 inside a `CreateService` helper using target-typed `new(...)`:

    | Service | Test files | Sites |
    | --- | --- | --- |
    | `TopAnimeService` | `Services/Library/TopAnimeServiceTests.cs` | 1 helper |
    | `SeasonBrowseService` | `Services/Season/SeasonBrowseServiceTests.cs` | 1 helper |
    | `ReconciliationService` | `Services/Sync/ReconciliationServiceRemovalTests.cs`, `…RewatchingTests.cs`, `…UnrecognizedStatusTests.cs`, `…ActivityTests.cs`, `…ProgressTests.cs` | 19 explicit + 2 helpers |
    | `InitialImportService` | `Services/Import/InitialImportServiceWorkTests.cs`, `…ActivityTests.cs` | 1 explicit + 1 helper |
    | `MetadataRefreshService` | `Services/Metadata/MetadataRefreshBackgroundServiceTests.cs`, `MetadataRefreshServiceTests.cs`, `Services/Updates/AnimeMetadataChangeDetectorSeriesBuildTests.cs` | 1 explicit + 2 helpers |
    | `ArtworkSelectionService` | `Services/Artwork/ArtworkSelectionServiceTests.cs`, `Services/Transfer/TransferImportRunnerTests.cs` | 26 explicit |

    Every one of the seven writers has tests that build the real service over an in-memory context, so every one can assert its own invalidation.
  - **Extended:** `Services/Search/AnimeSearchServiceTests.cs`, whose `FakeAnimeMetadataRepository` currently supplies the search index; it moves to supplying the fake index instead. Its assertions on ranking do not change.
- **API, database, frontend:** no change and no migration. The endpoint, its shape and its timings are identical.
- **Docs:**
  - `CODE_GUIDE.md` (local, gitignored): a note under `Services/Search/` on the cache and its invalidation contract
  - `docs/ISSUE_TRIAGE.md` (local, gitignored): N1 is half-closed, not closed — its series-lookup half stays open (below)
- **Risk this change accepts.** Invalidation is explicit, so a future write path added without an `Invalidate()` call leaves the dropdown quietly stale until the next unrelated write. The alternative (an EF `SaveChangesInterceptor`, self-maintaining) was weighed and not chosen — see design.md D3. The contract is documented on `IAnimeSearchIndex.Invalidate()` and in `CODE_GUIDE.md`.
- **Out of scope:**
  - `Services/Search/SeriesSearchLookup.cs:13-38`, the per-request `SeriesMembers ⋈ Series` join and N1's other cited half. The same direction applies, but its invalidation surface is larger and different — `SeriesGraphBuilder.cs`'s several `Add`/`Remove`/`RemoveRange` calls on `Series` and `SeriesMembers`, plus `ArtworkSelectionService`'s series-level title and picture methods. **N1 stays half-closed** unless a follow-up covers it.
  - `AnimeMetadataRepository.GetSearchFallbackIndexAsync` (`:24-29`) and `AnimeSearchService.BuildFallbackCandidatesAsync` (`:237-266`) — the results page's own full-table read, reached only when the live MAL search fails. Same pattern, rarer path, not part of N1's evidence.
  - The DB-side `ILIKE`/trigram alternative.
  - `SearchTextMatch`'s normalization and matching, and the exact/prefix/contains ranking.
  - `frontend/src/hooks/useAnimeSearch.ts` — its debounce rates and two-stage merge.
