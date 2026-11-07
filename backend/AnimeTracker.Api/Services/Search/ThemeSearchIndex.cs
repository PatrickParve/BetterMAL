using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Search;

/// <summary>One catalogue row with the three texts a query is matched against, already
/// normalized (<see cref="SearchTextMatch.Normalize"/>), so a keystroke doesn't
/// renormalize the whole catalogue. A key is null when the row has no such text: a song
/// with no title, or an anime AnimeThemes gave no name. <c>Row</c> is the stored row
/// itself, loaded without tracking and never changed after the index is built.</summary>
public sealed record ThemeSearchEntry(
    AnimeTheme Row, string? SongTitleKey, IReadOnlyList<string> ArtistKeys, string? AnimeNameKey);

/// <summary>The stored openings-and-endings catalogue, held in memory for the search
/// page's OP/ED mode (improve-search-page design.md D6), so a keystroke need not read
/// 14.6k rows from the database. Rows only: which anime a row belongs to, and what it is
/// called, is read per query from <see cref="Search.IAnimeSearchIndex"/>, so a newly saved
/// anime or a new picture choice shows from the next query without a rebuild here.</summary>
public interface IThemeSearchIndex
{
    /// <summary>Every catalogue row, rebuilding first if the index is empty or stale. The
    /// returned list is shared across callers and MUST NOT be mutated. There is no TTL:
    /// only <see cref="Invalidate"/> can make it stale, never the passage of time.</summary>
    Task<IReadOnlyList<ThemeSearchEntry>> GetAsync(CancellationToken ct = default);

    /// <summary>Marks the index stale so the next <see cref="GetAsync"/> rebuilds it. The
    /// only writer of the catalogue is the weekly refresh, which calls this after its one
    /// <c>SaveChangesAsync</c> succeeds, never before it and never on a refresh that failed
    /// or was abandoned.</summary>
    void Invalidate();
}

/// <summary>Registered as a singleton, so, like <see cref="AnimeSearchIndexCache"/>, it
/// creates a fresh DI scope per rebuild rather than holding a scoped
/// <c>AnimeTrackerDbContext</c>. The same generation-and-rebuild-lock pattern: concurrent
/// readers of a cold index issue one query between them, and a write that commits
/// mid-rebuild leaves the rebuilt copy stale at once.</summary>
public class ThemeSearchIndex(IServiceScopeFactory scopeFactory) : IThemeSearchIndex
{
    // Serializes "the index is empty/stale, go rebuild it", and is re-checked after
    // the wait so a caller queued behind an in-flight rebuild reuses its result.
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    // Rows and the generation they were built at are published as one reference, so a
    // reader can never pair one rebuild's rows with another's generation tag.
    private sealed record Entry(IReadOnlyList<ThemeSearchEntry> Rows, long Generation);

    private Entry? _entry;
    private long _generation;

    public async Task<IReadOnlyList<ThemeSearchEntry>> GetAsync(CancellationToken ct = default)
    {
        if (TryReadFresh() is { } cached)
            return cached;

        await _rebuildLock.WaitAsync(ct);
        try
        {
            if (TryReadFresh() is { } justBuilt)
                return justBuilt;

            // Captured before the read, not after: a refresh that commits mid-rebuild bumps
            // the generation, so this rebuild publishes a value the next read rejects
            // instead of resurrecting rows that are already stale.
            var generation = Volatile.Read(ref _generation);

            var rows = await LoadRowsAsync(ct);
            IReadOnlyList<ThemeSearchEntry> built = rows.Select(ToEntry).ToList();

            Volatile.Write(ref _entry, new Entry(built, generation));
            return built;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>Takes no lock and does no work beyond a counter increment, so the refresh
    /// that calls it never waits on a search.</summary>
    public void Invalidate() => Interlocked.Increment(ref _generation);

    private IReadOnlyList<ThemeSearchEntry>? TryReadFresh()
    {
        var entry = Volatile.Read(ref _entry);
        return entry is not null && entry.Generation == Volatile.Read(ref _generation)
            ? entry.Rows
            : null;
    }

    /// <summary>The one read of the catalogue. Virtual so a test can count it and hold it
    /// open to put a write in the middle of a rebuild.</summary>
    protected virtual async Task<List<AnimeTheme>> LoadRowsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        return await db.AnimeThemes.AsNoTracking().ToListAsync(ct);
    }

    private static ThemeSearchEntry ToEntry(AnimeTheme row) =>
        new(
            row,
            row.SongTitle is null ? null : SearchTextMatch.Normalize(row.SongTitle),
            row.Artists.Select(SearchTextMatch.Normalize).ToList(),
            row.AnimeName is null ? null : SearchTextMatch.Normalize(row.AnimeName));

}
