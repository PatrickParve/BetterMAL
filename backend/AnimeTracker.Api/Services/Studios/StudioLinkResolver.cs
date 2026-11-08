using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Airing.AniList;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Studios;

/// <summary>Finds a studio's AniList id for a studio link that has been followed
/// (studio-links design.md D7, "Way 2"). Nothing stored is fetched again: a stored id
/// is returned at once, and so is the answer for a name recorded as unmatched, both with
/// no request. Only a name never asked about makes one AniList request, about up to five
/// stored anime credited with it, and that request is capped so the new tab never waits
/// longer than <see cref="RequestCap"/> before it is sent to the search instead.</summary>
public class StudioLinkResolver(
    AnimeTrackerDbContext db,
    IAniListClient aniList,
    StudioLinkStore store,
    ILogger<StudioLinkResolver> logger)
{
    /// <summary>The longest a followed link waits on AniList, pacing included.</summary>
    public static readonly TimeSpan RequestCap = TimeSpan.FromSeconds(5);

    /// <summary>How many anime one request asks about.</summary>
    public const int MaxAnimeAsked = 5;

    /// <summary>The studio's AniList id, or null when it has none to give: AniList
    /// listed studios for the anime asked about and none was this one (now or before),
    /// asked about nothing it could match (no stored anime credits the name), failed,
    /// or did not answer in time. Only an answer that listed studios is held against the
    /// studio: a name no anime credits, an error, the cap, and an answer that lists no
    /// studio at all (AniList may not hold those anime, or may not have credited a
    /// studio to them yet) record nothing, so a later click asks again.</summary>
    public async Task<int?> ResolveAsync(string name, CancellationToken ct = default)
    {
        if (await store.FindAsync(name, ct) is { } record)
            return record.AniListStudioId;

        var animeIds = await CreditedAnimeAsync(name, ct);
        if (animeIds.Count == 0)
            return null;

        IReadOnlyDictionary<int, IReadOnlyList<AniListStudio>> answer;
        using var cap = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cap.CancelAfter(RequestCap);
        try
        {
            answer = await aniList.GetStudiosByMalIdsAsync(animeIds, cap.Token);
        }
        // The caller going away is theirs to see. Anything else, the cap included, just
        // means no answer: the link goes to the search and nothing is recorded.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AniList studios read failed for studio {Studio}; sending the link to the search.", name);
            return null;
        }

        // Each stored anime credits the name, so the name is matched against that same
        // anime's AniList studios, anime by anime.
        foreach (var studios in animeIds.Where(answer.ContainsKey).Select(id => answer[id]))
        {
            if (StudioNameMatcher.Match([name], studios).TryGetValue(name, out var aniListStudioId))
            {
                await store.RecordAsync(name, aniListStudioId, ct);
                return aniListStudioId;
            }
        }

        // No match. Only an answer that listed studios says the studio is not among them.
        // One that lists none says nothing about the studio, so the link goes to the
        // search this time and nothing is recorded.
        var listedAnyStudio = animeIds.Any(id => answer.TryGetValue(id, out var studios) && studios.Count > 0);
        if (listedAnyStudio)
            await store.RecordAsync(name, aniListStudioId: null, ct);

        return null;
    }

    // Up to five stored anime credited with the name: those in my list first, then those
    // whose AniList id is already stored (an anime AniList is known to hold), then by id
    // so the choice is the same every time.
    private async Task<List<int>> CreditedAnimeAsync(string name, CancellationToken ct) =>
        await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Studios != null && a.Studios.Contains(name))
            .OrderByDescending(a => db.UserAnimeEntries.Any(e => e.AnimeId == a.Id))
            .ThenByDescending(a => db.AnimeAiringSyncs.Any(s => s.AnimeId == a.Id && s.AniListId != null))
            .ThenBy(a => a.Id)
            .Select(a => a.Id)
            .Take(MaxAnimeAsked)
            .ToListAsync(ct);
}
