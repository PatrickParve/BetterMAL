using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// EpisodeScheduleRefreshService moved-episode detection (anime-updates spec,
// tasks 4.1-4.3): diffs an anime's stored per-episode air dates against the
// incoming AniList set before ReplaceForAnimeAsync overwrites them, comparing
// local calendar days and considering only episodes unaired as of now.
public class EpisodeScheduleRefreshServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly IBroadcastLocalTimeConverter LocalTime = new BroadcastLocalTimeConverter();

    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    // Noon UTC, comfortably clear of the Europe/Helsinki local-midnight
    // boundary in either direction, so a +/- few-hour shift never crosses a
    // local calendar day by accident.
    private static DateTimeOffset AtNoonUtc(DateOnly date) => new(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static EpisodeScheduleRefreshService CreateService(
        AnimeTrackerDbContext db, FakeAniListClient aniList, FakeEpisodeAiringRepository episodeAiringRepository) =>
        new(
            db,
            new UserAnimeEntryRepository(db),
            episodeAiringRepository,
            aniList,
            new AniListRelationStore(db),
            LocalTime,
            new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))),
            NullLogger<EpisodeScheduleRefreshService>.Instance);

    private static async Task SeedAnimeAsync(AnimeTrackerDbContext db, int animeId, string airingStatus)
    {
        db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", AiringStatus = airingStatus });
        // Every test below shares this seed, and most of them assert an
        // update IS recorded (scope-updates-to-my-list tasks 6.1): the new
        // relevance gate needs a non-Dropped list entry for that to still
        // happen, whatever else the individual test is exercising.
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = WatchStatus.Watching });
        // AniListId/LastFetchedAt already set: RefreshOneCoreAsync skips the
        // AniList lookup branch and goes straight to the schedule fetch.
        db.AnimeAiringSyncs.Add(new AnimeAiringSync
        {
            AnimeId = animeId,
            AniListId = 900 + animeId,
            LastFetchedAt = DateTimeOffset.UtcNow.AddDays(-1),
            RelationsFetchedAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ANextEpisodeDelayRecords()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        var oldAirsAt = AtNoonUtc(TodayUtc.AddDays(10));
        var newAirsAt = AtNoonUtc(TodayUtc.AddDays(17));
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = oldAirsAt, FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([new AniListEpisode(5, newAirsAt)], "RELEASING", newAirsAt, null) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodesMoved, update.Kinds);
        Assert.Equal(5, update.MovedEpisode);
        Assert.Equal(LocalTime.GetLocalDate(oldAirsAt), update.PreviousEpisodeDate);
        Assert.Equal(LocalTime.GetLocalDate(newAirsAt), update.NewEpisodeDate);
    }

    [Fact]
    public async Task ASameLocalDayShiftOfMinutesRecordsNothing()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        var oldAirsAt = AtNoonUtc(TodayUtc.AddDays(10));
        var newAirsAt = oldAirsAt.AddMinutes(20);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = oldAirsAt, FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([new AniListEpisode(5, newAirsAt)], "RELEASING", newAirsAt, null) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task APastEpisodesDateChangeRecordsNothing()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        var oldAirsAt = AtNoonUtc(TodayUtc.AddDays(-10)); // already aired
        var newAirsAt = AtNoonUtc(TodayUtc.AddDays(-3));
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 3, AirsAtUtc = oldAirsAt, FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([new AniListEpisode(3, newAirsAt)], "RELEASING", null, null) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task ThreeEpisodesMovingRecordOneUpdateNamingTheEarliest()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        var week = TimeSpan.FromDays(7);
        db.EpisodeAirings.AddRange(
            new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(10)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new EpisodeAiring { AnimeId = 1, Episode = 6, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(17)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new EpisodeAiring { AnimeId = 1, Episode = 7, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(24)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient
        {
            Schedule = new AniListScheduleResult(
                [
                    new AniListEpisode(5, AtNoonUtc(TodayUtc.AddDays(10)) + week),
                    new AniListEpisode(6, AtNoonUtc(TodayUtc.AddDays(17)) + week),
                    new AniListEpisode(7, AtNoonUtc(TodayUtc.AddDays(24)) + week),
                ],
                "RELEASING", AtNoonUtc(TodayUtc.AddDays(17)) + week, null),
        };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodesMoved, update.Kinds);
        Assert.Equal(5, update.MovedEpisode);
    }

    [Fact]
    public async Task AFirstEverFetchRecordsNothing()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "not_yet_aired"); // no prior EpisodeAiring rows at all

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var newAirsAt = AtNoonUtc(TodayUtc.AddDays(10));
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([new AniListEpisode(1, newAirsAt)], "NOT_YET_RELEASED", newAirsAt, null) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task TheBackfillPathOverAFinishedShowRecordsNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "finished_airing" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Completed });
        var oldAirsAt = AtNoonUtc(TodayUtc.AddDays(-400));
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 1, AirsAtUtc = oldAirsAt, FetchedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        // No AnimeAiringSync row yet: the backfill's first pass over this
        // anime, exercising the AniList lookup branch too.
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var newAirsAt = AtNoonUtc(TodayUtc.AddDays(-395)); // MAL/AniList tidying old history
        var aniList = new FakeAniListClient
        {
            Lookup = new AniListMediaLookup(999, "FINISHED", null, [], null),
            Schedule = new AniListScheduleResult([new AniListEpisode(1, newAirsAt)], "FINISHED", null, null),
        };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.BackfillAsync();

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    // design.md D9/D10 (refine-sync-status-and-episode-totals tasks.md
    // 7.1-7.3/9.8): AniList's total fills an unknown MAL total, MAL still wins
    // when it has one, and the fill records exactly one EpisodeCountReleased.
    [Fact]
    public async Task AnAniListTotalFillsAnUnknownMalTotalAndRecordsOneEpisodeCountReleased()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing"); // MalTotalEpisodes/TotalEpisodes both null

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([], "RELEASING", null, 12) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(12, anime.AniListTotalEpisodes);
        Assert.Equal(12, anime.TotalEpisodes);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    [Fact]
    public async Task AKnownMalTotalWinsOverAniListsAndRecordsNoUpdate()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        var anime = await db.AnimeMetadata.SingleAsync(a => a.Id == 1);
        anime.MalTotalEpisodes = 24;
        anime.ResolveTotalEpisodes();
        await db.SaveChangesAsync();

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([], "RELEASING", null, 12) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

        var stored = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(12, stored.AniListTotalEpisodes); // stored regardless
        Assert.Equal(24, stored.TotalEpisodes); // MAL still wins

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task ASecondRefreshOverAnAlreadyKnownTotalRecordsNoSecondUpdate()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([], "RELEASING", null, 12) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);
        await service.RefreshOneAsync(1);

        Assert.Single(await db.AnimeUpdates.ToListAsync());
    }

    // report-partial-runs-and-mal-side-removals tasks 1.1-1.2 (design D1):
    // RefreshManyAsync splits "no data" from "threw" instead of lumping both
    // into one zeroRows count.
    [Fact]
    public async Task RefreshManyCountsNoDataAndFailedSeparately()
    {
        using var db = CreateDb();
        await SeedAnimeAsync(db, 1, "currently_airing");
        await SeedAnimeAsync(db, 2, "currently_airing");

        var episodeAiringRepository = new FakeEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([], "RELEASING", null, null) };
        aniList.ThrowForAniListId[902] = new HttpRequestException("AniList is unreachable"); // anime 2's cached AniListId (900 + animeId)
        var service = CreateService(db, aniList, episodeAiringRepository);

        var processed = new List<int>();
        var result = await service.RefreshManyAsync([1, 2], onProgress: processed.Add);

        Assert.Equal(1, result.NoData);
        Assert.Equal(1, result.Failed);
        Assert.Equal([1, 2], processed);
    }

    private sealed class FakeAniListClient : IAniListClient
    {
        public AniListMediaLookup? Lookup { get; set; }
        public AniListScheduleResult Schedule { get; set; } = new([], null, null, null);
        public Dictionary<int, Exception> ThrowForAniListId { get; } = new();

        public Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default) =>
            Lookup is not null ? Task.FromResult<AniListMediaLookup?>(Lookup) : throw new NotImplementedException();

        public Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default) =>
            ThrowForAniListId.TryGetValue(aniListId, out var ex) ? throw ex : Task.FromResult(Schedule);

        public Task<IReadOnlyDictionary<int, AniListRelationsLookup>> GetRelationsBatchAsync(IReadOnlyList<int> malIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    // Mirrors the real repository's replace-wholesale behaviour without a
    // database transaction, which the in-memory provider doesn't support.
    private sealed class FakeEpisodeAiringRepository(AnimeTrackerDbContext db) : IEpisodeAiringRepository
    {
        public Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<int, DateTimeOffset>> GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default)
        {
            if (rows.Count == 0)
                return;

            var existing = await db.EpisodeAirings.Where(e => e.AnimeId == animeId).ToListAsync(ct);
            db.EpisodeAirings.RemoveRange(existing);
            db.EpisodeAirings.AddRange(rows);
            await db.SaveChangesAsync(ct);
        }
    }
}
