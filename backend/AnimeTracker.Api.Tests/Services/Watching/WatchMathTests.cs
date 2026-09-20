using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Watching;

namespace AnimeTracker.Api.Tests.Services.Watching;

// WatchMath.RewatchOnlyEpisodes / RewatchInclusiveEpisodes / IsMovie / IsMusic
// (tasks.md 1.2-1.4/8.1/11.3, design.md decision 1/4 and D8).
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

    // --- RewatchOnlyEpisodes (design.md D8/task 8.1/11.3): the same
    // arithmetic RewatchInclusiveEpisodes is now expressed in terms of. ---

    [Fact]
    public void RewatchOnlyEpisodes_ZeroRewatchesIsZero()
    {
        var entry = Entry(episodesWatched: 12, rewatchCount: 0, totalEpisodes: 12);

        Assert.Equal(0, WatchMath.RewatchOnlyEpisodes(entry));
    }

    [Fact]
    public void RewatchOnlyEpisodes_PublishedTotalIsTheBaselineRegardlessOfCurrentProgress()
    {
        var entry = Entry(episodesWatched: 1, rewatchCount: 2, totalEpisodes: 12);

        Assert.Equal(24, WatchMath.RewatchOnlyEpisodes(entry));
    }

    [Fact]
    public void RewatchOnlyEpisodes_EpisodesWatchedIsTheBaselineWithNoPublishedTotal()
    {
        var entry = Entry(episodesWatched: 8, rewatchCount: 1, totalEpisodes: null);

        Assert.Equal(8, WatchMath.RewatchOnlyEpisodes(entry));
    }

    [Theory]
    [InlineData(12, 1, 12, 24)] // published total baseline
    [InlineData(8, 1, null, 16)] // no published total: episodes-watched baseline
    [InlineData(1, 2, 12, 25)] // mid-rewatch: completed runs plus current progress
    [InlineData(12, 0, 12, 12)] // never rewatched
    public void RewatchInclusiveEpisodes_StillReturnsWhatItDidBeforeTheRefactor(
        int episodesWatched, int rewatchCount, int? totalEpisodes, int expected)
    {
        var entry = Entry(episodesWatched, rewatchCount, totalEpisodes);

        Assert.Equal(expected, WatchMath.RewatchInclusiveEpisodes(entry));
        Assert.Equal(episodesWatched + WatchMath.RewatchOnlyEpisodes(entry), WatchMath.RewatchInclusiveEpisodes(entry));
    }

    // --- RewatchEpisodesIncludingCurrentRun (design.md D1, tasks.md 1.1-1.3) ---

    [Fact]
    public void RewatchEpisodesIncludingCurrentRun_CompletedRunsOnly()
    {
        // Not currently Rewatching: only the completed runs count.
        Assert.Equal(24, WatchMath.RewatchEpisodesIncludingCurrentRun(
            rewatchCount: 2, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed));
    }

    [Fact]
    public void RewatchEpisodesIncludingCurrentRun_CompletedRunsPlusAnInProgressRun()
    {
        Assert.Equal(27, WatchMath.RewatchEpisodesIncludingCurrentRun(
            rewatchCount: 2, totalEpisodes: 12, episodesWatched: 3, status: WatchStatus.Rewatching));
    }

    [Fact]
    public void RewatchEpisodesIncludingCurrentRun_AFirstRewatchInProgressWithZeroRewatchCount()
    {
        Assert.Equal(5, WatchMath.RewatchEpisodesIncludingCurrentRun(
            rewatchCount: 0, totalEpisodes: 12, episodesWatched: 5, status: WatchStatus.Rewatching));
    }

    [Fact]
    public void RewatchEpisodesIncludingCurrentRun_ANonRewatchingStatusIgnoresEpisodesWatched()
    {
        Assert.Equal(0, WatchMath.RewatchEpisodesIncludingCurrentRun(
            rewatchCount: 0, totalEpisodes: 12, episodesWatched: 5, status: WatchStatus.Watching));
    }

    [Fact]
    public void RewatchEpisodesIncludingCurrentRun_NoPublishedTotalFallsBackToEpisodesWatchedPerRun()
    {
        // Documented fallback gap: no total published, mid-rewatch.
        // RewatchOnlyEpisodes(2, null, 3) = 6, plus the in-progress run's 3 = 9.
        Assert.Equal(9, WatchMath.RewatchEpisodesIncludingCurrentRun(
            rewatchCount: 2, totalEpisodes: null, episodesWatched: 3, status: WatchStatus.Rewatching));
    }

    // --- FirstViewingEpisodes primitive (add-time-spent-and-trim-empty-scopes
    // design.md D7, tasks.md 5.7): the entry-shaped overload delegates to the
    // primitive, so the two must always agree. ---

    [Fact]
    public void FirstViewingEpisodes_RewatchingUsesThePublishedTotal()
    {
        var entry = Entry(episodesWatched: 3, rewatchCount: 1, totalEpisodes: 12);
        entry.Status = WatchStatus.Rewatching;

        Assert.Equal(12, WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status));
        Assert.Equal(WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status),
            WatchMath.FirstViewingEpisodes(entry));
    }

    [Fact]
    public void FirstViewingEpisodes_RewatchingWithNoPublishedTotalFallsBackToEpisodesWatched()
    {
        var entry = Entry(episodesWatched: 3, rewatchCount: 1, totalEpisodes: null);
        entry.Status = WatchStatus.Rewatching;

        Assert.Equal(3, WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status));
        Assert.Equal(WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status),
            WatchMath.FirstViewingEpisodes(entry));
    }

    [Fact]
    public void FirstViewingEpisodes_AnOrdinaryWatchingEntryIsItsOwnEpisodesWatched()
    {
        var entry = Entry(episodesWatched: 5, rewatchCount: 0, totalEpisodes: 12);
        entry.Status = WatchStatus.Watching;

        Assert.Equal(5, WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status));
        Assert.Equal(WatchMath.FirstViewingEpisodes(entry.Anime.TotalEpisodes, entry.EpisodesWatched, entry.Status),
            WatchMath.FirstViewingEpisodes(entry));
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
