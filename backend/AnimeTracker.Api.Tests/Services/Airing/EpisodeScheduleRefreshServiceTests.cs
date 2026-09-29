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
    private static AnimeTrackerDbContext CreateDb() => AiringRefreshTestKit.CreateDb();

    private static readonly IBroadcastLocalTimeConverter LocalTime = AiringRefreshTestKit.LocalTime;

    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    // Noon UTC, comfortably clear of the Europe/Helsinki local-midnight
    // boundary in either direction, so a +/- few-hour shift never crosses a
    // local calendar day by accident.
    private static DateTimeOffset AtNoonUtc(DateOnly date) => new(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static EpisodeScheduleRefreshService CreateService(
        AnimeTrackerDbContext db, FakeAniListClient aniList, InMemoryEpisodeAiringRepository episodeAiringRepository) =>
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
        var newAirsAt = AtNoonUtc(TodayUtc.AddDays(10));
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([new AniListEpisode(1, newAirsAt)], "NOT_YET_RELEASED", newAirsAt, null) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);

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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
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

        var episodeAiringRepository = new InMemoryEpisodeAiringRepository(db);
        var aniList = new FakeAniListClient { Schedule = new AniListScheduleResult([], "RELEASING", null, 12) };
        var service = CreateService(db, aniList, episodeAiringRepository);

        await service.RefreshOneAsync(1);
        await service.RefreshOneAsync(1);

        Assert.Single(await db.AnimeUpdates.ToListAsync());
    }

    // --- The first-fetch rule (anime-updates, "Writing ... for the first time SHALL
    // record nothing"): an anime's first airing fetch, the one that gives it its
    // airing-fetched mark, records no release and no move. ---

    [Fact]
    public async Task AFirstEverAniListFetchRecordsNoEpisodeCountRelease()
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1); // no airing sync row: never looked up, no MAL total
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 3, status: "RELEASING", episodes: 12);

        await AiringRefreshTestKit.CreateService(db, aniList).RefreshOneAsync(1);

        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(12, anime.TotalEpisodes); // the total is still stored,
        Assert.Empty(await db.AnimeUpdates.ToListAsync()); // it is just not news
    }

    [Fact]
    public async Task ALaterFetchThatFillsAnUnknownTotalIsNews()
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1, fetchedAgo: TimeSpan.FromDays(1), aniListId: 901); // already has its mark
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 3, status: "RELEASING", episodes: 12);

        await AiringRefreshTestKit.CreateService(db, aniList).RefreshOneAsync(1);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    [Fact]
    public async Task AFirstFetchRecordsNoMoveEvenWhenRowsAreAlreadyStored()
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1); // rows below, but no airing-fetched mark
        db.EpisodeAirings.Add(new EpisodeAiring
        {
            AnimeId = 1, Episode = 5, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(10)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var aniList = new ScriptedAniListClient();
        var media = aniList.Add(1, status: "RELEASING");
        media.Rows = [new AniListEpisode(5, AtNoonUtc(TodayUtc.AddDays(17)))];

        await AiringRefreshTestKit.CreateService(db, aniList).RefreshOneAsync(1);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    // --- episode-airing-data "AniList request pacing": an anime AniList doesn't
    // know is looked up again by automatic refreshes only once its record is 90
    // days old, and by a refresh started by hand at any age. ---

    [Theory]
    [InlineData(89, false)]
    [InlineData(91, true)]
    public async Task AnAnimeUnknownToAniListIsLookedUpAgainByAnAutomaticRefreshOnlyAfter90Days(int days, bool lookedUp)
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(days), aniListId: null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2); // AniList knows it now

        await AiringRefreshTestKit.CreateService(db, aniList).RefreshOneAsync(1);

        Assert.Equal(lookedUp, aniList.SingleLookups.Count == 1);
        var sync = await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 1);
        Assert.Equal(lookedUp ? 901 : null, sync.AniListId);
    }

    [Fact]
    public async Task ARefreshStartedByHandLooksAnUnknownAnimeUpAgainWhateverItsAge()
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(2), aniListId: null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2);

        await AiringRefreshTestKit.CreateService(db, aniList).RefreshOneAsync(1, relookupAbsent: true);

        Assert.Equal([1], aniList.SingleLookups);
        Assert.Equal(901, (await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 1)).AniListId);
    }

    [Fact]
    public async Task AStillUnknownAnimeGetsAFreshRecordOfTheLookThatKeepsTheNext90DaysQuiet()
    {
        using var db = CreateDb();
        await AiringRefreshTestKit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(120), aniListId: null);
        var aniList = new ScriptedAniListClient(); // AniList still has nothing for MAL id 1
        var service = AiringRefreshTestKit.CreateService(db, aniList);

        await service.RefreshOneAsync(1);
        await service.RefreshOneAsync(1);

        Assert.Equal([1], aniList.SingleLookups); // the second call found the record fresh
        var sync = await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 1);
        Assert.Null(sync.AniListId);
        Assert.InRange(sync.LastFetchedAt!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    private sealed class FakeAniListClient : IAniListClient
    {
        public AniListMediaLookup? Lookup { get; set; }
        public AniListScheduleResult Schedule { get; set; } = new([], null, null, null);
        public Dictionary<int, Exception> ThrowForAniListId { get; } = new();
        public Dictionary<int, Exception> ThrowForMalId { get; } = new();

        public Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default) =>
            ThrowForMalId.TryGetValue(malId, out var ex) ? throw ex
            : Lookup is not null ? Task.FromResult<AniListMediaLookup?>(Lookup) : throw new NotImplementedException();

        public Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default) =>
            ThrowForAniListId.TryGetValue(aniListId, out var ex) ? throw ex : Task.FromResult(Schedule);

        public Task<IReadOnlyDictionary<int, AniListRelationsLookup>> GetRelationsBatchAsync(IReadOnlyList<int> malIds, CancellationToken ct = default) =>
            throw new NotImplementedException();

        // These tests are about the single-anime path; the batched one is in
        // EpisodeScheduleRefreshBatchTests, over a scripted client.
        public Task<IReadOnlyDictionary<int, AniListMediaLookup>> LookupBatchByMalIdsAsync(IReadOnlyList<int> malIds, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyDictionary<int, AniListMediaState>> GetMediaBatchAsync(IReadOnlyList<int> aniListIds, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<int>> GetAiringSchedulesAsync(
            IReadOnlyList<int> aniListIds, Func<int, IReadOnlyList<AniListEpisode>, Task> onAnimeComplete, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
