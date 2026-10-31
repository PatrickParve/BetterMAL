using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// series-page spec, "The series page lists the main line's openings and endings": the
// series read carries the stored themes of every main-line entry, each version slot's
// alternatives included, and nothing from an extra. Each test reads a stored, freshly
// built series, so the projection runs and the graph builder never does.
public class SeriesServiceThemesTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // graphBuilder is never reached: a series built just now and not partial needs no build.
    private static SeriesService CreateService(AnimeTrackerDbContext db, IAnimeThemesReads? themes = null) =>
        new(db, graphBuilder: null!, new RefreshGate(), new FakeEpisodeScheduleService(), new NoOpAnimeRankingService(),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""), TestIdMappings.Resolver(db),
            ScoreHistoryServiceTestKit.Create(db), themes ?? AnimeThemesReadsTestKit.Create(db));

    private static void AddAnime(AnimeTrackerDbContext db, params int[] ids)
    {
        foreach (var id in ids)
            db.AnimeMetadata.Add(new AnimeMetadata
            {
                Id = id, Title = $"Anime {id}", AiringStatus = "finished_airing",
                AiredFrom = new DateOnly(2020, 1, 1), AiredTo = new DateOnly(2020, 6, 1),
            });
    }

    private static SeriesMember Member(
        int animeId, int order, bool mainLine = true, int? slotKey = null, int? branchHead = null,
        string? relationGroup = null, string kind = "Core") =>
        new()
        {
            SeriesId = 1, AnimeId = animeId, IsMainLine = mainLine, IsPrimary = true, Order = order,
            VersionSlotKey = slotKey, BranchHeadAnimeId = branchHead, RelationGroup = relationGroup, MembershipKind = kind,
        };

    // Series 1, the root being anime 1:
    //   main line   1 (trunk), 2 and 3 (the alternatives of one slot, a film and a TV arc), 4 (in 3's
    //               branch), 5 (trunk, with no themes)
    //   extras      6 (a special) and 7 (a folded-in version neighbour, as Brotherhood is of the 2003 series)
    private static void SeedSeries(AnimeTrackerDbContext db)
    {
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 1, 2, 3, 4, 5, 6, 7);
        db.SeriesMembers.AddRange(
            Member(1, 0),
            Member(2, 1, slotKey: 2, branchHead: 2),
            Member(3, 2, slotKey: 2, branchHead: 3),
            Member(4, 3, branchHead: 3),
            Member(5, 4),
            Member(6, 0, mainLine: false, relationGroup: "SideStory"),
            Member(7, 0, mainLine: false, relationGroup: "AlternativeVersion", kind: "FoldedVersion"));
    }

    private static void SeedThemes(AnimeTrackerDbContext db, params int[] animeIds)
    {
        StoredThemes.MarkSynced(db);
        foreach (var id in animeIds)
        {
            db.AnimeThemes.Add(StoredThemes.Row(id, id * 100, AnimeThemeKind.Opening, 1));
            db.AnimeThemes.Add(StoredThemes.Row(id, id * 100 + 1, AnimeThemeKind.Ending, 1));
        }
    }

    [Fact]
    public async Task EveryMainLineEntryHasItsGroupInWatchOrder_AlternativesIncluded()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 1, 2, 3, 4);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.True(dto.Themes.Ready);
        Assert.Equal([1, 2, 3, 4], dto.Themes.Groups.Select(g => g.AnimeId));
        Assert.All(dto.Themes.Groups, g => Assert.Equal(["OP1", "ED1"], g.Themes.Select(t => t.Label)));
    }

    [Fact]
    public async Task ASlotsAlternativesAreAllPresent_SoSwitchingRouteNeedsNoRequest()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 2, 3, 4);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        // Both alternatives and the member of the one branch, whichever is picked.
        Assert.Equal([2, 3, 4], dto.Themes.Groups.Select(g => g.AnimeId));
        Assert.Equal([2, 3], dto.Slots.Single().AlternativeAnimeIds);
    }

    [Fact]
    public async Task ExtrasStayOut()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 1, 6); // 6 is a special among the extras
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.Equal([1], dto.Themes.Groups.Select(g => g.AnimeId));
    }

    [Fact]
    public async Task AVersionNeighbourStaysOut()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 1, 7); // 7 is the folded-in version neighbour
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.DoesNotContain(dto.Themes.Groups, g => g.AnimeId == 7);
        Assert.Equal([1], dto.Themes.Groups.Select(g => g.AnimeId));
    }

    [Fact]
    public async Task AMemberWithNoThemesHasNoGroup()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 1, 5); // 2, 3 and 4 have none
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.Equal([1, 5], dto.Themes.Groups.Select(g => g.AnimeId));
    }

    [Fact]
    public async Task ASeriesWhoseMainLineHasNoThemesHasNoGroups_ButIsReady()
    {
        using var db = CreateDb();
        SeedSeries(db);
        SeedThemes(db, 6, 7); // extras only
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.True(dto.Themes.Ready);
        Assert.Empty(dto.Themes.Groups);
    }

    [Fact]
    public async Task BeforeTheFirstRefreshTheThemesAreNotReady()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.False(dto.Themes.Ready);
        Assert.Empty(dto.Themes.Groups);
    }

    [Fact]
    public async Task TheGroupsFollowTheMainLinesOrderNotTheOrderRowsWereStored()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 1, 2, 3);
        // Stored 3, 1, 2 but watched 1, 2, 3.
        db.SeriesMembers.AddRange(Member(3, 2), Member(1, 0), Member(2, 1));
        SeedThemes(db, 1, 2, 3);
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetSeriesAsync(1);

        Assert.Equal([1, 2, 3], dto.Themes.Groups.Select(g => g.AnimeId));
        Assert.Equal([1, 2, 3], dto.MainLine.Select(e => e.AnimeId));
    }

    [Fact]
    public async Task ALargeSeriesShowsEveryGroupAtOnceFromOneRead()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        var ids = Enumerable.Range(1, 60).ToArray();
        AddAnime(db, ids);
        db.SeriesMembers.AddRange(ids.Select(id => Member(id, id - 1)));
        SeedThemes(db, ids);
        await db.SaveChangesAsync();
        var reads = new CountingThemesReads(AnimeThemesReadsTestKit.Create(db));

        var dto = await CreateService(db, reads).GetSeriesAsync(1);

        Assert.Equal(60, dto.Themes.Groups.Count);
        Assert.Equal(ids, dto.Themes.Groups.Select(g => g.AnimeId));
        Assert.Equal(1, reads.SeriesCalls); // one query for all sixty, not one each
    }

    [Fact]
    public async Task TheReaderIsAskedForTheMainLineAndNothingElse()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();
        var reads = new CountingThemesReads(AnimeThemesReadsTestKit.Create(db));

        await CreateService(db, reads).GetSeriesAsync(1);

        Assert.Equal([1, 2, 3, 4, 5], Assert.Single(reads.SeriesAsked));
        Assert.Empty(reads.AnimeCalls);
    }

    // The page's read can reach the catalogue and nothing that reaches AnimeThemes: no
    // client, no sync and no HTTP in what it is built from (spec anime-themes "A large series").
    [Fact]
    public void TheSeriesServiceCannotReachAnimeThemes()
    {
        var parameters = typeof(SeriesService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(IAnimeThemesReads), parameters);
        Assert.DoesNotContain(typeof(IAnimeThemesClient), parameters);
        Assert.DoesNotContain(typeof(IAnimeThemesCatalogueSyncService), parameters);
        Assert.DoesNotContain(typeof(HttpClient), parameters);
        Assert.DoesNotContain(typeof(IHttpClientFactory), parameters);
    }

    private sealed class CountingThemesReads(IAnimeThemesReads inner) : IAnimeThemesReads
    {
        public List<int> AnimeCalls { get; } = [];
        public List<List<int>> SeriesAsked { get; } = [];
        public int SeriesCalls => SeriesAsked.Count;

        public Task<AnimeThemesDto> GetForAnimeAsync(int animeId, CancellationToken ct = default)
        {
            AnimeCalls.Add(animeId);
            return inner.GetForAnimeAsync(animeId, ct);
        }

        public Task<SeriesThemesDto> GetForSeriesAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default)
        {
            SeriesAsked.Add([.. animeIds]);
            return inner.GetForSeriesAsync(animeIds, ct);
        }
    }

    // The same stand-ins as the other SeriesService tests: these don't score or
    // schedule anything, so an empty ranking and a schedule that is never asked
    // are faithful.
    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class NoOpAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Build([], []));
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
