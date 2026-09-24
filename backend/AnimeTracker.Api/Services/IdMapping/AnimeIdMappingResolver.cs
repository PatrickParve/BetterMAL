using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>The mapping as readers see it: what the weekly sync stored, with
/// the custom file applied on top (design.md D19). Everything that reads the
/// mapping goes through here: the IMDb links on the detail and series pages,
/// and the TMDB sets an anime or a series draws from. Nothing is ever written
/// back, so the synced mapping stays a plain mirror of the source and the sync
/// stays free to replace it whole.</summary>
public interface IAnimeIdMappingResolver
{
    /// <summary>One anime's mapping, or null when neither the synced mapping nor
    /// the custom file has any.</summary>
    Task<AnimeIdMapping?> FindAsync(int animeId, CancellationToken ct = default);

    /// <summary>The mappings of the given anime, keyed by MAL id, in one query.
    /// An anime with no mapping is simply absent.</summary>
    Task<Dictionary<int, AnimeIdMapping>> FindManyAsync(IReadOnlyCollection<int> animeIds, CancellationToken ct = default);
}

public class AnimeIdMappingResolver(AnimeTrackerDbContext db, ICustomIdMappings custom) : IAnimeIdMappingResolver
{
    public async Task<AnimeIdMapping?> FindAsync(int animeId, CancellationToken ct = default)
    {
        var synced = await db.AnimeIdMappings.AsNoTracking().FirstOrDefaultAsync(m => m.AnimeId == animeId, ct);
        return custom.Current.TryGetValue(animeId, out var entry) ? AnimeIdMappingMerge.Apply(synced, entry) : synced;
    }

    public async Task<Dictionary<int, AnimeIdMapping>> FindManyAsync(IReadOnlyCollection<int> animeIds, CancellationToken ct = default)
    {
        var mappings = await db.AnimeIdMappings.AsNoTracking()
            .Where(m => animeIds.Contains(m.AnimeId))
            .ToDictionaryAsync(m => m.AnimeId, ct);

        // One snapshot of the file for the whole call, so a change on disk can't
        // apply to half of the members.
        var entries = custom.Current;
        foreach (var animeId in animeIds)
        {
            if (!entries.TryGetValue(animeId, out var entry))
                continue;

            mappings.TryGetValue(animeId, out var synced);
            if (AnimeIdMappingMerge.Apply(synced, entry) is { } merged)
                mappings[animeId] = merged;
        }

        return mappings;
    }
}
