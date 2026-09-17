## Context

`AnimeSearchService.SearchAsync` starts every type-ahead request by loading the whole `AnimeMetadata` table through `AnimeMetadataRepository.GetSearchIndexAsync` — five columns, no `WHERE`, no `LIMIT` — and does all matching and ranking over that list in memory. The dropdown settles twice per query (a 120 ms cache-only stage and a 300 ms live-merged stage), so a settled query costs two full-table reads, and nothing a search does can change the table between them.

The proposal has the evidence, the full re-derived write surface and what stays out of scope. This document settles how the cache is shaped, where invalidation is called from, and how the two races involved are closed.

Constraints this design works under:

- **Singletons here do not hold a `DbContext`.** `AnimeTrackerDbContext` is registered scoped (`Program.cs:44`). The codebase's one existing singleton that needs it, `MalTokenProvider`, takes `IServiceScopeFactory` and creates a scope per call (`Services/Mal/Auth/MalTokenProvider.cs:6,21-23`).
- **The read path must not change.** `SearchAsync:52-121` is the ranking the `navigation-and-search` spec pins down. Whatever the cache returns has to be the same type, fields and rows as today, or that code has to be re-read against the spec.
- **No TTL.** `MalSearchCache` exists to make a *remote* result briefly reusable and is correct to expire on a clock. This index is local state that can be known to be exact, so a clock would only ever make it wrong for no reason.
- **Tests construct services by hand.** Thirteen test files construct the writer services directly, 53 sites in all (47 as `new XService(...)`, 6 inside a `CreateService` helper using target-typed `new(...)`); there is no DI container in the unit tests. Anything injected into a writer has to be cheap to fake.

## Goals / Non-Goals

**Goals:**

- One database read of the search index per *change* to it, rather than one per type-ahead request.
- Rows identical to today's, by construction rather than by re-implementation.
- Staleness bounded to "between a saved write and the next read", never to a clock.
- One query, and no partially built list, when concurrent requests race a cold cache.
- An invalidation contract a reader of the writer services can see at the call site.

**Non-Goals:**

- Caching `SeriesSearchLookup.LoadAsync` (N1's other half) or `GetSearchFallbackIndexAsync`. Both are named out of scope in the proposal.
- Incremental cache maintenance. Writes invalidate the whole index; they do not patch rows into it.
- Bounding the cache's memory. It is one projection of a table the app already loads whole on every search; holding one copy is strictly less than the status quo's churn.
- Any change to matching, ranking, debounce timings or the two-stage merge.

## Decisions

### D1. A separate `IAnimeSearchIndex` singleton, not caching inside the repository

`AnimeMetadataRepository` is scoped and per-request; a cache inside it would live and die with the request and buy nothing. Caching *behind* it (a scoped repository delegating to a singleton) would leave `GetSearchIndexAsync` looking like a database read while being something else, and would make the fallback projection's neighbouring method misleadingly similar.

So: a new `Services/Search/AnimeSearchIndexCache.cs` implementing a new `Services/Search/IAnimeSearchIndex.cs`:

```csharp
public interface IAnimeSearchIndex
{
    Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default);
    void Invalidate();
}
```

Registered `builder.Services.AddSingleton<IAnimeSearchIndex, AnimeSearchIndexCache>()` beside `MalSearchCache` (`Program.cs:59`). The implementation resolves `IAnimeMetadataRepository` from an `IServiceScopeFactory` scope per rebuild and calls the existing `GetSearchIndexAsync` — unchanged, and still the only place the projection is written. That is what makes "the same rows as today" true by construction rather than by a second query kept in sync by hand.

`AnimeSearchService` takes `IAnimeSearchIndex` as well as `IAnimeMetadataRepository` (it still needs the latter for `GetSearchFallbackIndexAsync`), and line 51 becomes `await searchIndex.GetAsync(ct)`. Nothing below it changes.

**One interface, not a split read/write pair.** `IAnimeSearchIndex` is injected into one reader and seven writers, and an ISP split would double the registrations and the fakes to stop a writer calling `GetAsync`, which is harmless anyway.

**Alternatives considered.** `IMemoryCache` with no expiry: it would hand back `object?` needing a cast, its `Remove` is stringly-keyed, and it brings an eviction model this cache exists to not have. A plain static field: untestable, and out of step with everything else in `Program.cs`.

### D2. Return the cached `List<AnimeTitleProjection>` itself, not a copy

The proposal requires the return shape to stay exactly what it is today so the ranking below it needs no changes. `AnimeTitleProjection` is a positional record — its five fields cannot be mutated — so the only hazard is a caller mutating the *list*. There is one caller, and it does `index.Select(...)`.

So `GetAsync` returns the same instance to every caller, and the interface's XML doc states that the list is shared and must not be mutated. Copying per call would add an allocation the size of the table to the path this change exists to make cheap.

`IReadOnlyList<AnimeTitleProjection>` would enforce that at the type level and `SearchAsync` would still compile, but it deviates from "the same shape as today" for a hazard that has one caller and a doc comment.

### D3. Invalidation is explicit at each write site, not an EF interceptor

**Chosen:** each of the seven writer services takes `IAnimeSearchIndex` and calls `Invalidate()` itself.

**The alternative, weighed seriously:** a `SaveChangesInterceptor` registered on `AddDbContext`, inspecting `ChangeTracker.Entries<AnimeMetadata>()` for added rows or for modified `Title`/`EnglishTitle`/`PictureUrl`/`PopularityRank`. It would be self-maintaining — a write path added later could not forget it — and the test project already knows the pattern (`Services/Entries/AiringWatchStatusServiceTests.cs:660`).

**Why not:** the interceptor has to decide *before* the save (that is when the change tracker still holds the modifications) and invalidate *after* it (D4), so it needs per-save state keyed by the `DbContext` instance and unwound on failure — machinery that is harder to read than the seven calls it replaces, in a codebase that spells things out. It also puts a search concern inside the data layer, where the next reader of `AnimeTrackerDbContext` would not look for it. And the unit tests construct `AnimeTrackerDbContext` directly, so every test wanting real invalidation would have to remember to wire the interceptor — trading a contract the compiler enforces for one it does not.

**What this costs:** a future write path added without an `Invalidate()` call leaves the dropdown stale until some unrelated write. The proposal records this as an accepted risk; the mitigations are in Risks below.

### D4. `Invalidate()` runs after a successful `SaveChangesAsync`, never before

Invalidating before the save would reopen the hole it is meant to close: a concurrent search could rebuild from rows the transaction has not committed and cache them as current. After a successful save, the rows any rebuild can read are the new ones.

It also follows that a save that throws invalidates nothing, which is right — nothing was committed.

Placement per site (line numbers at `44606a7`, to be re-checked when implementing):

| Service | Call goes after |
| --- | --- |
| `TopAnimeService` | the save at `:156`, once per refresh, covering both `:111` and `:120` |
| `SeasonBrowseService` | the save at `:287`, once per browse, covering both `:222` and `:228` |
| `ReconciliationService` | the save at `:162`, covering `:100` |
| `InitialImportService` | the save at `:159` in `ImportOneAsync`, so each imported anime is searchable as it lands |
| `MetadataRefreshService` | the save at `:115`, inside the same `if (due.Count > 0)` guard, and the save at `:158` |
| `ArtworkSelectionService` | each of the three saves at `:26`, `:41`, `:54` |

`InitialImportService:119` saves a `UserAnimeEntry` for an anime whose metadata was already cached and writes no `AnimeMetadata`, so it needs no call.

`MetadataRefreshService:115` is guarded rather than unconditional: a pass with no candidates writes nothing. A pass that had candidates but whose fetches all failed will invalidate for nothing, costing one rebuild on a job that runs every ten minutes — not worth a second condition to avoid.

### D5. A semaphore for the rebuild, and a generation counter for the write that overtakes it

Two races exist, and they need different guards.

**Concurrent cold reads.** A `SemaphoreSlim(1, 1)` around the rebuild, with the cache re-checked after the wait — the same double-check `MalTokenProvider` uses for the same reason (`MalTokenProvider.cs:17,32-43`). Requests queued behind an in-flight rebuild reuse its result instead of issuing their own query. The field is only ever published as a complete list, so no caller can see a half-built one.

**A write that lands mid-rebuild.** A rebuild reads the table, a write commits and invalidates, then the rebuild stores rows that are already stale — and, being stale-but-present, they would stay until the *next* write. A semaphore cannot fix this: the writer is not holding it.

A monotonic generation counter closes it. `Invalidate()` increments the generation; a rebuild captures the generation *before* it queries and publishes its result tagged with that value; a read treats the cache as usable only when its tag still equals the current generation. A rebuild a write overtook therefore publishes something the very next read rejects, and that read rebuilds:

```csharp
public async Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default)
{
    if (TryReadFresh() is { } cached) return cached;

    await _rebuildLock.WaitAsync(ct);
    try
    {
        if (TryReadFresh() is { } justBuilt) return justBuilt;

        var generation = Volatile.Read(ref _generation);
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnimeMetadataRepository>();
        var rows = await repository.GetSearchIndexAsync(ct);

        Publish(rows, generation);
        return rows;
    }
    finally { _rebuildLock.Release(); }
}

public void Invalidate() => Interlocked.Increment(ref _generation);
```

`Invalidate()` only bumps a counter — it takes no lock and does no work, so a write path never waits on a search, and invalidating is safe to call from a background job. The rows and their generation are published together (under the rebuild lock, read through `Volatile`/`Interlocked`) so a reader cannot pair one rebuild's rows with another's tag.

The counter is a `long`; at one invalidation per millisecond it would take ~292 million years to wrap.

**Alternative considered:** nulling a `_cached` field in `Invalidate()` and holding no generation. Simpler, and wrong in exactly the overtaking case above — the rebuild's write lands after the null and resurrects stale rows.

### D6. Writers take the dependency as a required constructor parameter

These are C# primary constructors, so an optional `IAnimeSearchIndex? searchIndex = null` parameter would compile and let the 53 existing test construction sites stay untouched. It would also mean a writer's tests silently exercise a no-op, which is the failure this design is guarding against.

So the parameter is required, the 53 sites are updated mechanically, and the test project gains one shared `FakeAnimeSearchIndex` that records invalidations — which is what the per-writer tests then assert on, so the churn buys the coverage. The six `CreateService` helpers absorb most of it: threading the fake through a helper covers every test in that file at once.

### D7. What the tests pin

- **Same rows.** The cache's first read returns exactly what `GetSearchIndexAsync` returns for the same stored anime — all five fields, ids included.
- **Reuse.** A second read with no intervening write issues no second query. Asserted with a counting repository fake, not by reaching into the cache's fields: the observable property is "the database was not asked twice".
- **Invalidation, per write site.** After each of the seven writers runs, the next read shows the change — a new anime present, a rewritten title, picture or popularity updated — rather than the pre-write value. These belong with each writer's own tests, which is where a later refactor would notice them.
- **The cold-cache race.** Several reads started together against a deliberately slow repository fake produce one query and identical complete lists, none of them short.
- **The overtaking write (D5).** A write that invalidates while a rebuild is in flight leaves the next read rebuilding rather than serving the rebuild's stale rows. This is the case the generation counter exists for, so it gets a test of its own.
- **End to end.** A type-ahead search before and after a season browse caches a new anime: absent from the first, present in the second.

`AnimeSearchServiceTests` moves its search-index fixtures from `FakeAnimeMetadataRepository` to the fake index. Its ranking assertions are not touched — if any of them needs editing, the read path was changed and this change has overreached.

## Risks / Trade-offs

- **A future write path forgets to invalidate** → the dropdown quietly serves a stale title or misses a new anime until an unrelated write. This is the cost of D3. Mitigations: the contract is stated on `IAnimeSearchIndex.Invalidate()` and noted under `Services/Search/` in `CODE_GUIDE.md`; the seven call sites all sit directly under a `SaveChangesAsync`, where a reader adding an eighth will see them; and the per-writer tests (D7) make the pattern visible in the place a new writer's tests will be written. If a stale index is ever observed in practice, D3's interceptor is the escalation.
- **A search now holds a second copy of the projection in memory** → it is one list of five-field records for the whole table, and the status quo already materialises exactly that list on every request, twice per settled query. The cache replaces churn with one retained copy.
- **A write invalidates the whole index, so a busy import rebuilds repeatedly** → `InitialImportService` saves per anime, so a 500-anime import invalidates 500 times. Each is a counter increment; a rebuild only happens if a search comes in between two of them, and that search would have read the table anyway. No worse than today, and better whenever nobody is searching.
- **The first search after any write pays the full read** → unchanged from today, where every search pays it.
- **N1 is only half closed** → `SeriesSearchLookup` still joins per request, and the triage entry stays open on that basis. The proposal says so explicitly so that archiving this change cannot be read as closing N1.

## Migration Plan

No database migration, no API change, no frontend change, no configuration. The cache is empty at startup and fills on the first search, so a deploy needs no warm-up and nothing to roll forward.

Rollback is reverting the commit: `GetSearchIndexAsync` is unchanged throughout, so the read path returns to calling it directly.

## Open Questions

None blocking. Two things to confirm while implementing:

- The line numbers in the proposal and in D4 were checked at `44606a7`. Re-derive the write surface from the code before wiring the calls — a write path may have been added since.
- `Services/Search/` is the natural home for both new files, alongside `AnimeSearchService` and `SeriesSearchLookup`. If the eventual follow-up caches the series lookup too, the two will want a shared shape; nothing here should be designed around a follow-up that may not happen, but the naming should not get in its way.
