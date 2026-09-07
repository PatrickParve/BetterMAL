using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The guarantee the whole Series page rests on (design.md D1/Risks, spec
// "The endpoint makes no MAL calls" / "A partial or truncated series shows
// figures computed over the members it has"): SeriesListService depends on
// SeriesRankingLookup (a plain read), IEpisodeScheduleService's batched
// id overload (also a plain read), and IAnimeRankingService's snapshot read
// (polish-search-sort-and-titles design.md D7 — also a plain read, over the
// same in-memory context, via the real AnimeRankingService so this test
// proves it rather than assuming it) alone — no ISeriesService, no graph
// builder, no MAL client — so a partial series still resolves, and the
// schedule reader is called at most once, batched over currently-airing
// main-line ids, never per member.
public class SeriesListNoBuildOrMalCallTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Throws on anything but the batched id overload, and fails the call
    // outright if it's invoked more than once — the only way this test would
    // pass is if SeriesListService reads aired counts in a single batch.
    private sealed class PoisonedEpisodeScheduleService : IEpisodeScheduleService
    {
        public int CallCount { get; private set; }

        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new InvalidOperationException("The Series page must never resolve a single episode.");
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The Series page must never fetch a next-airing instant.");
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The Series page must never resolve a per-anime aired count.");
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new InvalidOperationException("The Series page must use the id-taking overload, not the entity one.");
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new Dictionary<int, int> { [animeIds.Single()] = 5 });
        }
    }

    [Fact]
    public async Task PartialSeries_ResolvesWithoutBuildingOrCallingMalAndBatchesTheScheduleRead()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow, IsPartial = true });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Partial", AiringStatus = "currently_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 100, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 100, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        await db.SaveChangesAsync();

        var scheduleService = new PoisonedEpisodeScheduleService();
        var service = new SeriesListService(new SeriesRankingLookup(db), scheduleService, new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)));

        var result = await service.GetSeriesListAsync();

        var listed = Assert.Single(result.Items);
        Assert.Equal(100, listed.SeriesId);
        Assert.Equal(1, scheduleService.CallCount); // one batched call, not one per member
    }

    [Fact]
    public async Task NoCurrentlyAiringMembers_NeverCallsTheScheduleReaderAtAll()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Finished", AiringStatus = "finished_airing", TotalEpisodes = 12 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 100, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 100, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var scheduleService = new PoisonedEpisodeScheduleService();
        var service = new SeriesListService(new SeriesRankingLookup(db), scheduleService, new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)));

        await service.GetSeriesListAsync();

        Assert.Equal(0, scheduleService.CallCount);
    }
}
