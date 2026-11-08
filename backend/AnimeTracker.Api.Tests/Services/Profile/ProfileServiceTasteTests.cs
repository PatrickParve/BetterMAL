using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Taste;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// ProfileService.GetTasteSectionAsync (profile-stats "Hidden gems", "Top genres and Top
// studios", "Favourite combos", "Demographic, source and era", taste-stats "Taste figures
// count a defined set of entries", add-genre-and-studio-stats task 3.3). The counted set
// is every entry but Plan to watch; weighted scores use the whole list's mean. Every
// collaborator the read has no use for (a series build is a MyAnimeList job, the schedule
// service is AniList's, the activity log and the ranking service are other reads) throws,
// so the read provably touches the list and my stored ranking alone.
public class ProfileServiceTasteTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries, params int[] storedOrder) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new ThrowingActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(storedOrder),
            new SeriesRankingLookup(db),
            new ThrowingSeriesBuildTrigger(),
            new ThrowingEpisodeScheduleService(),
            new ThrowingAnimeRankingService());

    private static UserAnimeEntry Entry(
        int id, string[]? genres = null, string[]? studios = null, int? score = null,
        WatchStatus status = WatchStatus.Completed, int? popularity = null, string? source = null, DateOnly? airedFrom = null) =>
        new()
        {
            AnimeId = id,
            Anime = new AnimeMetadata
            {
                Id = id,
                Title = $"Anime {id:D3}",
                PictureUrl = $"https://img.test/{id}.jpg",
                Genres = genres?.ToList(),
                Studios = studios?.ToList(),
                PopularityRank = popularity,
                Source = source,
                AiredFrom = airedFrom,
            },
            MyScore = score,
            Status = status,
        };

    private static async Task<TasteSectionDto> ReadAsync(List<UserAnimeEntry> entries, params int[] storedOrder)
    {
        using var db = CreateDb();
        return await CreateService(db, entries, storedOrder).GetTasteSectionAsync();
    }

    [Fact]
    public async Task PlanToWatchIsLeftOutOfEveryTasteFigure()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action", "Fantasy", "Shounen"], ["Studio A"], score: 9, popularity: 5000, source: "manga", airedFrom: new DateOnly(2015, 1, 1)),
            Entry(2, ["Action", "Fantasy"], ["Studio A"], score: 8, source: "manga", airedFrom: new DateOnly(2016, 1, 1)),
            // Planned, though scored: nothing of it may show.
            Entry(3, ["Romance", "Seinen"], ["Studio P"], score: 10, status: WatchStatus.PlanToWatch, popularity: 9000, source: "novel", airedFrom: new DateOnly(1999, 1, 1)),
        ];

        var section = await ReadAsync(entries);

        Assert.Equal(["Action", "Fantasy"], section.Genres.Select(g => g.Name).OrderBy(n => n));
        Assert.Equal(["Studio A"], section.Studios.Select(s => s.Name));
        Assert.Equal([("Shounen", 1)], section.Demographics.Select(d => (d.Label, d.Count)));
        Assert.Equal([("manga", 2)], section.Sources.Select(d => (d.Label, d.Count)));
        Assert.Equal([("2010s", 2)], section.Eras.Select(d => (d.Label, d.Count)));
        Assert.Equal([1], section.HiddenGems.Select(g => g.AnimeId)); // anime 3 is the better gem, if it counted
        Assert.Equal(["Action", "Fantasy"], Assert.Single(section.Pairs).Genres);
    }

    [Theory]
    [InlineData(WatchStatus.Watching)]
    [InlineData(WatchStatus.Rewatching)]
    [InlineData(WatchStatus.Completed)]
    [InlineData(WatchStatus.OnHold)]
    [InlineData(WatchStatus.Dropped)]
    public async Task EveryStatusButPlanToWatchCounts(WatchStatus status)
    {
        // A dropped anime still counts toward its studio, and so does every other kind but a planned one.
        List<UserAnimeEntry> entries = [Entry(1, ["Action"], ["MAPPA"], score: 6, status: status)];

        var section = await ReadAsync(entries);

        Assert.Equal(1, Assert.Single(section.Studios).AnimeCount);
        Assert.Equal(1, Assert.Single(section.Genres).AnimeCount);
    }

    [Fact]
    public async Task AnEmptyListHasNothingToShow()
    {
        var section = await ReadAsync([]);

        Assert.Empty(section.Genres);
        Assert.Empty(section.Studios);
        Assert.Empty(section.Pairs);
        Assert.Empty(section.Trios);
        Assert.Empty(section.Demographics);
        Assert.Empty(section.Sources);
        Assert.Empty(section.Eras);
        Assert.Empty(section.HiddenGems);
    }

    [Fact]
    public async Task PairsAndTriosStopAtFiveAndHiddenGemsAtTen()
    {
        // Two anime carry eight genres together: 28 pairs and 56 trios, each carried twice. Twelve are gems.
        string[] genres = ["A", "B", "C", "D", "E", "F", "G", "H"];
        List<UserAnimeEntry> entries =
        [
            Entry(1, genres), Entry(2, genres),
            .. Enumerable.Range(10, 12).Select(id => Entry(id, score: 9, popularity: 2000 + id)),
        ];

        var section = await ReadAsync(entries);

        Assert.Equal(5, section.Pairs.Count);
        Assert.Equal(5, section.Trios.Count);
        Assert.Equal(10, section.HiddenGems.Count);
        Assert.Equal(2021, section.HiddenGems[0].PopularityRank); // the least popular of the twelve first
    }

    [Fact]
    public async Task GenresAndStudiosCarryBothRanksAndEveryGenreIsShipped()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], ["Studio A"], score: 9), Entry(2, ["Action"], ["Studio A"], score: 8), Entry(3, ["Action"], null, score: 8),
            Entry(4, ["Drama"], ["Studio B"]),
        ];

        var section = await ReadAsync(entries);

        var action = section.Genres.Single(g => g.Name == "Action");
        var drama = section.Genres.Single(g => g.Name == "Drama");
        Assert.Equal((1, 1, 3, 3), (action.CountRank, action.ScoreRank!.Value, action.AnimeCount, action.ScoredCount));
        Assert.Equal((2, null), (drama.CountRank, drama.ScoreRank));
        Assert.Null(drama.WeightedScore);
        var unscored = section.Studios.Single(s => s.Name == "Studio B");
        Assert.Null(unscored.ScoreRank);
        Assert.Equal(["Action", "Drama"], section.Genres.Select(g => g.Name)); // most watched order
    }

    [Fact]
    public async Task EveryWeightedScoreUsesTheWholeListsMeanIncludingAScoredPlannedAnime()
    {
        // The counted anime is scored 10; a planned one is scored 2. My whole-list mean is 6, so the genre
        // (m = 10) weighs (1/11) * 10 + (10/11) * 6 = 6.36, where the counted set's own mean of 10 would give 10.
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action"], ["Studio A"], score: 10),
            Entry(2, ["Romance"], ["Studio P"], score: 2, status: WatchStatus.PlanToWatch),
        ];

        var section = await ReadAsync(entries);

        Assert.Equal(6.36, section.Genres.Single().WeightedScore);
        // The studio (m = 5): (1/6) * 10 + (5/6) * 6 = 6.67.
        Assert.Equal(6.67, section.Studios.Single().WeightedScore);
    }

    [Fact]
    public async Task PostersFollowMyStoredRanking()
    {
        List<UserAnimeEntry> entries = Enumerable.Range(1, 5).Select(id => Entry(id, ["Action"], ["Studio A"], score: 8)).ToList();

        var section = await ReadAsync(entries, storedOrder: [4, 2, 5, 1, 3]);

        Assert.Equal([4, 2, 5], section.Genres.Single().Posters.Select(p => p.AnimeId));
        Assert.Equal([4, 2, 5], section.Studios.Single().Posters.Select(p => p.AnimeId));
    }

    [Fact]
    public async Task ReadingItTouchesNeitherMyAnimeListNorAniListNorAnyOtherRead()
    {
        // ReadAsync builds the service with collaborators that throw if anything but the list and
        // my stored ranking is read: this passing is the proof.
        List<UserAnimeEntry> entries = [Entry(1, ["Action"], ["Studio A"], score: 9, popularity: 2000, source: "manga", airedFrom: new DateOnly(2020, 1, 1))];

        var section = await ReadAsync(entries);

        Assert.Single(section.Genres);
        Assert.Single(section.HiddenGems);
    }

    [Fact]
    public async Task TwoReadsWithNothingChangedInBetweenAgree()
    {
        List<UserAnimeEntry> entries =
        [
            Entry(1, ["Action", "Fantasy"], ["Studio A", "Studio B"], score: 9, popularity: 4000, source: "manga", airedFrom: new DateOnly(2018, 1, 1)),
            Entry(2, ["Action", "Fantasy"], ["Studio B"], score: 7, source: "original", airedFrom: new DateOnly(2021, 1, 1)),
            Entry(3, ["Comedy"], ["Studio C"], source: "manga"),
        ];

        var first = await ReadAsync(entries);
        var second = await ReadAsync(entries);

        Assert.Equal(first.Genres.Select(g => (g.Name, g.CountRank, g.ScoreRank, g.WeightedScore)), second.Genres.Select(g => (g.Name, g.CountRank, g.ScoreRank, g.WeightedScore)));
        Assert.Equal(first.Studios.Select(s => (s.Name, s.CountRank, s.ScoreRank)), second.Studios.Select(s => (s.Name, s.CountRank, s.ScoreRank)));
        Assert.Equal(first.Pairs.Select(p => string.Join('+', p.Genres)), second.Pairs.Select(p => string.Join('+', p.Genres)));
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository(int[] storedOrder) : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(storedOrder.ToList());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class ThrowingActivityLogRepository : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    // A series build is a MyAnimeList job: the taste read must never start one.
    private sealed class ThrowingSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public void Enqueue(int animeId) => throw new InvalidOperationException("The taste read must not queue a series build.");
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    // The schedule service is backed by AniList's airing data.
    private sealed class ThrowingEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class ThrowingAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) => throw new NotImplementedException();
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
