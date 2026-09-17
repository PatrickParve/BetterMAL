Paths without a prefix are under `backend/AnimeTracker.Api/`. Test paths are under `backend/AnimeTracker.Api.Tests/`. Line numbers are as of `44606a7`, so check them before editing.

Group 1 builds the cache, group 2 switches the read onto it, group 3 wires invalidation, groups 4–6 are the tests, group 7 is docs and verification. Nothing user-visible changes; the `navigation-and-search` delta only writes down what the cache is now allowed to be and what it must never do.

## 1. The cache (design D1, D2, D5)

- [x] 1.1 Add `Services/Search/IAnimeSearchIndex.cs`:

  ```csharp
  public interface IAnimeSearchIndex
  {
      Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default);
      void Invalidate();
  }
  ```

  `AnimeTitleProjection` lives in `Data.Repositories`, so the file needs that using. Write the doc comment to cover:
  - the rows are exactly `IAnimeMetadataRepository.GetSearchIndexAsync`'s, held in memory for the type-ahead's local stage
  - **the returned list is shared and MUST NOT be mutated** (design D2); the records in it are immutable already
  - there is no TTL: only `Invalidate()` can make it stale, never the clock
  - **the invalidation contract**, on `Invalidate()` itself: every write that sets `Title`, `EnglishTitle`, `PopularityRank`, or `PictureUrl` (via `ResolvePictureUrl()`) on an `AnimeMetadata` row must call this *after* its `SaveChangesAsync` succeeds, and the seven services in task 3 are the current callers

- [x] 1.2 Add `Services/Search/AnimeSearchIndexCache.cs` implementing it, taking `IServiceScopeFactory scopeFactory` and nothing else. Follow `Services/Mal/Auth/MalTokenProvider.cs:6,21-23` — a singleton resolves `AnimeTrackerDbContext`'s scoped dependents per call, never holds them. State: a `SemaphoreSlim(1, 1)` rebuild lock, the cached `List<AnimeTitleProjection>?`, the generation it was built at, and a `long _generation`.

- [x] 1.3 Implement `GetAsync` exactly as design D5 shows:
  - fast path first: return the cached list when it is non-null **and** its generation still equals the current one, with no lock taken
  - otherwise take the rebuild lock, re-check the same condition (a queued caller reuses the rebuild it waited on), then read `_generation` into a local **before** querying
  - `using var scope = scopeFactory.CreateScope();`, `GetRequiredService<IAnimeMetadataRepository>()`, `await repository.GetSearchIndexAsync(ct)`
  - publish the rows and that captured generation together, still under the lock, and return the rows
  - release in a `finally`

  Read the cached fields through `Volatile.Read` and the counter through `Interlocked`, so the fast path cannot pair one rebuild's rows with another's generation. Comment why the generation is captured before the query and not after: a write that commits mid-rebuild bumps it, so the rebuild publishes a value the next read rejects instead of resurrecting stale rows.

- [x] 1.4 Implement `Invalidate()` as `Interlocked.Increment(ref _generation)` and nothing else. Comment that it takes no lock and does no work, so a write path never waits on a search and a background job can call it freely.

- [x] 1.5 In `Program.cs`, register `builder.Services.AddSingleton<IAnimeSearchIndex, AnimeSearchIndexCache>();` beside `MalSearchCache` at `:59`. Leave `AddScoped<IAnimeMetadataRepository, AnimeMetadataRepository>()` (`:47`) alone — the cache resolves it per rebuild.

- [x] 1.6 Confirm `Data/Repositories/AnimeMetadataRepository.cs` and `IAnimeMetadataRepository.cs` are untouched by this change. `GetSearchIndexAsync` stays the one place the projection is written; that is what makes "the same rows as today" true by construction.

## 2. The read path (design D1)

- [x] 2.1 In `Services/Search/AnimeSearchService.cs`, add `IAnimeSearchIndex searchIndex` to the primary constructor (`:11-18`). Keep `IAnimeMetadataRepository repository` — `BuildFallbackCandidatesAsync` (`:237-266`) still needs `GetSearchFallbackIndexAsync`, which this change does not cache.

- [x] 2.2 Change `:51` from `await repository.GetSearchIndexAsync(ct)` to `await searchIndex.GetAsync(ct)`. Change nothing else in the method: `:52-121` — the merge, the exact/prefix/contains bands, the popularity ordering, the series rows and the build trigger — must be byte-identical afterwards. If any of it needs editing, stop: the return shape has drifted from design D2.

- [x] 2.3 Grep `backend/AnimeTracker.Api` for `GetSearchIndexAsync`. Only two hits must remain in production code: the interface declaration and the repository implementation. `GetSearchFallbackIndexAsync`'s call in `BuildFallbackCandidatesAsync` is unchanged and stays.

## 3. Invalidation (design D3, D4)

- [x] 3.1 **Re-derive the write surface before wiring anything**, rather than trusting the proposal's table. Grep `backend/AnimeTracker.Api` for `AnimeMetadata.Add`, `.ApplyTo(`, `.ApplyLeanTo(`, `ResolvePictureUrl`, `ExecuteUpdateAsync` and `ExecuteDeleteAsync`. The result must match the proposal's seven services and its "confirmed to need no call" list; if a new write path has appeared since `44606a7`, it gets a call too and is added to the tables in the proposal and design D4.

- [x] 3.2 Each of the seven services takes `IAnimeSearchIndex searchIndex` as a **required** primary-constructor parameter (design D6 — no optional parameter, no null default), placed before the `ILogger` where there is one:
  - `Services/Library/TopAnimeService.cs:14-22`
  - `Services/Season/SeasonBrowseService.cs:19-26`
  - `Services/Sync/ReconciliationService.cs:9-13`
  - `Services/Import/InitialImportService.cs:23-27`
  - `Services/Metadata/MetadataRefreshService.cs:12-16`
  - `Services/Artwork/ArtworkSelectionService.cs:18`

- [x] 3.3 Add the `searchIndex.Invalidate()` calls, each on the line **after** its `SaveChangesAsync` (design D4 — never before, or a concurrent rebuild can cache uncommitted rows):
  - `TopAnimeService.cs`, after the save at `:156` — one call covers both the `ApplyLeanTo` at `:111` and the `Add` at `:120`
  - `SeasonBrowseService.cs`, after the save at `:287` — covers `:222` and `:228`
  - `ReconciliationService.cs`, after the save at `:162` — covers the `Add` at `:100`
  - `InitialImportService.cs`, after the save at `:159` in `ImportOneAsync`, so each imported anime is searchable as it lands. **Not** after `:119`: that save writes a `UserAnimeEntry` for already-cached metadata and touches no `AnimeMetadata`.
  - `MetadataRefreshService.cs`, inside the existing `if (due.Count > 0)` guard after the save at `:115`, and after the save at `:158`
  - `ArtworkSelectionService.cs`, after each of the three anime saves at `:26`, `:41`, `:54`. **Not** in the six series methods (`:61-123`) — they write `Series`, not `AnimeMetadata`.

  One short comment per service is enough; the contract itself lives on `Invalidate()` (task 1.1).

- [x] 3.4 Confirm no call is added to `Services/Artwork/PictureRefreshService.cs` (it writes `PictureUrls` and `PicturesSyncedAt` through `ApplyPictureSetTo` and never calls `ResolvePictureUrl()`), nor to `Services/Series/SeriesGraphBuilder.cs` or `Services/Transfer/TransferImportRunner.cs` (both reach `AnimeMetadata` only through `MetadataRefreshService.RefreshOneAsync`, covered by 3.3).

## 4. The cache's own tests (design D7)

- [x] 4.1 Add `Services/Search/FakeAnimeSearchIndex.cs` to the test project (or put it beside the first test that needs it, whichever matches how the project already shares fakes — check `Services/Search/AnimeSearchServiceTests.cs:53-60`). It implements `IAnimeSearchIndex`, returns a settable list from `GetAsync`, and counts `Invalidate()` calls. Make it `internal` so groups 5 and 6 can both use it.

- [x] 4.2 Add `Services/Search/AnimeSearchIndexCacheTests.cs`. Build the cache over a real in-memory `AnimeTrackerDbContext` (the `UseInMemoryDatabase(Guid.NewGuid().ToString())` helper the other search tests use) plus a hand-rolled `IServiceScopeFactory` that hands out a scope resolving a **counting** `IAnimeMetadataRepository` — one that delegates to the real `AnimeMetadataRepository` over that context and records how many times `GetSearchIndexAsync` ran.

- [x] 4.3 Test **the same rows as the repository**: seed several anime with distinct `Title`, `EnglishTitle`, `PictureUrl` and `PopularityRank`, including one with all three nullable fields null. One `GetAsync` returns exactly what `GetSearchIndexAsync` returns for the same data — same ids, same five fields each.

- [x] 4.4 Test **reuse**: two `GetAsync` calls with no `Invalidate()` between them run the underlying query once. Assert on the counter, not on the cache's fields (design D7).

- [x] 4.5 Test **invalidation**: read, add an anime and save, `Invalidate()`, read again — the second read includes the new anime and the query ran twice. Then a third read with no further write runs no third query.

- [x] 4.6 Test **no TTL**: a read, a wait past anything a clock-based cache would expire on, then a second read with no `Invalidate()` — the query still ran only once. Keep it a real assertion on the counter rather than a sleep of any length; an `await Task.Yield()` and a second read is enough to pin "nothing but `Invalidate()` expires this", and the test must not become slow.

- [x] 4.7 Test **the cold-cache race**: make the counting repository block on a `TaskCompletionSource` inside `GetSearchIndexAsync`, start several `GetAsync` calls, release it, await all. The query ran once, every result is the complete list, and no result is short or empty.

- [x] 4.8 Test **the overtaking write** (design D5, the case the generation counter exists for): hold the rebuild inside the repository as in 4.7; while it is blocked, save a new anime and call `Invalidate()`; release. The read that was in flight may return either list, but **the next** `GetAsync` must re-query and include the new anime rather than serving the stale rebuild.

## 5. The writers' tests (design D7)

- [x] 5.1 Update the 53 construction sites across the thirteen test files that build these services by hand, passing `FakeAnimeSearchIndex`. Search for **both** `new XService(` and target-typed `new(` inside a `private static XService CreateService(...)` helper — the helpers are easy to miss with a `new XService(` grep alone:

  | Service | Test files | Sites |
  | --- | --- | --- |
  | `TopAnimeService` | `Services/Library/TopAnimeServiceTests.cs:29-38` | 1 helper |
  | `SeasonBrowseService` | `Services/Season/SeasonBrowseServiceTests.cs:30-38` | 1 helper |
  | `ReconciliationService` | `Services/Sync/ReconciliationServiceRemovalTests.cs` (11), `…RewatchingTests.cs` (5), `…UnrecognizedStatusTests.cs` (3), `…ActivityTests.cs:21` (helper), `…ProgressTests.cs:22` (helper) | 19 + 2 helpers |
  | `InitialImportService` | `Services/Import/InitialImportServiceWorkTests.cs` (1), `…ActivityTests.cs:20` (helper) | 1 + 1 helper |
  | `MetadataRefreshService` | `Services/Metadata/MetadataRefreshBackgroundServiceTests.cs` (1), `MetadataRefreshServiceTests.cs:28` (helper), `Services/Updates/AnimeMetadataChangeDetectorSeriesBuildTests.cs:31` (helper) | 1 + 2 helpers |
  | `ArtworkSelectionService` | `Services/Artwork/ArtworkSelectionServiceTests.cs` (25), `Services/Transfer/TransferImportRunnerTests.cs` (1) | 26 |

  Where a file has a `CreateService` helper, thread the fake through it — one edit covers every test in that file. Give the helper an optional `FakeAnimeSearchIndex? searchIndex = null` parameter defaulting to a fresh fake, so the tests that need to assert on it can pass their own while the rest stay as they are. (This is a *test helper* default, not the production constructor default design D6 rejects.)

- [x] 5.2 In each of the six services' test files, add one test that the write invalidates: run the service's write path, then assert the fake recorded an invalidation. Specifically:
  - `TopAnimeServiceTests.cs` — a ranking refresh that caches a not-yet-seen anime, and one that leanly rewrites an existing row
  - `SeasonBrowseServiceTests.cs` — the same two, through a season browse
  - one of the `ReconciliationService*Tests.cs` files — a run that discovers an uncached anime
  - `InitialImportServiceActivityTests.cs` or `…WorkTests.cs` — an imported anime
  - `ArtworkSelectionServiceTests.cs` — all three anime methods (set, reset, adopt), **and** that a series picture or title write records none

- [x] 5.3 Check whether a writer's test file asserts a total `SaveChangesAsync` count or similar; invalidation adds no database work, so no such assertion should need changing. If one does, the call was placed inside a transaction or before a save — re-read design D4.

- [x] 5.4 In `Services/Metadata/MetadataRefreshServiceTests.cs` and `MetadataRefreshBackgroundServiceTests.cs`, cover both `RefreshStaleBatchAsync` (a pass with candidates invalidates; a pass with none does not) and `RefreshOneAsync` (both the insert and the update branch invalidate).

## 6. Search-path tests (design D7)

- [x] 6.1 In `Services/Search/AnimeSearchServiceTests.cs`, move the search-index fixture off `FakeAnimeMetadataRepository` (`:53-60`) and onto `FakeAnimeSearchIndex`, threading it through `CreateService` (`:38-51`). `FakeAnimeMetadataRepository` keeps `GetSearchFallbackIndexAsync` and its fallback fixture; its `GetSearchIndexAsync` becomes `throw new NotImplementedException()` alongside the other two, so a regression that reintroduces the repository read fails loudly.

- [x] 6.2 Do not edit a single ranking, merge, quoting, series-row or build-trigger assertion in that file. If any needs changing, the read path changed and task 2.2 was not kept to.

- [x] 6.3 Add the end-to-end test: over one in-memory context and one real `AnimeSearchIndexCache`, run a type-ahead search for a title, have `SeasonBrowseService` cache an anime with that title, then search again. The first search does not list it; the second does. This is the one test that ties a real writer, the real cache and the real search path together.

- [x] 6.4 `Controllers/SeriesControllerBulkBuildTests.cs:117` also fakes `GetSearchIndexAsync`. Check whether it still needs to after 6.1 — it implements the whole repository interface, so the member stays, but it can become `NotImplementedException` too if nothing reaches it.

## 7. Docs and verification

- [x] 7.1 Run the backend tests. The local SDK is now 10.0.401, matching the project's net10.0 target, so `dotnet test` runs natively — no Docker copy needed. 1520/1522 pass; the 2 failures (`AiringDaySlotGrouperTests`) are a pre-existing `C.UTF-8` locale issue formatting `HH:mm` with `.` instead of `:`, unrelated to this change (untouched file, unrelated area).

- [x] 7.2 In `CODE_GUIDE.md` (local, gitignored), add a note under `Services/Search/`: the search index is cached in a singleton with no TTL, and any new write to an `AnimeMetadata` title, English title, picture or popularity must invalidate it after its save.

- [x] 7.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored), mark N1 **half** resolved. Its `AnimeSearchService`/`GetSearchIndexAsync` half is fixed; its `SeriesSearchLookup.LoadAsync` half is not, and the entry stays open on that basis with its remaining scope narrowed to the series join.

- [x] 7.4 Sanity-check the change end to end in the running stack (`docker compose build backend`, dev ports from `.env`): type a query in the navbar, confirm the dropdown behaves exactly as before — same rows, same ordering, both stages — then browse a season containing an anime not yet stored and confirm it is matchable immediately afterwards. Verified against the rebuilt `backend` container hitting the real `api/anime/search` and `api/season/{y}/{s}/refresh` endpoints directly (no browser automation — these routes need no auth and the frontend is unchanged by this proposal): `?q=attack` and `?q=hajime` return identical rows/order for `stage=local` and the default merged stage; `POST /api/season/2000/fall/refresh` fetched a season not previously stored (`Vandread`, id 180, not in `AnimeMetadata` before); it was immediately matchable via `?q=Vandread&stage=local` with no restart, proving the cache invalidated and rebuilt in-process. No errors in backend logs.

- [x] 7.5 Run `openspec validate cache-type-ahead-search-index` .
