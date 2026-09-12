using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Tests.Services.Library;

// episode-airing-data / fix-score-reveal-and-nplus1 tasks.md 4.1: MyListService
// must resolve every entry's aired-so-far count through the bulk read
// (design.md D6), never per entry.
public class MyListServiceBulkReadTests
{
    [Fact]
    public async Task TheReadSucceedsAndEveryRowCarriesTheAiredCountTheBulkResultGaveIt()
    {
        List<UserAnimeEntry> entries =
        [
            new() { AnimeId = 1, Status = WatchStatus.Watching, Anime = new AnimeMetadata { Id = 1, Title = "One" } },
            new() { AnimeId = 2, Status = WatchStatus.Watching, Anime = new AnimeMetadata { Id = 2, Title = "Two" } },
        ];
        var scheduleService = new PoisonedEpisodeScheduleService(new Dictionary<int, int> { [1] = 4, [2] = 9 });

        var service = new MyListService(
            new FakeUserAnimeEntryRepository(entries),
            new FakeTopAnimeSelectionRepository(),
            scheduleService,
            new NoopAiringWatchStatusService());

        var list = await service.GetMyListAsync();

        Assert.Equal(4, list.Single(i => i.AnimeId == 1).EpisodesAired);
        Assert.Equal(9, list.Single(i => i.AnimeId == 2).EpisodesAired);
        Assert.Equal(1, scheduleService.BulkCallCount);
    }

    // Throws on every per-anime or wrong-shaped member; only the
    // AnimeMetadata-collection bulk overload answers. The only way this test
    // passes is if MyListService reads aired counts through that one call.
    private sealed class PoisonedEpisodeScheduleService(Dictionary<int, int> bulkResult) : IEpisodeScheduleService
    {
        public int BulkCallCount { get; private set; }

        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new InvalidOperationException("My list must never resolve a single episode.");
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("My list must never fetch a next-airing instant.");
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("My list must use the bulk aired-count read, not the per-anime one.");
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default)
        {
            BulkCallCount++;
            return Task.FromResult(bulkResult);
        }
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("My list must use the AnimeMetadata overload, not the id one.");
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class NoopAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
