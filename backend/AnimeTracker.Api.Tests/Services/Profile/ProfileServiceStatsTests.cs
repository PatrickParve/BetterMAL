using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildStats (profile-stats "Anime stats computed from local data", tasks.md
// 3.2-3.4, design.md decisions 1/2/4). GetProfileAsync doesn't touch
// SeriesRankingLookup itself, but the constructor needs one, so these tests
// follow ProfileServiceTopSeriesTests' construction with an empty in-memory
// db standing in for it.
public class ProfileServiceStatsTests
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

    private static UserAnimeEntry Entry(
        int animeId, string? mediaType, int episodesWatched, int? totalEpisodes = null,
        int rewatchCount = 0, int? durationSeconds = null, WatchStatus status = WatchStatus.Watching) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata
            {
                Id = animeId,
                Title = $"Anime {animeId}",
                MediaType = mediaType,
                TotalEpisodes = totalEpisodes,
                AverageEpisodeDurationSeconds = durationSeconds,
            },
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
            Status = status,
        };

    [Fact]
    public async Task ARewatchAddsAFullRunToBothEpisodesAndDays()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
            [Entry(1, "tv", episodesWatched: 12, totalEpisodes: 12, rewatchCount: 1, durationSeconds: 1500, status: WatchStatus.Completed)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(24, profile.Stats.Episodes);
        Assert.Equal(Math.Round(24 * 1500 / 86400.0, 1), profile.Stats.Days);
    }

    [Fact]
    public async Task MoviesAndMusicAreExcludedFromEpisodesButPresentInDays()
    {
        using var db = CreateDb();
        var movie = Entry(1, "movie", episodesWatched: 1, durationSeconds: 6000, status: WatchStatus.Completed);
        var music = Entry(2, "music", episodesWatched: 1, durationSeconds: 300, status: WatchStatus.Completed);
        List<UserAnimeEntry> entries = [movie, music];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(0, profile.Stats.Episodes);
        Assert.Equal(Math.Round((6000 + 300) / 86400.0, 1), profile.Stats.Days);
    }

    [Fact]
    public async Task OvaAndSpecialStillCountAsEpisodes()
    {
        using var db = CreateDb();
        var ova = Entry(1, "ova", episodesWatched: 3, status: WatchStatus.Completed);
        var special = Entry(2, "special", episodesWatched: 2, status: WatchStatus.Completed);
        List<UserAnimeEntry> entries = [ova, special];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(5, profile.Stats.Episodes);
    }

    [Fact]
    public async Task DroppedEpisodesCountTowardEpisodes()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "tv", episodesWatched: 4, status: WatchStatus.Dropped)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(4, profile.Stats.Episodes);
    }

    [Fact]
    public async Task MoviesStatCountsOnlyFilmsWithProgressAndARewatchedFilmOnce()
    {
        using var db = CreateDb();
        var watched = Entry(1, "movie", episodesWatched: 1, status: WatchStatus.Completed);
        var unwatched = Entry(2, "movie", episodesWatched: 0, status: WatchStatus.PlanToWatch);
        var rewatched = Entry(3, "movie", episodesWatched: 1, rewatchCount: 2, status: WatchStatus.Completed);
        List<UserAnimeEntry> entries = [watched, unwatched, rewatched];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(2, profile.Stats.Movies);
    }

    [Fact]
    public async Task DaysUsesTheCachedDurationWherePresentAndTheAssumptionWhereAbsent()
    {
        using var db = CreateDb();
        var cached = Entry(1, "tv", episodesWatched: 12, durationSeconds: 1380, status: WatchStatus.Completed);
        var uncached = Entry(2, "tv", episodesWatched: 6, durationSeconds: null, status: WatchStatus.Completed);
        List<UserAnimeEntry> entries = [cached, uncached];

        var profile = await CreateService(db, entries).GetProfileAsync();

        var expectedSeconds = 12L * 1380 + 6L * ProfileService.AssumedMinutesPerEpisode * 60;
        Assert.Equal(Math.Round(expectedSeconds / 86400.0, 1), profile.Stats.Days);
    }

    // profile-stats "Rewatching has its own place in the status breakdown"
    // (tasks.md 5.10).
    [Fact]
    public async Task RewatchingIsCountedSeparatelyAndTheBreakdownStillSumsToTotalEntries()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, "tv", episodesWatched: 5, status: WatchStatus.Watching),
            Entry(2, "tv", episodesWatched: 1, totalEpisodes: 12, rewatchCount: 2, status: WatchStatus.Rewatching),
            Entry(3, "tv", episodesWatched: 3, totalEpisodes: 12, rewatchCount: 1, status: WatchStatus.Rewatching),
            Entry(4, "tv", episodesWatched: 12, totalEpisodes: 12, status: WatchStatus.Completed),
            Entry(5, "tv", episodesWatched: 0, status: WatchStatus.PlanToWatch),
            Entry(6, "tv", episodesWatched: 2, status: WatchStatus.OnHold),
            Entry(7, "tv", episodesWatched: 1, status: WatchStatus.Dropped),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(2, profile.Stats.Rewatching);
        Assert.Equal(1, profile.Stats.Watching);
        Assert.Equal(1, profile.Stats.Completed);
        var sum = profile.Stats.Watching + profile.Stats.Completed + profile.Stats.OnHold +
                  profile.Stats.Dropped + profile.Stats.PlanToWatch + profile.Stats.Rewatching;
        Assert.Equal(profile.Stats.TotalEntries, sum);
    }

    [Fact]
    public async Task ARewatchInProgressContributesTheSameEpisodeFigureAsUnderThePreviousRepresentation()
    {
        using var db = CreateDb();
        // "a twelve-episode series with a rewatch count of two whose episodes
        // watched currently reads one" (design.md D7) -> 2*12 + 1 = 25.
        List<UserAnimeEntry> entries =
            [Entry(1, "tv", episodesWatched: 1, totalEpisodes: 12, rewatchCount: 2, status: WatchStatus.Rewatching)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(25, profile.Stats.Episodes);
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

    // Not touched by BuildStats — a stub returning the empty ranking is
    // enough (tier-season-refresh-and-top-series-order task 4.7).
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
