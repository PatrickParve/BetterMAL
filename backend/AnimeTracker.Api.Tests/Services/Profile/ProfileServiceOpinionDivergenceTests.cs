using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildOpinionDivergence standardizes both scales to standard deviations from
// their own mean before comparing them, then applies a label gate in raw
// score terms (design.md decision 3/task 3.3). GetProfileAsync doesn't touch
// SeriesRankingLookup itself, but the constructor needs one, so these tests
// follow ProfileServiceTopSeriesTests' construction with an empty in-memory
// db standing in for it.
public class ProfileServiceOpinionDivergenceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(),
            new SeriesRankingLookup(db),
            new FakeSeriesBuildTrigger());

    private static UserAnimeEntry Rated(
        int animeId, string title, int myScore, double malScore, WatchStatus status = WatchStatus.Completed) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = title, MalScore = malScore },
            Status = status,
            MyScore = myScore,
        };

    // A population where three titles diverge far enough in each direction to
    // qualify, one per direction sits right at the label gate's excluded
    // side, and four sit in the neutral middle. Values and expected outcomes
    // (including the exact standardized divergence) were derived with a
    // population-mean/population-sd script over this exact list, not
    // hand-computed — see design.md decision 3 for the rule itself.
    private static List<UserAnimeEntry> MixedPopulation() =>
    [
        Rated(1, "Slam Dunk", 3, 9.0),
        Rated(2, "Fumetsu", 3, 8.5),
        Rated(3, "Gintama", 5, 8.9),
        Rated(4, "Neutral Six", 6, 9.0), // diverges >=1.0 SD but my=6 fails the <=5 dislike gate
        Rated(5, "Kanojo5", 9, 6.3),
        Rated(6, "Kanojo4", 8, 6.2),
        Rated(7, "Bakugan", 8, 6.4),
        Rated(8, "High But Not Dislike", 10, 7.8), // diverges >=1.0 SD but mal=7.8 fails the <=7.5 gate
        Rated(9, "Average1", 7, 7.5),
        Rated(10, "Average2", 6, 7.0),
        Rated(11, "Average3", 5, 7.2),
        Rated(12, "Average4", 4, 7.0),
    ];

    [Fact]
    public async Task AnAnimeLandsInEachList()
    {
        using var db = CreateDb();
        var section = await CreateService(db, MixedPopulation()).GetProfileAsync();

        Assert.Contains(section.TheyLikedItIDidnt, i => i.AnimeId == 1); // Slam Dunk
        Assert.Contains(section.ILikedItTheyDidnt, i => i.AnimeId == 5); // Kanojo5
    }

    [Fact]
    public async Task MyScoreAboveDislikeCeilingExcludedDespiteDivergence()
    {
        using var db = CreateDb();
        var section = await CreateService(db, MixedPopulation()).GetProfileAsync();

        Assert.DoesNotContain(section.TheyLikedItIDidnt, i => i.AnimeId == 4); // Neutral Six, my=6
    }

    [Fact]
    public async Task MalScoreAboveLikeFloorExcludedFromILikedDespiteDivergence()
    {
        using var db = CreateDb();
        var section = await CreateService(db, MixedPopulation()).GetProfileAsync();

        Assert.DoesNotContain(section.ILikedItTheyDidnt, i => i.AnimeId == 8); // High But Not Dislike, mal=7.8
    }

    [Fact]
    public async Task StrongestDivergenceFirst()
    {
        using var db = CreateDb();
        var section = await CreateService(db, MixedPopulation()).GetProfileAsync();

        Assert.Equal([1, 2, 3], section.TheyLikedItIDidnt.Select(i => i.AnimeId)); // Slam Dunk, Fumetsu, Gintama
        Assert.Equal([5, 6, 7], section.ILikedItTheyDidnt.Select(i => i.AnimeId)); // Kanojo5, Kanojo4, Bakugan
    }

    [Fact]
    public async Task TiedDivergenceBreaksOnTitleCaseInsensitively()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Rated(1, "zebra", 3, 9.0),
            Rated(2, "apple", 3, 9.0), // same my/mal scores as #1 -> identical divergence
            Rated(3, "Filler A", 6, 7.0),
            Rated(4, "Filler B", 6, 7.0),
            Rated(5, "Filler C", 6, 7.0),
            Rated(6, "Filler D", 6, 7.0),
            Rated(7, "Filler E", 6, 7.0),
            Rated(8, "Filler F", 6, 7.0),
            Rated(9, "Filler G", 6, 7.0),
            Rated(10, "Filler H", 6, 7.0),
        ];

        var section = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(["apple", "zebra"], section.TheyLikedItIDidnt.Select(i => i.Title));
    }

    [Fact]
    public async Task DroppedEntryInEitherListCarriesTheRevealFlag()
    {
        using var db = CreateDb();
        var entries = MixedPopulation();
        entries[0] = Rated(1, "Slam Dunk", 3, 9.0, WatchStatus.Dropped); // TheyLikedItIDidnt member
        entries[4] = Rated(5, "Kanojo5", 9, 6.3, WatchStatus.Dropped); // ILikedItTheyDidnt member

        var section = await CreateService(db, entries).GetProfileAsync();

        Assert.True(section.TheyLikedItIDidnt.Single(i => i.AnimeId == 1).IsCompleted);
        Assert.True(section.ILikedItTheyDidnt.Single(i => i.AnimeId == 5).IsCompleted);
    }

    [Fact]
    public async Task FewerThanMinimumRatedPairsYieldsTwoEmptyLists()
    {
        using var db = CreateDb();
        var entries = MixedPopulation().Take(9).ToList(); // one short of the minimum of 10

        var section = await CreateService(db, entries).GetProfileAsync();

        Assert.Empty(section.TheyLikedItIDidnt);
        Assert.Empty(section.ILikedItTheyDidnt);
    }

    [Fact]
    public async Task ZeroStandardDeviationYieldsTwoEmptyListsRatherThanDividingByZero()
    {
        using var db = CreateDb();
        // Every my score identical (SD = 0) over a population that otherwise
        // clears the minimum-pairs guard and would divide by that SD.
        List<UserAnimeEntry> entries = Enumerable.Range(1, 10)
            .Select(id => Rated(id, $"Anime {id}", 7, 4.0 + id * 0.5))
            .ToList();

        var section = await CreateService(db, entries).GetProfileAsync();

        Assert.Empty(section.TheyLikedItIDidnt);
        Assert.Empty(section.ILikedItTheyDidnt);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }
}
