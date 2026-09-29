namespace AnimeTracker.Api.Services.Setup;

/// <summary>The order setup takes list anime in. Each queue asks the database what is left
/// and puts it in one of these orders (design D8, D10); a worker never keeps a list of its
/// own, so a restart or a build that settled several anime at once needs no bookkeeping.
/// Ties fall back on the order the list read returned, then on the anime id, so the order is
/// the same every time it is asked for.</summary>
internal static class SetupOrdering
{
    /// <summary>The details step's order, which the series step reuses (spec "Fetching anime
    /// details fills in every list anime"):
    /// <list type="number">
    /// <item><description>anime whose entry is Watching or Rewatching;</description></item>
    /// <item><description>anime that are currently airing;</description></item>
    /// <item><description>every other anime, in the order the list read returned them.</description></item>
    /// </list></summary>
    public static IReadOnlyList<int> DetailsOrder(IEnumerable<LibraryAnime> anime, IReadOnlyDictionary<int, int> listOrder) =>
        anime
            .OrderBy(a => a.IsWatching ? 0 : a.AiringStatus == "currently_airing" ? 1 : 2)
            .ThenBy(a => PositionInList(a, listOrder))
            .ThenBy(a => a.AnimeId)
            .Select(a => a.AnimeId)
            .ToList();

    /// <summary>The airing step's order (spec "Airing dates are fetched for every list anime,
    /// most useful first"): the four tiers of <see cref="AiringPriority"/>, and within the
    /// first, anime whose entry is Watching or Rewatching ahead of the rest.</summary>
    public static IReadOnlyList<int> AiringOrder(
        IEnumerable<LibraryAnime> anime, IReadOnlyDictionary<int, int> listOrder, DateOnly today) =>
        anime
            .Select(a => (Anime: a, Tier: AiringPriority.TierOf(a.AiringStatus, a.AiredFrom, today)))
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.Tier == 1 && x.Anime.IsWatching ? 0 : 1)
            .ThenBy(x => PositionInList(x.Anime, listOrder))
            .ThenBy(x => x.Anime.AnimeId)
            .Select(x => x.Anime.AnimeId)
            .ToList();

    private static int PositionInList(LibraryAnime anime, IReadOnlyDictionary<int, int> listOrder) =>
        listOrder.GetValueOrDefault(anime.AnimeId, int.MaxValue);
}
