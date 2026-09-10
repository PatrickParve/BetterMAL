using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
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
            new FakeSeriesBuildTrigger(),
            new FakeEpisodeScheduleService(),
            new UnusedAnimeRankingService());

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
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder: []);
        var recapRanking = RecapRankingBuilder.BuildSeasonRanking(
            entries.Where(e => e.Anime.AiredFrom!.Value.Year == 2019).ToList(), recapPeriod, globalMean, snapshot);

        var fall2019 = profile.FavouriteSeasons.Single(s => s.Year == 2019 && s.Season == "fall");
        var recapFall2019 = recapRanking.Single(s => s.Year == 2019 && s.Season == "fall");
        Assert.Equal(recapFall2019.WeightedScore, fall2019.WeightedScore);
    }

    // Extended tie-break (tasks.md 6.8, design.md decision 6): two years tie
    // exactly on weighted score and scored count but differ at the highest
    // score, so the profile's favourites should order them exactly as a
    // recap covering both years would — the spec's "Equal scores resolved by
    // coverage then by score" scenario.
    [Fact]
    public async Task EqualScoresResolvedByCoverageThenByScore_AgreesWithARecapCoveringBothYears()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2020, 3, 1), 10),
            Entry(2, new DateOnly(2020, 4, 1), 10),
            Entry(3, new DateOnly(2020, 6, 1), 6),
            Entry(4, new DateOnly(2020, 8, 1), 6),
            Entry(5, new DateOnly(2020, 10, 1), 3),
            Entry(6, new DateOnly(2023, 3, 1), 10),
            Entry(7, new DateOnly(2023, 4, 1), 7),
            Entry(8, new DateOnly(2023, 6, 1), 7),
            Entry(9, new DateOnly(2023, 8, 1), 7),
            Entry(10, new DateOnly(2023, 10, 1), 4),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        var globalMean = RecapRankingBuilder.ScoredMean(entries)!.Value;
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder: []);
        var recapRanking = RecapRankingBuilder.BuildYearRanking(entries, RecapPeriod.MultiYear(2020, 2023), globalMean, snapshot);

        Assert.Equal(2, profile.FavouriteYears.Count);
        Assert.Equal(recapRanking.Select(y => y.Year), profile.FavouriteYears.Select(y => y.Year));
        Assert.Equal(recapRanking.Select(y => y.WeightedScore), profile.FavouriteYears.Select(y => y.WeightedScore));
        Assert.Equal(2020, profile.FavouriteYears[0].Year); // two 10s outranks one 10 at equal score and coverage
        Assert.Equal(2023, profile.FavouriteYears[1].Year);
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

    // ScoreCounts (design.md D5, tasks.md 11.4): the profile's favourite
    // seasons/years read the same histogram a recap covering the same period
    // reports, so the score filter's counts can't disagree between the two
    // surfaces.
    [Fact]
    public async Task ASeasonsScoreCountsAgreeWithARecapCoveringIt()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2019, 11, 1), 8),
            Entry(2, new DateOnly(2019, 11, 15), 9),
            Entry(3, new DateOnly(2019, 11, 20), 9),
            Entry(4, new DateOnly(2021, 4, 1), 6),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        var globalMean = RecapRankingBuilder.ScoredMean(entries)!.Value;
        var recapPeriod = RecapPeriod.MultiYear(2019, 2019);
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder: []);
        var recapRanking = RecapRankingBuilder.BuildSeasonRanking(
            entries.Where(e => e.Anime.AiredFrom!.Value.Year == 2019).ToList(), recapPeriod, globalMean, snapshot);

        var fall2019 = profile.FavouriteSeasons.Single(s => s.Year == 2019 && s.Season == "fall");
        var recapFall2019 = recapRanking.Single(s => s.Year == 2019 && s.Season == "fall");
        Assert.Equal(recapFall2019.ScoreCounts, fall2019.ScoreCounts);
        Assert.Equal(2, fall2019.ScoreCounts[8]); // two 9s
        Assert.Equal(1, fall2019.ScoreCounts[7]); // one 8
    }

    [Fact]
    public async Task AYearsScoreCountsAgreeWithARecapCoveringIt()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, new DateOnly(2020, 3, 1), 10),
            Entry(2, new DateOnly(2020, 4, 1), 10),
            Entry(3, new DateOnly(2020, 6, 1), 6),
            Entry(4, new DateOnly(2023, 3, 1), 7),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        var globalMean = RecapRankingBuilder.ScoredMean(entries)!.Value;
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder: []);
        var recapRanking = RecapRankingBuilder.BuildYearRanking(entries, RecapPeriod.MultiYear(2020, 2023), globalMean, snapshot);

        var year2020 = profile.FavouriteYears.Single(y => y.Year == 2020);
        var recapYear2020 = recapRanking.Single(y => y.Year == 2020);
        Assert.Equal(recapYear2020.ScoreCounts, year2020.ScoreCounts);
        Assert.Equal(2, year2020.ScoreCounts[9]); // two 10s
    }

    // design.md D1/D2, tasks.md 4.7: the profile's favourites posters follow
    // PostersByScore the same way its weighted score and score counts already
    // do — agreeing byte-for-byte with a recap covering the same year.
    [Fact]
    public async Task EveryRankedRowCarriesPostersAgreeingWithARecapCoveringTheSameYear()
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
        Assert.NotEmpty(profile.FavouriteYears[0].PostersByScore);
        Assert.NotEmpty(profile.FavouriteYears[1].PostersByScore);

        var globalMean = RecapRankingBuilder.ScoredMean(entries)!.Value;
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder: []);
        var recapRanking = RecapRankingBuilder.BuildYearRanking(entries, RecapPeriod.MultiYear(2021, 2022), globalMean, snapshot);

        var year2022 = profile.FavouriteYears.Single(y => y.Year == 2022);
        var recapYear2022 = recapRanking.Single(y => y.Year == 2022);
        Assert.Equal(
            recapYear2022.PostersByScore.SelectMany(b => b.Posters).Select(p => p.AnimeId),
            year2022.PostersByScore.SelectMany(b => b.Posters).Select(p => p.AnimeId));
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
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    // Not touched by BuildFavouriteSeasonsAndYears — a stub returning the
    // empty ranking is enough (tier-season-refresh-and-top-series-order task 4.7).
    private sealed class UnusedAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Empty);
        public Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
