using AnimeTracker.Api.Data.Repositories;

namespace AnimeTracker.Api.Services.Search;

/// <summary>Registered as a singleton, so it creates a fresh DI scope per
/// rebuild rather than holding the scoped <c>AnimeTrackerDbContext</c>'s
/// dependents directly — the same pattern
/// <see cref="Mal.Auth.MalTokenProvider"/> uses for the same reason.</summary>
public class AnimeSearchIndexCache(IServiceScopeFactory scopeFactory) : IAnimeSearchIndex
{
    // Serializes "cache is empty/stale, go rebuild it" so concurrent requests
    // racing a cold cache issue one query, not one each. Re-checked after the
    // wait so a caller queued behind an in-flight rebuild reuses its result.
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    // Rows, their by-id view and the generation they were built at are
    // published together as a single reference, so a reader can never pair
    // one rebuild's rows with another's generation tag (design.md D5), nor
    // the list with a by-id view of a different generation
    // (improve-search-page design.md D1).
    private sealed record Entry(
        List<AnimeTitleProjection> Rows, Dictionary<int, AnimeTitleProjection> ById, long Generation);

    private Entry? _entry;
    private long _generation;

    public async Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default) =>
        (await GetEntryAsync(ct)).Rows;

    /// <summary>The same rows, keyed by id. Declared here, not left to the
    /// interface's default, so it is the cached view of the same generation as
    /// <see cref="GetAsync"/> and not a dictionary built again on every call.</summary>
    public async Task<IReadOnlyDictionary<int, AnimeTitleProjection>> GetByIdAsync(CancellationToken ct = default) =>
        (await GetEntryAsync(ct)).ById;

    private async Task<Entry> GetEntryAsync(CancellationToken ct)
    {
        if (TryReadFresh() is { } cached)
            return cached;

        await _rebuildLock.WaitAsync(ct);
        try
        {
            if (TryReadFresh() is { } justBuilt)
                return justBuilt;

            // Captured before the query, not after: a write that commits
            // mid-rebuild bumps the generation, so this rebuild publishes a
            // value the next read rejects instead of resurrecting rows that
            // are already stale.
            var generation = Volatile.Read(ref _generation);

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAnimeMetadataRepository>();
            var rows = await repository.GetSearchIndexAsync(ct);

            var entry = new Entry(rows, IAnimeSearchIndex.IndexById(rows), generation);
            Volatile.Write(ref _entry, entry);
            return entry;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>Takes no lock and does no work beyond a counter increment, so
    /// a write path never waits on a search, and a background job can call
    /// this freely.</summary>
    public void Invalidate() => Interlocked.Increment(ref _generation);

    private Entry? TryReadFresh()
    {
        var entry = Volatile.Read(ref _entry);
        return entry is not null && entry.Generation == Volatile.Read(ref _generation)
            ? entry
            : null;
    }
}
