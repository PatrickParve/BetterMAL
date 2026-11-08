using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Library;

// add-genre-filter-and-polish-lists (design D5, tasks.md 1.3): each my-list row
// carries the anime's stored genres, in MyAnimeList's order, and never null —
// the client's genre filter runs over the loaded list and reads `.length`. The
// rows come through the real repository's lean projection, so these also fail
// if `Genres` ever leaves GetAllForListViewAsync's field list.
public class MyListServiceGenresTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MyListService BuildService(AnimeTrackerDbContext db) => new(
        new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db), new FakeEpisodeScheduleService(), new NoopAiringWatchStatusService());

    [Fact]
    public async Task GenresPassThroughInMyAnimeListsOrder()
    {
        using var db = CreateDb();
        // Deliberately not alphabetical: the order is MyAnimeList's, and the
        // client sorts the board itself.
        var anime = new AnimeMetadata
        {
            Id = 1, Title = "Genred", AiringStatus = "finished_airing",
            Genres = ["Romance", "Action", "Slice of Life", "Comedy"],
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        Assert.Equal(["Romance", "Action", "Slice of Life", "Comedy"], list.Single().Genres);
    }

    [Fact]
    public async Task EachRowCarriesItsOwnAnimesGenres()
    {
        using var db = CreateDb();
        var first = new AnimeMetadata { Id = 1, Title = "First", AiringStatus = "finished_airing", Genres = ["Drama"] };
        var second = new AnimeMetadata { Id = 2, Title = "Second", AiringStatus = "finished_airing", Genres = ["Sports", "Seinen"] };
        db.AnimeMetadata.AddRange(first, second);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = first, Status = WatchStatus.Completed },
            new UserAnimeEntry { AnimeId = 2, Anime = second, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        Assert.Equal(["Drama"], list.Single(i => i.AnimeId == 1).Genres);
        Assert.Equal(["Sports", "Seinen"], list.Single(i => i.AnimeId == 2).Genres);
    }

    [Fact]
    public async Task AStoredNullArrivesAsAnEmptyListNotNull()
    {
        using var db = CreateDb();
        // An anime whose full details were never fetched has no stored genres
        // (null); one fetched with none stores an empty list. Both reach the
        // client as [] so it never has to guard `.length`.
        var neverFetched = new AnimeMetadata { Id = 1, Title = "Never Fetched", AiringStatus = "finished_airing", Genres = null };
        var fetchedWithNone = new AnimeMetadata { Id = 2, Title = "Fetched With None", AiringStatus = "finished_airing", Genres = [] };
        db.AnimeMetadata.AddRange(neverFetched, fetchedWithNone);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = neverFetched, Status = WatchStatus.Completed },
            new UserAnimeEntry { AnimeId = 2, Anime = fetchedWithNone, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        var never = list.Single(i => i.AnimeId == 1).Genres;
        Assert.NotNull(never);
        Assert.Empty(never);
        var none = list.Single(i => i.AnimeId == 2).Genres;
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
