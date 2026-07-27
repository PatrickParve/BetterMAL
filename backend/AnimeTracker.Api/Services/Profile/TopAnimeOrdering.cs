using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Profile;

/// <summary>Shared tier-member ordering rule: explicitly ordered members first
/// (by their position in the persisted preference list), then any unordered
/// members alphabetically. Used both to resolve "My top anime" and to
/// recompute a tier's full order when merging an edit made under a filter.</summary>
public static class TopAnimeOrdering
{
    public static Dictionary<int, int> ToPositionMap(IReadOnlyList<int> orderedAnimeIds) =>
        orderedAnimeIds
            .Select((animeId, index) => (animeId, index))
            .ToDictionary(x => x.animeId, x => x.index);

    public static List<UserAnimeEntry> OrderTierMembers(
        IEnumerable<UserAnimeEntry> tierMembers,
        IReadOnlyDictionary<int, int> positionByAnimeId) =>
        tierMembers
            .OrderBy(e => positionByAnimeId.GetValueOrDefault(e.AnimeId, int.MaxValue))
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
