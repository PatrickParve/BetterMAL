using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Watching;

namespace AnimeTracker.Api.Tests.Services.Watching;

// WatchMath.RewatchInclusiveEpisodes / IsMovie / IsMusic (tasks.md 1.2-1.4,
// design.md decision 1/4).
public class WatchMathTests
{
    private static UserAnimeEntry Entry(int episodesWatched, int rewatchCount, int? totalEpisodes) =>
        new()
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1", TotalEpisodes = totalEpisodes },
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
        };

    [Fact]
    public void RewatchBaselineUsesThePublishedTotal()
    {
        var entry = Entry(episodesWatched: 12, rewatchCount: 1, totalEpisodes: 12);

        Assert.Equal(24, WatchMath.RewatchInclusiveEpisodes(entry));
    }

    [Fact]
    public void RewatchBaselineFallsBackToEpisodesWatchedWithNoPublishedTotal()
    {
        var entry = Entry(episodesWatched: 8, rewatchCount: 1, totalEpisodes: null);

        Assert.Equal(16, WatchMath.RewatchInclusiveEpisodes(entry));
    }

    [Fact]
    public void AMidRewatchEntryReadsCompletedRunsPlusCurrentProgress()
    {
        var entry = Entry(episodesWatched: 1, rewatchCount: 2, totalEpisodes: 12);

        Assert.Equal(25, WatchMath.RewatchInclusiveEpisodes(entry));
    }

    [Fact]
    public void ZeroRewatchCountContributesJustEpisodesWatched()
    {
        var entry = Entry(episodesWatched: 12, rewatchCount: 0, totalEpisodes: 12);

        Assert.Equal(12, WatchMath.RewatchInclusiveEpisodes(entry));
    }

    [Theory]
    [InlineData("movie", true)]
    [InlineData("Movie", true)]
    [InlineData("MOVIE", true)]
    [InlineData("tv", false)]
    [InlineData(null, false)]
    public void IsMovieMatchesCaseInsensitively(string? mediaType, bool expected)
    {
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", MediaType = mediaType };

        Assert.Equal(expected, WatchMath.IsMovie(anime));
    }

    [Theory]
    [InlineData("music", true)]
    [InlineData("Music", true)]
    [InlineData("MUSIC", true)]
    [InlineData("tv", false)]
    [InlineData(null, false)]
    public void IsMusicMatchesCaseInsensitively(string? mediaType, bool expected)
    {
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1", MediaType = mediaType };

        Assert.Equal(expected, WatchMath.IsMusic(anime));
    }
}
