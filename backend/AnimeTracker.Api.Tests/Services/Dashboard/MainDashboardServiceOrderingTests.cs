using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Dashboard;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// OrderCurrentlyWatching: episodes watched descending, then Anime.Title
// case-insensitively (design.md decision 1/2, task 1.1).
public class MainDashboardServiceOrderingTests
{
    private static UserAnimeEntry Entry(int animeId, string title, int episodesWatched, int? totalEpisodes = null) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = title, TotalEpisodes = totalEpisodes },
            Status = WatchStatus.Watching,
            EpisodesWatched = episodesWatched,
        };

    [Fact]
    public void MostWatchedFirst()
    {
        var entries = new List<UserAnimeEntry>
        {
            Entry(1, "Alpha", episodesWatched: 3),
            Entry(2, "Bravo", episodesWatched: 10),
            Entry(3, "Charlie", episodesWatched: 6),
        };

        var ordered = MainDashboardService.OrderCurrentlyWatching(entries).Select(e => e.AnimeId);

        Assert.Equal([2, 3, 1], ordered);
    }

    [Fact]
    public void EqualCountsFallBackToCaseInsensitiveTitleOrder()
    {
        var entries = new List<UserAnimeEntry>
        {
            Entry(1, "zebra", episodesWatched: 5),
            Entry(2, "Apple", episodesWatched: 5),
            Entry(3, "banana", episodesWatched: 5),
        };

        var ordered = MainDashboardService.OrderCurrentlyWatching(entries).Select(e => e.AnimeId);

        Assert.Equal([2, 3, 1], ordered);
    }

    [Fact]
    public void OrderingIgnoresTotalEpisodes()
    {
        // 100-of-unknown has more episodes watched than 40-of-50, so it leads
        // regardless of either entry's TotalEpisodes.
        var entries = new List<UserAnimeEntry>
        {
            Entry(1, "Forty Of Fifty", episodesWatched: 40, totalEpisodes: 50),
            Entry(2, "Hundred Of Unknown", episodesWatched: 100, totalEpisodes: null),
        };

        var ordered = MainDashboardService.OrderCurrentlyWatching(entries).Select(e => e.AnimeId);

        Assert.Equal([2, 1], ordered);
    }
}
