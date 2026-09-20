using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildScopeOptions through GetProfileAsync (add-time-spent-and-trim-empty-scopes
// design.md D1/D2, tasks.md 5.1-5.3): which media-type scopes "My top anime"
// and "Most rewatched" offer, computed from the same membership rules each
// section already applies to itself.
public class ProfileServiceScopeOptionsTests
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
        int animeId, string? mediaType, int? myScore = null, int rewatchCount = 0,
        WatchStatus status = WatchStatus.Completed) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", MediaType = mediaType },
            EpisodesWatched = 1,
            MyScore = myScore,
            RewatchCount = rewatchCount,
            Status = status,
        };

    [Fact]
    public async Task AMediaTypeWithEntriesIsListed()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "tv", myScore: 8)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Contains("tv", profile.ScopeOptions.TopAnime);
    }

    [Fact]
    public async Task AMediaTypeWithNoEntriesIsNotListed()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "tv", myScore: 8)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.DoesNotContain("movie", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("ova", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("ona", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("special", profile.ScopeOptions.TopAnime);
    }

    [Fact]
    public async Task AllAndSeriesNeverAppearInEitherList()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, "tv", myScore: 8),
            Entry(2, "movie", rewatchCount: 1),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.DoesNotContain("all", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("series", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("all", profile.ScopeOptions.Rewatched);
        Assert.DoesNotContain("series", profile.ScopeOptions.Rewatched);
    }

    [Fact]
    public async Task OrderMatchesTheSharedMediaTypesOrder()
    {
        using var db = CreateDb();
        // Added out of order; TopAnimeMediaTypeScope.MediaTypes is tv, movie,
        // ova, ona, special.
        List<UserAnimeEntry> entries =
        [
            Entry(1, "special", myScore: 5),
            Entry(2, "ona", myScore: 5),
            Entry(3, "ova", myScore: 5),
            Entry(4, "movie", myScore: 5),
            Entry(5, "tv", myScore: 5),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(["tv", "movie", "ova", "ona", "special"], profile.ScopeOptions.TopAnime);
    }

    [Fact]
    public async Task AScoredNeverRewatchedEntryOffersTopAnimeOnly()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "movie", myScore: 9, rewatchCount: 0)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Contains("movie", profile.ScopeOptions.TopAnime);
        Assert.DoesNotContain("movie", profile.ScopeOptions.Rewatched);
    }

    [Fact]
    public async Task ARewatchedUnscoredEntryOffersRewatchedOnly()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "ona", myScore: null, rewatchCount: 1)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.DoesNotContain("ona", profile.ScopeOptions.TopAnime);
        Assert.Contains("ona", profile.ScopeOptions.Rewatched);
    }

    [Fact]
    public async Task ARewatchedPlanToWatchEntryOffersRewatchedOnly()
    {
        using var db = CreateDb();
        // Scored but plan-to-watch: still excluded from the ranking, so it
        // must not put its type on TopAnime even though it carries a score.
        List<UserAnimeEntry> entries = [Entry(1, "ova", myScore: 7, rewatchCount: 1, status: WatchStatus.PlanToWatch)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.DoesNotContain("ova", profile.ScopeOptions.TopAnime);
        Assert.Contains("ova", profile.ScopeOptions.Rewatched);
    }

    [Fact]
    public async Task TvSpecialOffersSpecialInBothLists()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, "tv_special", myScore: 8, rewatchCount: 1)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Contains("special", profile.ScopeOptions.TopAnime);
        Assert.Contains("special", profile.ScopeOptions.Rewatched);
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
        public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) =>
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
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

    // Not touched by BuildScopeOptions — a stub returning the empty ranking is
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
