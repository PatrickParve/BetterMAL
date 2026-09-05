using System.Collections.Concurrent;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>In-memory cache of live MAL search responses, shared by the
/// type-ahead's merged stage and the results page (design.md D7) — the two
/// now make the identical 60-candidate request (design.md D6), so one cache
/// entry serves both. A singleton because the service it backs is scoped per
/// request and the whole point is sharing across requests.</summary>
public class MalSearchCache
{
    // An entry is ~60 lean anime nodes (design.md D6); at roughly 1 KB each
    // that's ~3 MB worst case at the cap, a fine trade for removing a paced
    // MAL call from the two most common search interactions.
    private const int MaxEntries = 50;

    // Sequence, not just Expiry, orders entries for eviction — two Set calls
    // close together can land on the same DateTimeOffset.UtcNow tick, which
    // would make "oldest" ambiguous if expiry were the only order.
    private sealed record Entry(DateTimeOffset Expiry, long Sequence, List<MalAnimeListEdge> Edges);

    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private long _nextSequence;

    // 60 seconds covers the interaction it exists for — type, read the
    // dropdown, press Enter — with room for a second look, and is short
    // enough that "MAL results are a minute stale" isn't worth reasoning
    // about for a search box (design.md D7).
    public MalSearchCache() : this(TimeSpan.FromSeconds(60))
    {
    }

    // Internal: lets tests use a short TTL instead of waiting out the real one.
    internal MalSearchCache(TimeSpan ttl)
    {
        _ttl = ttl;
    }

    public List<MalAnimeListEdge>? Get(string query)
    {
        if (_entries.TryGetValue(query, out var entry) && entry.Expiry > DateTimeOffset.UtcNow)
            return entry.Edges;

        return null;
    }

    public void Set(string query, List<MalAnimeListEdge> edges)
    {
        SweepExpired();

        if (_entries.Count >= MaxEntries && !_entries.ContainsKey(query))
            EvictOldest();

        _entries[query] = new Entry(DateTimeOffset.UtcNow + _ttl, Interlocked.Increment(ref _nextSequence), edges);
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _entries)
        {
            if (entry.Expiry <= now)
                _entries.TryRemove(key, out _);
        }
    }

    private void EvictOldest()
    {
        var oldest = _entries.OrderBy(kvp => kvp.Value.Sequence).FirstOrDefault();
        if (oldest.Key is not null)
            _entries.TryRemove(oldest.Key, out _);
    }
}
