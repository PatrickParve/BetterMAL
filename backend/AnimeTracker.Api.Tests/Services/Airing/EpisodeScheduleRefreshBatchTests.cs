using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Updates;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Kit = AnimeTracker.Api.Tests.Services.Airing.AiringRefreshTestKit;

namespace AnimeTracker.Api.Tests.Services.Airing;

// episode-airing-data "A pass over many anime is batched" and "Refreshing all airing
// dates skips only shows whose history is complete", add-first-run-setup design
// D10, D15 and D16. Everything runs over ScriptedAniListClient, which answers the
// single and the batched methods from one script, so the two paths can be compared.
public class EpisodeScheduleRefreshBatchTests
{
    private static DateTimeOffset AtNoonUtc(DateOnly date) => new(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    private static DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);

    // HttpClient reports its own timeout as a TaskCanceledException while the
    // caller's token is still live.
    private static TaskCanceledException HttpClientTimeout() =>
        new("The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException());

    private static async Task<RefreshBatchResult> RefreshAsync(
        EpisodeScheduleRefreshService service, IReadOnlyList<int> ids, List<(int Id, AiringRefreshOutcome Outcome)>? done = null,
        bool relookupAbsent = false, CancellationToken ct = default) =>
        await service.RefreshBatchAsync(
            ids, new RefreshBatchOptions(relookupAbsent, done is null ? null : (id, outcome) => done.Add((id, outcome))), ct);

    // --- batch sizes ---

    [Fact]
    public async Task TwentySixAnimeNeverLookedUpAreResolvedInLookupBatchesOf25AndOne()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        var ids = Enumerable.Range(1, 26).ToList();
        foreach (var id in ids)
        {
            await Kit.SeedAsync(db, id);
            aniList.Add(id, rows: 2);
        }

        var result = await RefreshAsync(Kit.CreateService(db, aniList), ids);

        Assert.Equal([25, 1], aniList.LookupBatches.Select(b => b.Length));
        Assert.Empty(aniList.SingleLookups);
        Assert.Equal(26, result.Fetched);
        Assert.Equal(26, await db.AnimeAiringSyncs.CountAsync(s => s.LastFetchedAt != null));
    }

    [Fact]
    public async Task FiftyOneAnimeWithAStoredIdAreReadInMediaBatchesOf50AndOne()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        var ids = Enumerable.Range(1, 51).ToList();
        foreach (var id in ids)
        {
            await Kit.SeedAsync(db, id, fetchedAgo: TimeSpan.FromDays(1), aniListId: 900 + id);
            aniList.Add(id, rows: 1, status: "FINISHED", episodes: 12);
        }

        var result = await RefreshAsync(Kit.CreateService(db, aniList), ids);

        Assert.Equal([50, 1], aniList.MediaBatches.Select(b => b.Length));
        Assert.Empty(aniList.LookupBatches); // an anime whose id is stored is never looked up
        Assert.Equal(51, result.Fetched);
        // The media read carried each total over.
        Assert.All(await db.AnimeMetadata.AsNoTracking().ToListAsync(), a => Assert.Equal(12, a.AniListTotalEpisodes));
    }

    [Fact]
    public async Task FourShortShowsShareOneScheduleRead()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        foreach (var id in new[] { 1, 2, 3, 4 })
        {
            await Kit.SeedAsync(db, id, "finished_airing");
            aniList.Add(id, rows: 12);
        }

        await RefreshAsync(Kit.CreateService(db, aniList), [1, 2, 3, 4]);

        Assert.Single(aniList.ScheduleReads);
        Assert.Equal(4, aniList.ScheduleReads[0].Length);
        Assert.Equal(48, await db.EpisodeAirings.CountAsync());
    }

    [Fact]
    public async Task ARepeatedIdIsFetchedOnce()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);
        aniList.Add(1, rows: 3);
        var done = new List<(int, AiringRefreshOutcome)>();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1, 1, 1], done);

        Assert.Equal([1], aniList.LookupBatches.Single());
        Assert.Equal(1, result.Fetched);
        Assert.Single(done);
    }

    // --- what a lookup's answer means ---

    [Fact]
    public async Task AMalIdMissingFromALookupAnswerIsRecordedAsUnknownToAniList()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        var ids = Enumerable.Range(1, 25).ToList();
        foreach (var id in ids)
        {
            await Kit.SeedAsync(db, id);
            if (id != 25)
                aniList.Add(id, rows: 2); // AniList answers with 24
        }
        var done = new List<(int Id, AiringRefreshOutcome Outcome)>();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), ids, done);

        var sync = await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 25);
        Assert.Null(sync.AniListId);
        Assert.NotNull(sync.LastFetchedAt); // the airing-fetched mark: "looked up, and confirmed absent"
        Assert.NotNull(sync.RelationsFetchedAt);
        Assert.Equal(24, result.Fetched);
        Assert.Equal(1, result.NoData);
        Assert.Contains((25, AiringRefreshOutcome.NoData), done);
    }

    [Fact]
    public async Task ARelationRidesAlongOnALookupAndIsStoredWithTheAnime()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);
        aniList.Add(1, rows: 2).Relations = [new AniListRelationEdge("SEQUEL", 2), new AniListRelationEdge("ADAPTATION", 99)];

        await RefreshAsync(Kit.CreateService(db, aniList), [1]);

        var relation = Assert.Single(await db.AniListRelations.AsNoTracking().ToListAsync());
        Assert.Equal((1, 2, "SEQUEL"), (relation.AnimeId, relation.RelatedAnimeId, relation.RelationType));
    }

    [Fact]
    public async Task AnAnimeThatIsGoneReportsNoDataAndCostsNoRequest()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [404]);

        Assert.Equal(new RefreshBatchResult(0, 1, 0), result);
        Assert.Empty(aniList.LookupBatches);
    }

    // --- schedules ---

    [Fact]
    public async Task ZeroScheduleRowsKeepTheStoredRowsButStillRecordWhatWasLearned()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1, fetchedAgo: TimeSpan.FromDays(1), aniListId: 901);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 3, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(-30)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-5) });
        await db.SaveChangesAsync();
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 0, status: "FINISHED", episodes: 12); // AniList has the show, and no rows for it

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1]);

        Assert.Equal(1, result.NoData);
        var row = Assert.Single(await db.EpisodeAirings.AsNoTracking().ToListAsync());
        Assert.Equal(3, row.Episode); // stored rows untouched
        Assert.Equal(12, (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).AniListTotalEpisodes);
        Assert.True((await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 1)).LastFetchedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ALongRunnerOf1100RowsPagesThroughAndIsStoredWhole()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);
        await Kit.SeedAsync(db, 2, "finished_airing");
        aniList.Add(1, rows: 1100, status: "RELEASING");
        aniList.Add(2, rows: 12);

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1, 2]);

        Assert.Equal(2, result.Fetched);
        Assert.Equal(1100, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1));
        Assert.Equal(12, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 2));
    }

    [Fact]
    public async Task AnAnimeIsSavedAsSoonAsItsRowsAreComplete_NotWhenTheWholeReadEnds()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        foreach (var id in new[] { 1, 2 })
        {
            await Kit.SeedAsync(db, id, "finished_airing");
            aniList.Add(id, rows: 6);
        }

        // Just before the second anime is handed over, the first is already in the database.
        var storedBeforeSecond = -1;
        aniList.BeforeAnimeComplete = async aniListId =>
        {
            if (aniListId == 902)
                storedBeforeSecond = await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1);
        };

        await RefreshAsync(Kit.CreateService(db, aniList), [1, 2]);

        Assert.Equal(6, storedBeforeSecond);
    }

    [Fact]
    public async Task AReadThatHitsThePageCapSavesTheCompleteAnimeAndReadsTheRestAgain()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient { PageSize = 50, PageCap = 4 }; // 200 rows a read
        await Kit.SeedAsync(db, 1, "finished_airing");
        await Kit.SeedAsync(db, 2, "finished_airing");
        await Kit.SeedAsync(db, 3, "finished_airing");
        aniList.Add(1, rows: 10);
        aniList.Add(2, rows: 150);
        aniList.Add(3, rows: 100); // rows 161 to 260 of the read: cut off at 200
        var done = new List<(int Id, AiringRefreshOutcome Outcome)>();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1, 2, 3], done);

        // The first read saved 1 and 2; 3 came back incomplete and was read again, alone.
        Assert.Equal([[901, 902, 903], [903]], aniList.ScheduleReads.Select(r => r.OrderBy(x => x).ToArray()).ToArray());
        Assert.Equal(3, result.Fetched);
        Assert.Equal(100, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 3));
        Assert.Equal(10, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1));
        Assert.Equal(3, done.Count); // reported once each
    }

    [Fact]
    public async Task AnAnimeTooLongForOneReadFailsAloneAndTheOthersAreStillSaved()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient { PageSize = 50, PageCap = 2 }; // 100 rows a read
        await Kit.SeedAsync(db, 1, "finished_airing");
        await Kit.SeedAsync(db, 2, "finished_airing");
        aniList.Add(1, rows: 5);
        aniList.Add(2, rows: 500);
        var logger = new CapturingLogger<EpisodeScheduleRefreshService>();

        var result = await Kit.CreateService(db, aniList, logger).RefreshBatchAsync([1, 2]);

        Assert.Equal(new RefreshBatchResult(Fetched: 1, NoData: 0, Failed: 1), result);
        Assert.Equal(5, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1));
        Assert.Equal(0, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 2));
        Assert.Null(await db.AnimeAiringSyncs.AsNoTracking().SingleOrDefaultAsync(s => s.AnimeId == 2)); // no mark: a later pass tries again
        Assert.Contains(logger.Lines, l => l.StartsWith("Error") && l.Contains("Anime 2"));
    }

    // --- failures ---

    [Fact]
    public async Task AFailureMidReadLeavesTheAnimeAlreadySavedSavedAndFailsTheRest()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient { FailScheduleReadAfterCompletions = 2 };
        foreach (var id in new[] { 1, 2, 3 })
        {
            await Kit.SeedAsync(db, id, "finished_airing");
            aniList.Add(id, rows: 4);
        }
        var done = new List<(int Id, AiringRefreshOutcome Outcome)>();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1, 2, 3], done);

        Assert.Equal(new RefreshBatchResult(Fetched: 2, NoData: 0, Failed: 1), result);
        Assert.Equal(8, await db.EpisodeAirings.CountAsync());
        Assert.Equal([1, 2], (await db.AnimeAiringSyncs.Where(s => s.LastFetchedAt != null).Select(s => s.AnimeId).ToListAsync()).Order());
        Assert.Equal(3, done.Count);
        Assert.Contains((3, AiringRefreshOutcome.Failed), done);
    }

    [Fact]
    public async Task ATimedOutLookupFailsItsAnimeAloneAndTheNextBatchIsStillRead()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient
        {
            Fail = (what, ids) => what == "lookup" && ids.Contains(1) ? HttpClientTimeout() : null,
        };
        var ids = Enumerable.Range(1, 30).ToList(); // lookup batches of 25 and 5
        foreach (var id in ids)
        {
            await Kit.SeedAsync(db, id);
            aniList.Add(id, rows: 1);
        }

        var result = await RefreshAsync(Kit.CreateService(db, aniList), ids);

        Assert.Equal(new RefreshBatchResult(Fetched: 5, NoData: 0, Failed: 25), result);
        // The failed ones have no mark, which is all the next pass needs to retry them.
        Assert.Equal(5, await db.AnimeAiringSyncs.CountAsync(s => s.LastFetchedAt != null));
    }

    [Fact]
    public async Task AFailedSaveFailsThatAnimeAlone()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        foreach (var id in new[] { 1, 2 })
        {
            await Kit.SeedAsync(db, id, "finished_airing");
            aniList.Add(id, rows: 3);
        }
        // Anime 1's rows can't be written; anime 2's can.
        var service = new EpisodeScheduleRefreshService(
            db, new UserAnimeEntryRepository(db), new FailingRepositoryFor(db, failingAnimeId: 1),
            aniList, new AniListRelationStore(db), Kit.LocalTime,
            new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))),
            NullLogger<EpisodeScheduleRefreshService>.Instance);

        var result = await RefreshAsync(service, [1, 2]);

        Assert.Equal(new RefreshBatchResult(Fetched: 1, NoData: 0, Failed: 1), result);
        Assert.Equal(0, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1));
        Assert.Equal(3, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 2));
        Assert.Null((await db.AnimeAiringSyncs.AsNoTracking().SingleOrDefaultAsync(s => s.AnimeId == 1))?.LastFetchedAt);
    }

    private sealed class FailingRepositoryFor(AnimeTrackerDbContext db, int failingAnimeId) : IEpisodeAiringRepository
    {
        private readonly InMemoryEpisodeAiringRepository _inner = new(db);

        public Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Dictionary<int, DateTimeOffset>> GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) => throw new NotImplementedException();

        public Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default) =>
            animeId == failingAnimeId ? throw new InvalidOperationException("the database refused the write") : _inner.ReplaceForAnimeAsync(animeId, rows, ct);
    }

    [Fact]
    public async Task ACancelledPassThrowsRatherThanReportingFailures()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);
        aniList.Add(1, rows: 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RefreshAsync(Kit.CreateService(db, aniList), [1], ct: cts.Token));
    }

    [Fact]
    public async Task EveryAnimeIsReportedExactlyOnce_MatchingTheCounts()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);                       // AniList has it, with rows
        await Kit.SeedAsync(db, 2);                       // AniList doesn't know it
        await Kit.SeedAsync(db, 3, fetchedAgo: TimeSpan.FromDays(1), aniListId: null); // unknown, recorded a day ago
        aniList.Add(1, rows: 2);
        var done = new List<(int Id, AiringRefreshOutcome Outcome)>();

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1, 2, 3], done);

        Assert.Equal(new RefreshBatchResult(Fetched: 1, NoData: 2, Failed: 0), result);
        Assert.Equal([1, 2, 3], done.Select(d => d.Id).Order());
    }

    // --- the 90-day relookup ---

    [Theory]
    [InlineData(89, false, false)]
    [InlineData(91, false, true)]
    [InlineData(2, true, true)]  // by hand, whatever the age
    public async Task AnUnknownAnimeIsLookedUpAgainAfter90DaysOrWhenTheRefreshWasStartedByHand(int days, bool byHand, bool lookedUp)
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(days), aniListId: null);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 3);

        var result = await RefreshAsync(Kit.CreateService(db, aniList), [1], relookupAbsent: byHand);

        Assert.Equal(lookedUp, aniList.LookupBatches.Count == 1);
        Assert.Equal(lookedUp ? 1 : 0, result.Fetched);
        Assert.Equal(lookedUp ? 1 : 0, result.Fetched);
        Assert.Equal(lookedUp ? 901 : null, (await db.AnimeAiringSyncs.AsNoTracking().SingleAsync(s => s.AnimeId == 1)).AniListId);
    }

    // --- the first-fetch rule ---

    [Fact]
    public async Task ABatchedFirstFetchRecordsNoReleaseAndALaterOneDoes()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();
        await Kit.SeedAsync(db, 1);                                                          // never fetched
        await Kit.SeedAsync(db, 2, fetchedAgo: TimeSpan.FromDays(1), aniListId: 902);         // has its mark
        aniList.Add(1, rows: 2, status: "RELEASING", episodes: 12);
        aniList.Add(2, rows: 2, status: "RELEASING", episodes: 12);

        await RefreshAsync(Kit.CreateService(db, aniList), [1, 2]);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(2, update.AnimeId);
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
        Assert.Equal(12, (await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1)).TotalEpisodes); // stored either way
    }

    [Fact]
    public async Task ABatchedRefreshRecordsAMovedEpisodeForAnAnimeWithAMark()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1, fetchedAgo: TimeSpan.FromDays(1), aniListId: 901);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = AtNoonUtc(TodayUtc.AddDays(10)), FetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, status: "RELEASING").Rows = [new AniListEpisode(5, AtNoonUtc(TodayUtc.AddDays(17)))];

        await RefreshAsync(Kit.CreateService(db, aniList), [1]);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodesMoved, update.Kinds);
        Assert.Equal(5, update.MovedEpisode);
    }

    // --- batched equals single ---

    [Fact]
    public async Task ABatchedPassStoresExactlyWhatFetchingEachAnimeOnItsOwnStores()
    {
        // The same script, the same starting rows, once one anime at a time and once as a batch.
        var now = DateTimeOffset.UtcNow;
        async Task<(AnimeTrackerDbContext Db, ScriptedAniListClient Client)> SeedWorldAsync()
        {
            var db = Kit.CreateDb();
            var client = new ScriptedAniListClient();

            // 1: airing, never looked up, with relations and a next episode
            await Kit.SeedAsync(db, 1);
            var airing = client.Add(1, rows: 8, status: "RELEASING", episodes: null);
            airing.NextAiringAt = now.AddDays(3);
            airing.Relations = [new AniListRelationEdge("PREQUEL", 50), new AniListRelationEdge("SIDE_STORY", 51)];

            // 2: finished, never looked up, a total
            await Kit.SeedAsync(db, 2, "finished_airing");
            client.Add(2, rows: 12, status: "FINISHED", episodes: 12);

            // 3: unknown to AniList
            await Kit.SeedAsync(db, 3, "finished_airing");

            // 4: stored id, refreshed
            await Kit.SeedAsync(db, 4, fetchedAgo: TimeSpan.FromDays(2), aniListId: 904);
            client.Add(4, rows: 24, status: "RELEASING", episodes: 24).NextAiringAt = now.AddDays(10);

            // 5: stored id, AniList has the show but no rows, and rows are stored already
            await Kit.SeedAsync(db, 5, "finished_airing", fetchedAgo: TimeSpan.FromDays(2), aniListId: 905, malTotal: 12);
            db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 5, Episode = 2, AirsAtUtc = now.AddDays(-40), FetchedAt = now.AddDays(-3) });
            await db.SaveChangesAsync();
            client.Add(5, rows: 0, status: "FINISHED", episodes: 13);

            // 6: a long runner
            await Kit.SeedAsync(db, 6, fetchedAgo: TimeSpan.FromDays(2), aniListId: 906);
            client.Add(6, rows: 1100, status: "RELEASING");

            return (db, client);
        }

        var ids = new[] { 1, 2, 3, 4, 5, 6 };
        var (singleDb, singleClient) = await SeedWorldAsync();
        var (batchDb, batchClient) = await SeedWorldAsync();
        using (singleDb)
        using (batchDb)
        {
            var single = Kit.CreateService(singleDb, singleClient);
            foreach (var id in ids)
                await single.RefreshOneAsync(id);

            await RefreshAsync(Kit.CreateService(batchDb, batchClient), ids);

            Assert.Equal(await RowsAsync(singleDb), await RowsAsync(batchDb));
            Assert.Equal(await SyncsAsync(singleDb), await SyncsAsync(batchDb));
            Assert.Equal(await TotalsAsync(singleDb), await TotalsAsync(batchDb));
            Assert.Equal(await RelationsAsync(singleDb), await RelationsAsync(batchDb));
            Assert.Equal(await UpdatesAsync(singleDb), await UpdatesAsync(batchDb));
            // ...and the batch really was one, not the single path in disguise.
            Assert.Empty(batchClient.SingleLookups);
            Assert.Empty(batchClient.SingleSchedules);
            Assert.NotEmpty(singleClient.SingleLookups);
        }
    }

    private static async Task<List<string>> RowsAsync(AnimeTrackerDbContext db) =>
        (await db.EpisodeAirings.AsNoTracking().OrderBy(e => e.AnimeId).ThenBy(e => e.Episode).ToListAsync())
        .Select(e => $"{e.AnimeId}/{e.Episode}/{e.AirsAtUtc:O}").ToList();

    // Timestamps that are "now" differ between two runs; whether they are set doesn't.
    private static async Task<List<string>> SyncsAsync(AnimeTrackerDbContext db) =>
        (await db.AnimeAiringSyncs.AsNoTracking().OrderBy(s => s.AnimeId).ToListAsync())
        .Select(s => $"{s.AnimeId}/{s.AniListId}/{s.LastFetchedAt is not null}/{s.RelationsFetchedAt is not null}/" +
                     $"{s.NextAiringEpisodeAtUtc:O}/{s.HasCompleteData}/{(s.NextRecheckAtUtc is null ? "none" : "set")}")
        .ToList();

    private static async Task<List<string>> TotalsAsync(AnimeTrackerDbContext db) =>
        (await db.AnimeMetadata.AsNoTracking().OrderBy(a => a.Id).ToListAsync())
        .Select(a => $"{a.Id}/{a.MalTotalEpisodes}/{a.AniListTotalEpisodes}/{a.TotalEpisodes}").ToList();

    private static async Task<List<string>> RelationsAsync(AnimeTrackerDbContext db) =>
        (await db.AniListRelations.AsNoTracking().OrderBy(r => r.AnimeId).ThenBy(r => r.RelatedAnimeId).ToListAsync())
        .Select(r => $"{r.AnimeId}/{r.RelatedAnimeId}/{r.RelationType}").ToList();

    private static async Task<List<string>> UpdatesAsync(AnimeTrackerDbContext db) =>
        (await db.AnimeUpdates.AsNoTracking().OrderBy(u => u.AnimeId).ToListAsync()).Select(u => $"{u.AnimeId}/{u.Kinds}").ToList();

    // --- the Refresh all / Force all targets ---

    private static async Task SeedTargetWorldAsync(AnimeTrackerDbContext db)
    {
        // Frieren: finished, total 28, rows for episodes 5 to 28 only (1 to 4 premiered together).
        await Kit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 901, malTotal: 28);
        db.EpisodeAirings.AddRange(Enumerable.Range(5, 24).Select(n => new EpisodeAiring { AnimeId = 1, Episode = n, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-100 + n), FetchedAt = DateTimeOffset.UtcNow }));

        // A finished show AniList has no rows for.
        await Kit.SeedAsync(db, 2, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 902, malTotal: 12);

        // Finished and complete, with every row.
        await Kit.SeedAsync(db, 3, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 903, malTotal: 2);
        db.EpisodeAirings.AddRange(
            new EpisodeAiring { AnimeId = 3, Episode = 1, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-90), FetchedAt = DateTimeOffset.UtcNow },
            new EpisodeAiring { AnimeId = 3, Episode = 2, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-83), FetchedAt = DateTimeOffset.UtcNow });

        // Finished, with rows, but no known total from either source.
        await Kit.SeedAsync(db, 4, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 904);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 4, Episode = 1, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-90), FetchedAt = DateTimeOffset.UtcNow });

        // Finished, recorded as unknown to AniList.
        await Kit.SeedAsync(db, 5, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: null, malTotal: 12);

        // Still airing.
        await Kit.SeedAsync(db, 6, "currently_airing", fetchedAgo: TimeSpan.FromDays(1), aniListId: 906, malTotal: 12);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 6, Episode = 12, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), FetchedAt = DateTimeOffset.UtcNow });

        // Finished, never fetched.
        await Kit.SeedAsync(db, 7, "finished_airing", malTotal: 1);

        // Not in my list at all: never a target.
        await Kit.SeedAsync(db, 8, "currently_airing", inList: false);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task RefreshAllSkipsOnlyShowsWhoseHistoryIsComplete()
    {
        using var db = Kit.CreateDb();
        await SeedTargetWorldAsync(db);

        var targets = await Kit.CreateService(db, new ScriptedAniListClient()).GetFullRefreshTargetsAsync(force: false);

        // 1 (Frieren) and 3 (every row) are complete. 2 has no rows, 4 has no total, 5 is unknown,
        // 6 still airs, 7 was never fetched.
        Assert.Equal([2, 4, 5, 6, 7], targets.Order());
    }

    [Fact]
    public async Task ForceAllTakesEveryAnimeInMyList()
    {
        using var db = Kit.CreateDb();
        await SeedTargetWorldAsync(db);

        var targets = await Kit.CreateService(db, new ScriptedAniListClient()).GetFullRefreshTargetsAsync(force: true);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7], targets.Order());
    }

    [Fact]
    public async Task TheHighestStoredEpisodeNotTheRowCountDecidesCompleteness()
    {
        using var db = Kit.CreateDb();
        // Twelve rows for a 12-episode show, but they are episodes 1 to 6 and 13 to 18: the highest is 18, not 12.
        await Kit.SeedAsync(db, 1, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 901, malTotal: 12);
        db.EpisodeAirings.AddRange(Enumerable.Range(1, 6).Concat(Enumerable.Range(13, 6))
            .Select(n => new EpisodeAiring { AnimeId = 1, Episode = n, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-100 + n), FetchedAt = DateTimeOffset.UtcNow }));
        // Twelve rows would seem enough for a 24-episode show; the highest episode says otherwise.
        await Kit.SeedAsync(db, 2, "finished_airing", fetchedAgo: TimeSpan.FromDays(30), aniListId: 902, malTotal: 24);
        db.EpisodeAirings.AddRange(Enumerable.Range(1, 12)
            .Select(n => new EpisodeAiring { AnimeId = 2, Episode = n, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-100 + n), FetchedAt = DateTimeOffset.UtcNow }));
        await db.SaveChangesAsync();

        var targets = await Kit.CreateService(db, new ScriptedAniListClient()).GetFullRefreshTargetsAsync(force: false);

        Assert.Equal([2], targets);
    }

    // --- the catch-up ---

    [Fact]
    public async Task TheCatchUpFetchesListAnimeWithNoMarkAndUnknownOnesOlderThan90DaysAndNothingElse()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1);                                                              // no sync row: catch up
        await Kit.SeedAsync(db, 2, "finished_airing", fetchedAgo: TimeSpan.FromDays(120), aniListId: null); // unknown, 120 days: catch up
        await Kit.SeedAsync(db, 3, "finished_airing", fetchedAgo: TimeSpan.FromDays(10), aniListId: null);  // unknown, 10 days: not yet
        await Kit.SeedAsync(db, 4, fetchedAgo: TimeSpan.FromDays(1), aniListId: 904);            // has its mark: the daily pass's
        await Kit.SeedAsync(db, 5, inList: false);                                               // not in my list: never
        var aniList = new ScriptedAniListClient();
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
            aniList.Add(id, rows: 2);

        await Kit.CreateService(db, aniList).CatchUpAsync();

        Assert.Equal([1, 2], aniList.LookupBatches.Single().Order());
        Assert.Equal(2, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 1));
        Assert.Equal(2, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 2));
        Assert.Equal(0, await db.EpisodeAirings.CountAsync(e => e.AnimeId == 3));
    }

    [Fact]
    public async Task TheCatchUpMakesNoRequestWhenThereIsNothingToCatchUp()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1, fetchedAgo: TimeSpan.FromDays(1), aniListId: 901);
        await Kit.SeedAsync(db, 2, "finished_airing", fetchedAgo: TimeSpan.FromDays(89), aniListId: null);
        var aniList = new ScriptedAniListClient();

        await Kit.CreateService(db, aniList).CatchUpAsync();

        Assert.Empty(aniList.LookupBatches);
        Assert.Empty(aniList.MediaBatches);
        Assert.Empty(aniList.ScheduleReads);
    }

    [Fact]
    public async Task TheCatchUpLogsEachAnimeAniListHasNoAiringDataForByTitleAndId()
    {
        using var db = Kit.CreateDb();
        await Kit.SeedAsync(db, 1);
        await Kit.SeedAsync(db, 2);
        var aniList = new ScriptedAniListClient();
        aniList.Add(1, rows: 2); // AniList knows 1, and has never heard of 2
        var logger = new CapturingLogger<EpisodeScheduleRefreshService>();

        await Kit.CreateService(db, aniList, logger).CatchUpAsync();

        var line = Assert.Single(logger.Lines, l => l.Contains("Catch-up: AniList returned no airing data"));
        Assert.Contains("anime 2", line);
        Assert.Contains("Anime 2", line);
    }

    [Fact]
    public async Task ACatchUpWithAnEmptyListDoesNothing()
    {
        using var db = Kit.CreateDb();
        var aniList = new ScriptedAniListClient();

        await Kit.CreateService(db, aniList).CatchUpAsync();

        Assert.Empty(aniList.LookupBatches);
    }
}
