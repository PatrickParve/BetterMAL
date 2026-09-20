using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildEpisodeProgress (profile-stats "All-list episode progress", design.md
// decision 7/task 4.2). GetProfileAsync doesn't touch SeriesRankingLookup
// itself, but the constructor needs one, so these tests follow
// ProfileServiceTopSeriesTests' construction with an empty in-memory db
// standing in for it.
public class ProfileServiceEpisodeProgressTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(
        AnimeTrackerDbContext db, List<UserAnimeEntry> entries, Dictionary<int, int>? airedCounts = null) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(),
            new SeriesRankingLookup(db),
            new FakeSeriesBuildTrigger(),
            new FakeEpisodeScheduleService(airedCounts ?? []),
            new UnusedAnimeRankingService());

    private static UserAnimeEntry Entry(
        int animeId, int? totalEpisodes, int episodesWatched,
        WatchStatus status = WatchStatus.Watching, int rewatchCount = 0, string? airingStatus = null) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes, AiringStatus = airingStatus },
            Status = status,
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
        };

    [Fact]
    public async Task UnknownTotalWithNoAiredCountIsUnresolved()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed),
            Entry(2, totalEpisodes: null, episodesWatched: 5), // still airing / unknown length, no aired count
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(1, profile.EpisodeProgress.UnresolvedEntries);
        Assert.Equal(2, profile.EpisodeProgress.TotalEntries);
        var unresolved = Assert.Single(profile.EpisodeProgress.UnresolvedAnime);
        Assert.Equal(2, unresolved.AnimeId);
        Assert.Equal(5, unresolved.EpisodesWatched);
    }

    [Fact]
    public async Task UnresolvedAnimeIsOrderedAlphabeticallyAndExcludesResolvedEntries()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed), // resolved
            new()
            {
                AnimeId = 2,
                Anime = new AnimeMetadata { Id = 2, Title = "Zebra", TotalEpisodes = null },
                EpisodesWatched = 3,
            },
            new()
            {
                AnimeId = 3,
                Anime = new AnimeMetadata { Id = 3, Title = "Apple", TotalEpisodes = null },
                EpisodesWatched = 1,
            },
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(["Apple", "Zebra"], profile.EpisodeProgress.UnresolvedAnime.Select(a => a.Title));
    }

    [Fact]
    public async Task AnAiredCountSuppliesTheDenominatorWhenNoTotalIsPublished()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, totalEpisodes: null, episodesWatched: 5)];

        var profile = await CreateService(db, entries, new Dictionary<int, int> { [1] = 8 }).GetProfileAsync();

        Assert.Equal(5, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(8, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(0, profile.EpisodeProgress.UnresolvedEntries);
    }

    [Fact]
    public async Task AZeroAiredCountWithNoPublishedTotalIsUnresolved()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, totalEpisodes: null, episodesWatched: 0)];

        var profile = await CreateService(db, entries, new Dictionary<int, int> { [1] = 0 }).GetProfileAsync();

        Assert.Equal(0, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(0, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(1, profile.EpisodeProgress.UnresolvedEntries);
    }

    [Fact]
    public async Task DroppedEntriesAreExcludedFromBothSides()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, totalEpisodes: 12, episodesWatched: 4, status: WatchStatus.Dropped),
            Entry(2, totalEpisodes: 24, episodesWatched: 24, status: WatchStatus.Completed),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(24, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(24, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(0, profile.EpisodeProgress.UnresolvedEntries);
    }

    [Fact]
    public async Task PlanToWatchEntryContributesTotalAndNoWatched()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, totalEpisodes: 24, episodesWatched: 0, status: WatchStatus.PlanToWatch)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(0, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(24, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(0, profile.EpisodeProgress.UnresolvedEntries);
    }

    [Fact]
    public async Task FilmsCount()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            new()
            {
                AnimeId = 1,
                Anime = new AnimeMetadata { Id = 1, Title = "Movie", MediaType = "movie", TotalEpisodes = 1 },
                Status = WatchStatus.Completed,
                EpisodesWatched = 1,
            },
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(1, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(1, profile.EpisodeProgress.EpisodesTotal);
    }

    [Fact]
    public async Task AStoredOverCountClampsToTheTotal()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, totalEpisodes: 12, episodesWatched: 20, status: WatchStatus.Completed)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
    }

    [Fact]
    public async Task ARewatchedEntryContributesAtMostItsTotal()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
            [Entry(1, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed, rewatchCount: 3)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
    }

    // Mirrors WatchMath.FirstViewingEpisodes, the same helper Anime stats'
    // Episodes figure uses (design.md D4): a Rewatching entry counts its
    // completed first viewing (the full total), not just how far the
    // current rewatch has gotten, so the two figures agree.
    [Fact]
    public async Task ARewatchingEntryCountsAFullFirstViewingRun()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
            [Entry(1, totalEpisodes: 12, episodesWatched: 3, status: WatchStatus.Rewatching, rewatchCount: 1)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
    }

    // A not-yet-aired anime with no published total and no aired rows has
    // nothing to progress against yet — that's a legitimate "not applicable",
    // not a data gap, so it's excluded from the figure entirely rather than
    // flagged unresolved.
    [Fact]
    public async Task ANotYetAiredAnimeWithNoTotalIsExcludedRatherThanUnresolved()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed),
            Entry(2, totalEpisodes: null, episodesWatched: 0, status: WatchStatus.PlanToWatch, airingStatus: "not_yet_aired"),
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(0, profile.EpisodeProgress.UnresolvedEntries);
        Assert.Empty(profile.EpisodeProgress.UnresolvedAnime);
    }

    // A not-yet-aired anime with a published total still resolves normally —
    // the exclusion only kicks in once both the total and the aired count
    // have failed to resolve.
    [Fact]
    public async Task ANotYetAiredAnimeWithAPublishedTotalStillCounts()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
            [Entry(1, totalEpisodes: 12, episodesWatched: 0, status: WatchStatus.PlanToWatch, airingStatus: "not_yet_aired")];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(0, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(0, profile.EpisodeProgress.UnresolvedEntries);
    }

    // A currently-airing anime with no published total and no aired rows is
    // the genuine data-gap case the unresolved bucket exists for.
    [Fact]
    public async Task ACurrentlyAiringAnimeWithNoTotalAndNoAiredCountIsUnresolved()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
            [Entry(1, totalEpisodes: null, episodesWatched: 3, airingStatus: "currently_airing")];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(1, profile.EpisodeProgress.UnresolvedEntries);
        Assert.Single(profile.EpisodeProgress.UnresolvedAnime);
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

    private sealed class FakeEpisodeScheduleService(Dictionary<int, int> airedCounts) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(anime
                .Where(a => airedCounts.ContainsKey(a.Id))
                .ToDictionary(a => a.Id, a => airedCounts[a.Id]));
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(animeIds
                .Where(airedCounts.ContainsKey)
                .ToDictionary(id => id, id => airedCounts[id]));
    }

    // Not touched by BuildEpisodeProgress — a stub returning the empty
    // ranking is enough (tier-season-refresh-and-top-series-order task 4.7).
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
