using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Library;

// add-genre-and-studio-stats (library-views "My list studio filter"): each my-list row
// carries the studios MyAnimeList credits the anime with, in its order, and never null,
// so the client's studio filter runs over the loaded list and reads `.length`. The rows
// come through the real repository's lean projection, so these also fail if `Studios`
// ever leaves GetAllForListViewAsync's field list. The same shape as the genre facts.
public class MyListServiceStudiosTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MyListService BuildService(AnimeTrackerDbContext db) => new(
        new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db), new FakeEpisodeScheduleService(), new NoopAiringWatchStatusService());

    [Fact]
    public async Task StudiosPassThroughInMyAnimeListsOrder()
    {
        using var db = CreateDb();
        // A co-production, deliberately not alphabetical: the order is MyAnimeList's,
        // and the client sorts the board itself.
        var anime = new AnimeMetadata
        {
            Id = 1, Title = "Co-produced", AiringStatus = "finished_airing",
            Studios = ["Wit Studio", "Aniplex", "MAPPA"],
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        Assert.Equal(["Wit Studio", "Aniplex", "MAPPA"], list.Single().Studios);
    }

    [Fact]
    public async Task EachRowCarriesItsOwnAnimesStudios()
    {
        using var db = CreateDb();
        var first = new AnimeMetadata { Id = 1, Title = "First", AiringStatus = "finished_airing", Studios = ["Madhouse"] };
        var second = new AnimeMetadata { Id = 2, Title = "Second", AiringStatus = "finished_airing", Studios = ["Bones", "Sunrise"] };
        db.AnimeMetadata.AddRange(first, second);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = first, Status = WatchStatus.Completed },
            new UserAnimeEntry { AnimeId = 2, Anime = second, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        Assert.Equal(["Madhouse"], list.Single(i => i.AnimeId == 1).Studios);
        Assert.Equal(["Bones", "Sunrise"], list.Single(i => i.AnimeId == 2).Studios);
    }

    [Fact]
    public async Task AStoredNullArrivesAsAnEmptyListNotNull()
    {
        using var db = CreateDb();
        // An anime whose full details were never fetched has no stored studios (null); one
        // fetched with none stores an empty list. Both reach the client as [].
        var neverFetched = new AnimeMetadata { Id = 1, Title = "Never Fetched", AiringStatus = "finished_airing", Studios = null };
        var fetchedWithNone = new AnimeMetadata { Id = 2, Title = "Fetched With None", AiringStatus = "finished_airing", Studios = [] };
        db.AnimeMetadata.AddRange(neverFetched, fetchedWithNone);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = neverFetched, Status = WatchStatus.Completed },
            new UserAnimeEntry { AnimeId = 2, Anime = fetchedWithNone, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        var never = list.Single(i => i.AnimeId == 1).Studios;
        Assert.NotNull(never);
        Assert.Empty(never);
        var none = list.Single(i => i.AnimeId == 2).Studios;
        Assert.NotNull(none);
        Assert.Empty(none);
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult<ResolvedEpisode?>(null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class NoopAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
