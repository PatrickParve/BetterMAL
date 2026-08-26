using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// design.md D1/D2: score dominance, within-score banding, the never-placed
// alphabetical fallback, and unranked exclusion from the ranked set
// (tasks.md 1.6).
public class AnimeRankingSnapshotTests
{
    private static UserAnimeEntry Entry(
        int animeId, string title, int? myScore,
        WatchStatus status = WatchStatus.Completed, string? mediaType = "tv") =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = title, MediaType = mediaType, AiringStatus = "finished_airing" },
            Status = status,
            MyScore = myScore,
        };

    [Fact]
    public void ScoreDominatesOrder()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Nine", myScore: 9),
            Entry(2, "Ten", myScore: 10),
        ];

        var snapshot = AnimeRankingSnapshot.Build(entries, []);

        Assert.Equal([2, 1], snapshot.RankedEntries.Select(e => e.AnimeId));
        Assert.Equal(1, snapshot.RankOf(2));
        Assert.Equal(2, snapshot.RankOf(1));
    }

    [Fact]
    public void PlacingAtTheTopNeverBeatsAHigherScore()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Nine", myScore: 9),
            Entry(2, "Ten", myScore: 10),
        ];
        var storedOrder = new List<int> { 1, 2 }; // "9" placed first, ahead of "10"

        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder);

        Assert.Equal([2, 1], snapshot.RankedEntries.Select(e => e.AnimeId));
    }

    [Fact]
    public void BandingWithinOneScore()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Dropped one", myScore: 8, status: WatchStatus.Dropped),
            Entry(2, "Hand ordered", myScore: 8),
            Entry(3, "Music video", myScore: 8, mediaType: "music"),
        ];

        var snapshot = AnimeRankingSnapshot.Build(entries, []);

        Assert.Equal([2, 3, 1], snapshot.RankedEntries.Select(e => e.AnimeId));
    }

    [Fact]
    public void DroppedMembersOrderAlphabeticallyAmongThemselves()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Zulu", myScore: 8, status: WatchStatus.Dropped),
            Entry(2, "Alpha", myScore: 8, status: WatchStatus.Dropped),
        ];

        var snapshot = AnimeRankingSnapshot.Build(entries, []);

        Assert.Equal([2, 1], snapshot.RankedEntries.Select(e => e.AnimeId));
    }

    [Fact]
    public void NeverPlacedMembersFollowPlacedOnesAlphabetically()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Zebra", myScore: 8),
            Entry(2, "Apple", myScore: 8),
            Entry(3, "Placed second", myScore: 8),
            Entry(4, "Placed first", myScore: 8),
        ];
        var storedOrder = new List<int> { 4, 3 };

        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder);

        Assert.Equal([4, 3, 2, 1], snapshot.RankedEntries.Select(e => e.AnimeId));
    }

    [Fact]
    public void UnrankedEntriesAreExcludedFromTheRankedSet()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Scored", myScore: 8),
            Entry(2, "Unscored", myScore: null),
            Entry(3, "Plan to watch", myScore: 7, status: WatchStatus.PlanToWatch),
        ];

        var snapshot = AnimeRankingSnapshot.Build(entries, []);

        Assert.Equal([1], snapshot.RankedEntries.Select(e => e.AnimeId));
        Assert.Null(snapshot.RankOf(2));
        Assert.Null(snapshot.RankOf(3));
    }

    [Fact]
    public void RankingIsATotalOrderWithNoGaps()
    {
        var entries = Enumerable.Range(1, 5).Select(i => Entry(i, $"Anime {i}", myScore: 5)).ToList();

        var snapshot = AnimeRankingSnapshot.Build(entries, []);

        Assert.Equal([1, 2, 3, 4, 5], snapshot.RankedEntries.Select(e => snapshot.RankOf(e.AnimeId)));
    }
}
