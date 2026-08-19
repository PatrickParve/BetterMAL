using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// BuildEpisodeProgress (profile-stats "All-list episode progress", design.md
// decision 5/task 6.2). GetProfileAsync doesn't touch SeriesRankingLookup
// itself, but the constructor needs one, so these tests follow
// ProfileServiceTopSeriesTests' construction with an empty in-memory db
// standing in for it.
public class ProfileServiceEpisodeProgressTests
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

    private static UserAnimeEntry Entry(
        int animeId, int? totalEpisodes, int episodesWatched,
        WatchStatus status = WatchStatus.Watching, int rewatchCount = 0) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes },
            Status = status,
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
        };

    [Fact]
    public async Task UnknownTotalEntryExcludedFromCountedFiguresButPresentInTotalEntries()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, totalEpisodes: 12, episodesWatched: 12, status: WatchStatus.Completed),
            Entry(2, totalEpisodes: null, episodesWatched: 5), // still airing / unknown length
        ];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(12, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(12, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(1, profile.EpisodeProgress.EntriesCounted);
        Assert.Equal(2, profile.EpisodeProgress.TotalEntries);
    }

    [Fact]
    public async Task PlanToWatchEntryContributesTotalAndNoWatched()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1, totalEpisodes: 24, episodesWatched: 0, status: WatchStatus.PlanToWatch)];

        var profile = await CreateService(db, entries).GetProfileAsync();

        Assert.Equal(0, profile.EpisodeProgress.EpisodesWatched);
        Assert.Equal(24, profile.EpisodeProgress.EpisodesTotal);
        Assert.Equal(1, profile.EpisodeProgress.EntriesCounted);
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
