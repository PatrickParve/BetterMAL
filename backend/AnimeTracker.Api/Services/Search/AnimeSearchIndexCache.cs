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

    // Rows and the generation they were built at are published together as
    // a single reference so a reader can never pair one rebuild's rows with
    // another's generation tag (design.md D5).
    private sealed record Entry(List<AnimeTitleProjection> Rows, long Generation);

    private Entry? _entry;
    private long _generation;

    public async Task<List<AnimeTitleProjection>> GetAsync(CancellationToken ct = default)
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

            Publish(rows, generation);
            return rows;
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

    private List<AnimeTitleProjection>? TryReadFresh()
    {
        var entry = Volatile.Read(ref _entry);
        return entry is not null && entry.Generation == Volatile.Read(ref _generation)
            ? entry.Rows
            : null;
    }

    private void Publish(List<AnimeTitleProjection> rows, long generation) =>
        Volatile.Write(ref _entry, new Entry(rows, generation));
}
