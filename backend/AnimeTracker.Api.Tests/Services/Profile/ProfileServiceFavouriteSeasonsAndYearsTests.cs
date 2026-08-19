using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildFavouriteSeasonsAndYears (profile-stats "Favourite seasons and years",
// design.md decision 6/task 7.2-7.3): a synthetic whole-list RecapPeriod fed
// into the same RecapRankingBuilder a recap uses, so the two surfaces can
// never disagree on a season's or year's weighted score. GetProfileAsync
// doesn't touch SeriesRankingLookup itself, but the constructor needs one, so
// these tests follow ProfileServiceTopSeriesTests' construction with an
// empty in-memory db standing in for it.
public class ProfileServiceFavouriteSeasonsAndYearsTests
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

    private static UserAnimeEntry Entry(int animeId, DateOnly? airedFrom, int? myScore) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId:D3}", AiredFrom = airedFrom },
            Status = WatchStatus.Completed,
            MyScore = myScore,
        };

    [Fact]
    public async Task ASeasonsWeightedScoreAgreesWithARecapCoveringIt()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2019, 11, 1), 8),
            Entry(2, new DateOnly(2019, 11, 15), 9),
            Entry(3, new DateOnly(2021, 4, 1), 6),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        var globalMean = RecapRankingBuilder.ScoredMean(entries)!.Value;
        var recapPeriod = RecapPeriod.MultiYear(2019, 2019);
        var recapRanking = RecapRankingBuilder.BuildSeasonRanking(
            entries.Where(e => e.Anime.AiredFrom!.Value.Year == 2019).ToList(), recapPeriod, globalMean);

        var fall2019 = profile.FavouriteSeasons.Single(s => s.Year == 2019 && s.Season == "fall");
        var recapFall2019 = recapRanking.Single(s => s.Year == 2019 && s.Season == "fall");
        Assert.Equal(recapFall2019.WeightedScore, fall2019.WeightedScore);
    }

    [Fact]
    public async Task GroupsWithNoScoredAnimeAreOmitted()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2013, 6, 1), myScore: null),
            Entry(2, new DateOnly(2019, 6, 1), myScore: 7), // keeps ScoredMean non-null
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.DoesNotContain(profile.FavouriteYears, y => y.Year == 2013);
    }

    [Fact]
    public async Task AnimeWithNoAirDateContributeToNeitherRanking()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, airedFrom: null, myScore: 9)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Empty(profile.FavouriteSeasons);
        Assert.Empty(profile.FavouriteYears);
    }

    [Fact]
    public async Task EveryRankedRowCarriesPosters()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2022, 4, 1), 10),
            Entry(2, new DateOnly(2022, 4, 2), 10),
            Entry(3, new DateOnly(2021, 1, 1), 3),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(2, profile.FavouriteYears.Count);
        Assert.NotEmpty(profile.FavouriteYears[0].TopPosters);
        Assert.NotEmpty(profile.FavouriteYears[1].TopPosters);
    }

    [Fact]
    public async Task EmptyListReturnsEmptyRankings()
    {
        using var db = CreateDb();
        var profile = await CreateService(db, []).GetProfileAsync();

        Assert.Empty(profile.FavouriteSeasons);
        Assert.Empty(profile.FavouriteYears);
    }

    [Fact]
    public async Task NothingScoredReturnsEmptyRankingsDespiteAirDates()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, new DateOnly(2022, 4, 1), myScore: null)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Empty(profile.FavouriteSeasons);
        Assert.Empty(profile.FavouriteYears);
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
