using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing.AniList;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Studios;

/// <summary>Reads and writes what the app has learned about studios' AniList pages
/// (<see cref="StudioLink"/>, studio-links design.md D7). Three rules hold on every
/// path through it: a stored id is never overwritten or cleared; a name is recorded
/// as unmatched only by <see cref="RecordAsync"/>, for a followed link whose answer
/// listed studios and held none of that name, and a later match fills it in; and a
/// lookup records matches alone, because what one anime's AniList record leaves out
/// (it may list no studio yet, or others) says nothing about whether the studio is on
/// AniList. A request that failed is never recorded, since nothing was answered;
/// callers simply do not call in that case.</summary>
public class StudioLinkStore(AnimeTrackerDbContext db)
{
    /// <summary>The record for a studio name, or null when the studio has never been
    /// asked about.</summary>
    public Task<StudioLink?> FindAsync(string name, CancellationToken ct = default) =>
        db.StudioLinks.AsNoTracking().FirstOrDefaultAsync(l => l.Name == name, ct);

    /// <summary>Whether at least one of the names has no stored id: it has no record,
    /// or its record is unmatched. A lookup asks AniList for studios only then
    /// ("nothing stored is fetched again").</summary>
    public async Task<bool> AnyWithoutIdAsync(IReadOnlyCollection<string> names, CancellationToken ct = default)
    {
        var distinct = names.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
            return false;

        var withId = await db.StudioLinks.AsNoTracking()
            .CountAsync(l => distinct.Contains(l.Name) && l.AniListStudioId != null, ct);
        return withId < distinct.Count;
    }

    /// <summary>Records what a lookup learned about one anime's studios. Each of the
    /// anime's MyAnimeList names is matched to the same anime's AniList studios
    /// (<see cref="StudioNameMatcher"/>): a match is stored unless the name already
    /// holds an id, and fills an unmatched record in. A name with no match is left
    /// exactly as it was, with no record, an unmatched one, or its id, whether AniList
    /// listed other studios for the anime or none at all; the studio may well be on
    /// AniList through another anime.</summary>
    public async Task LearnAsync(
        IReadOnlyCollection<string> malNames, IReadOnlyList<AniListStudio> aniListStudios, CancellationToken ct = default)
    {
        var matches = StudioNameMatcher.Match(malNames, aniListStudios);
        if (matches.Count > 0)
            await ApplyAsync(matches.Keys.ToList(), matches, ct);
    }

    /// <summary>Records one name a followed link asked AniList about: the id it was
    /// matched to, or null when AniList listed studios for the anime asked about and
    /// none was this one. A stored id is kept, and a match fills an unmatched record
    /// in, as in <see cref="LearnAsync"/>.</summary>
    public Task RecordAsync(string name, int? aniListStudioId, CancellationToken ct = default) =>
        ApplyAsync(
            [name],
            aniListStudioId is { } id ? new Dictionary<string, int> { [name] = id } : new Dictionary<string, int>(),
            ct);

    private async Task ApplyAsync(List<string> names, IReadOnlyDictionary<string, int> matches, CancellationToken ct)
    {
        if (names.Count == 0)
            return;

        // Another writer (a lookup and a followed link can learn the same studio at once)
        // may store one of these names between the read and the write. The second attempt
        // reads it back and applies the same rules to what is there.
        for (var attempt = 1; ; attempt++)
        {
            var existing = await db.StudioLinks
                .Where(l => names.Contains(l.Name))
                .ToDictionaryAsync(l => l.Name, StringComparer.Ordinal, ct);

            foreach (var name in names)
            {
                existing.TryGetValue(name, out var record);

                if (matches.TryGetValue(name, out var aniListStudioId))
                {
                    if (record is null)
                        db.StudioLinks.Add(new StudioLink { Name = name, AniListStudioId = aniListStudioId });
                    else if (record.AniListStudioId is null)
                        record.AniListStudioId = aniListStudioId;
                }
                else if (record is null)
                {
                    db.StudioLinks.Add(new StudioLink { Name = name, AniListStudioId = null });
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt == 1 && !ct.IsCancellationRequested)
            {
                DetachPending();
            }
            catch
            {
                // The context is shared with whoever called, whose next save would
                // otherwise carry these half-applied records along.
                DetachPending();
                throw;
            }
        }
    }

    private void DetachPending()
    {
        foreach (var entry in db.ChangeTracker.Entries<StudioLink>().ToList())
            entry.State = EntityState.Detached;
    }
}
