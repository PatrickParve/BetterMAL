using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// anime-ranking capability read into "My top anime" (design.md D2/D8,
// tasks.md 3.1-3.3): dropped/short-form members band to the bottom of their
// tier, rows carry the anime's overall rank, and the editor's IncludedCount
// is expressed against listed (hand-orderable) members only.
public class ProfileServiceTopAnimeRankingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries, List<int>? orderedAnimeIds = null) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(orderedAnimeIds ?? []),
            new SeriesRankingLookup(db),
            new FakeSeriesBuildTrigger(),
            new FakeEpisodeScheduleService());

    private static UserAnimeEntry Entry(
        int animeId, string title, int? myScore, WatchStatus status = WatchStatus.Completed, string? mediaType = "tv") =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = title, MediaType = mediaType, AiringStatus = "finished_airing" },
            Status = status,
            MyScore = myScore,
        };

    [Fact]
    public async Task DroppedAndShortFormMembersSinkToTheBottomOfTheirTierAndAreNotListedInTheEditor()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Hand ordered", myScore: 8),
            Entry(2, "Dropped", myScore: 8, status: WatchStatus.Dropped),
            Entry(3, "Music", myScore: 8, mediaType: "music"),
        ];

        var section = await CreateService(db, entries).GetTopAnimeSectionAsync(TopAnimeMediaTypeScope.All);

        var tier = Assert.Single(section.Tiers);
        Assert.Equal([1], tier.Members.Select(m => m.AnimeId));
        Assert.Equal([1, 3, 2], section.Items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task RowsCarryTheirOverallRank()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Ten", myScore: 10),
            Entry(2, "Nine", myScore: 9),
        ];

        var section = await CreateService(db, entries).GetTopAnimeSectionAsync(TopAnimeMediaTypeScope.All);

        Assert.Equal(1, section.Items.Single(i => i.AnimeId == 1).MyRank);
        Assert.Equal(2, section.Items.Single(i => i.AnimeId == 2).MyRank);
    }

    [Fact]
    public async Task AScoredPlanToWatchAnimeIsAbsentEntirely()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries =
        [
            Entry(1, "Scored PTW", myScore: 8, status: WatchStatus.PlanToWatch),
        ];

        var section = await CreateService(db, entries).GetTopAnimeSectionAsync(TopAnimeMediaTypeScope.All);

        Assert.Empty(section.Items);
        Assert.Empty(section.Tiers);
    }

    [Fact]
    public async Task IncludedCountIsCappedToListedMembersWhenTheCutFallsAmongPinnedOnes()
    {
        using var db = CreateDb();
        // Eight 10s fill 8 of the 10 slots, leaving 2 for the 9-tier below,
        // whose only hand-orderable member is followed by two dropped ones —
        // so the raw cut (2 slots) reaches past the single listed member and
        // into the pinned ones.
        var entries = new List<UserAnimeEntry>();
        for (var i = 1; i <= 8; i++)
            entries.Add(Entry(i, $"Ten {i:00}", myScore: 10));
        entries.Add(Entry(20, "Nine hand ordered", myScore: 9));
        entries.Add(Entry(21, "Nine dropped A", myScore: 9, status: WatchStatus.Dropped));
        entries.Add(Entry(22, "Nine dropped B", myScore: 9, status: WatchStatus.Dropped));

        var section = await CreateService(db, entries).GetTopAnimeSectionAsync(TopAnimeMediaTypeScope.All);

        var nineTier = section.Tiers.Single(t => t.Score == 9);
        Assert.Equal([20], nineTier.Members.Select(m => m.AnimeId));
        Assert.Equal(1, nineTier.IncludedCount);
        Assert.Equal(nineTier.Members.Count, nineTier.IncludedCount);
        // The top list itself still includes the dropped anime that fit.
        Assert.Contains(21, section.Items.Select(i => i.AnimeId));
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
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository(List<int> orderedAnimeIds) : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(orderedAnimeIds);
        public Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public void Enqueue(int animeId) { }
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
}
