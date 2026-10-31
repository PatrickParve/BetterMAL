using System.Net;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime task 4.2: GameAnimeDetails over the real
// MetadataRefreshService, with only MyAnimeList faked. A game's fetch has to fill
// the stats in, and it must stay out of the Updates menu: a hidden answer that
// showed up there would spoil the game (design D6).
public class GameAnimeDetailsRefreshTests
{
    private static MalAnimeNode DetailNode(int id) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        AlternativeTitles = new MalAlternativeTitles { En = $"English {id}" },
        MainPicture = new MalMainPicture { Large = $"https://img/{id}.jpg" },
        Mean = 8.1,
        MediaType = "tv",
        // Still airing, because schedule news is only recorded for an anime that
        // hasn't finished: a finished one would make the no-news tests below
        // pass whatever the first-fetch rule did.
        Status = "currently_airing",
        NumEpisodes = 25,
        StartDate = "2013-04-07",
        Genres = [new MalGenre { Id = 1, Name = "Action" }, new MalGenre { Id = 8, Name = "Drama" }],
        Studios = [new MalStudio { Id = 4, Name = "Bones" }],
        Broadcast = new MalBroadcast { DayOfTheWeek = "sundays", StartTime = "17:00" },
        Popularity = 15,
        // A sequel, so that a fetch which did count as news would have something to say.
        RelatedAnime = [new MalRelatedAnimeEdge { RelationType = "sequel", Node = new MalAnimeNode { Id = id + 1000, Title = "Sequel" } }],
    };

    private sealed class Fixture : IDisposable
    {
        public Fixture(Dictionary<int, MalAnimeNode>? responses = null, Dictionary<int, Exception>? failures = null)
        {
            Db = CreateDb();
            Mal = new FakeMalClient(responses ?? new Dictionary<int, MalAnimeNode> { [1] = DetailNode(1) }, failures);
            var refresh = new MetadataRefreshService(
                Db,
                Mal,
                new AnimeMetadataChangeDetector(
                    Db, new AnimeUpdateRecorder(Db, new AnimeUpdateRelevance(Db, new RelationResolver(Db))), SeriesBuilds,
                    ScoreRecorderTestKit.Create(Db)),
                ScoreRecorderTestKit.Create(Db),
                new FakeAnimeSearchIndex(),
                NullLogger<MetadataRefreshService>.Instance);
            Details = new GameAnimeDetails(Db, refresh, new RefreshGate());
        }

        public AnimeTrackerDbContext Db { get; }
        public FakeMalClient Mal { get; }
        public FakeSeriesBuilds SeriesBuilds { get; } = new();
        public GameAnimeDetails Details { get; }

        public void Dispose() => Db.Dispose();
    }

    /// <summary>What a listing refresh leaves: lean fields only, never fully
    /// fetched, on my list so that an Updates entry would be relevant.</summary>
    private static async Task AddNeverFetchedAsync(Fixture f, int id = 1, bool onMyList = true)
    {
        f.Db.AnimeMetadata.Add(Anime(id, score: 8.0));
        if (onMyList)
            f.Db.UserAnimeEntries.Add(OnList(id, WatchStatus.Watching));
        await f.Db.SaveChangesAsync();
    }

    // ---- a never-fetched row ----

    [Fact]
    public async Task ANeverFetchedRowsFetchFillsGenresStudiosAndTheStartDate()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        var fetched = await f.Details.EnsureFetchedAsync(1);

        Assert.True(fetched);
        var anime = await f.Db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(["Action", "Drama"], anime.Genres);
        Assert.Equal(["Bones"], anime.Studios);
        Assert.Equal(new DateOnly(2013, 4, 7), anime.AiredFrom);
        Assert.Equal(25, anime.TotalEpisodes);
        Assert.False(AnimeStats.IsMissingAny(anime));
    }

    [Fact]
    public async Task TheFetchStampsLastSyncedAt()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        await f.Details.EnsureFetchedAsync(1);

        var anime = await f.Db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.NotEqual(default, anime.LastSyncedAt);
    }

    [Fact]
    public async Task ANeverFetchedRowsFetchWritesNoUpdateAndNoRelationDiscovery()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        await f.Details.EnsureFetchedAsync(1);

        // The premiere date, the episode count, the broadcast slot and a sequel all
        // arrived at once, and none of it is news: it would spoil a hidden answer.
        Assert.Empty(await f.Db.AnimeUpdates.ToListAsync());
        Assert.Empty(await f.Db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task ThatHoldsForAnAnimeNotOnMyListToo()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f, onMyList: false);

        await f.Details.EnsureFetchedAsync(1);

        Assert.Empty(await f.Db.AnimeUpdates.ToListAsync());
        Assert.Empty(await f.Db.RelationDiscoveries.ToListAsync());
    }

    [Fact]
    public async Task ThePictureAndTitleAreKeptOrRefreshedFromTheFetch()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        await f.Details.EnsureFetchedAsync(1);

        var anime = await f.Db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal("English 1", anime.EnglishTitle);
        Assert.NotNull(anime.PictureUrl);
    }

    [Fact]
    public async Task OneRequestMakesTheAnimeCompleteSoNoSecondIsMade()
    {
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        await f.Details.EnsureFetchedAsync(1);
        var again = await f.Details.EnsureFetchedAsync(1);

        Assert.False(again);
        Assert.Equal([1], f.Mal.Calls);
    }

    [Fact]
    public async Task AControlARefetchOfAnAnimeFetchedBeforeCanRecordNews()
    {
        // The same fixture and the same MyAnimeList answer, but the anime has had a
        // full fetch before, its premiere date has moved and a sequel has turned up:
        // an ordinary refresh, so the Updates menu hears of both. Without this, the
        // no-news tests above could pass for a fixture that could never record
        // anything.
        using var f = new Fixture();
        var anime = Anime(1, score: 8.0, airedFrom: new DateOnly(2013, 1, 6));
        anime.TotalEpisodes = 25;
        anime.Studios = ["Bones"];
        anime.LastSyncedAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(60);
        // No genres stored, so a stat is missing and the game fetches it.
        f.Db.AnimeMetadata.Add(anime);
        f.Db.UserAnimeEntries.Add(OnList(1, WatchStatus.PlanToWatch));
        await f.Db.SaveChangesAsync();

        Assert.True(await f.Details.EnsureFetchedAsync(1));

        Assert.NotEmpty(await f.Db.AnimeUpdates.ToListAsync());
        Assert.NotEmpty(await f.Db.RelationDiscoveries.ToListAsync());
    }

    // ---- an anime that isn't stored ----

    [Fact]
    public async Task AnAnimeThatIsNotStoredGetsARow()
    {
        using var f = new Fixture(new Dictionary<int, MalAnimeNode> { [77] = DetailNode(77) });

        var fetched = await f.Details.EnsureFetchedAsync(77);

        Assert.True(fetched);
        var anime = await f.Db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 77);
        Assert.Equal("Anime 77", anime.Title);
        Assert.Equal(["Action", "Drama"], anime.Genres);
        Assert.Equal(["Bones"], anime.Studios);
        Assert.Equal(new DateOnly(2013, 4, 7), anime.AiredFrom);
        Assert.False(AnimeStats.IsMissingAny(anime));
        Assert.NotEqual(default, anime.LastSyncedAt);
    }

    [Fact]
    public async Task AnAnimeThatIsNotStoredWritesNoNewsEither()
    {
        using var f = new Fixture(new Dictionary<int, MalAnimeNode> { [77] = DetailNode(77) });

        await f.Details.EnsureFetchedAsync(77);

        Assert.Empty(await f.Db.AnimeUpdates.ToListAsync());
        Assert.Empty(await f.Db.RelationDiscoveries.ToListAsync());
    }

    // ---- what a first fetch does besides (design D6) ----

    [Fact]
    public async Task AFirstFetchWithRelationsQueuesASeriesBuild()
    {
        // The same as opening the anime's detail page: the edges stop being new
        // after this fetch, so the build would never be queued again.
        using var f = new Fixture();
        await AddNeverFetchedAsync(f);

        await f.Details.EnsureFetchedAsync(1);

        Assert.Equal([1], f.SeriesBuilds.Enqueued);
    }

    // ---- a failure ----

    [Fact]
    public async Task AFailedFetchLeavesTheRowAsItWas()
    {
        using var f = new Fixture(failures: new Dictionary<int, Exception> { [1] = new HttpRequestException("MAL is down") });
        await AddNeverFetchedAsync(f);

        await Assert.ThrowsAsync<HttpRequestException>(() => f.Details.EnsureFetchedAsync(1));

        var anime = await f.Db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(default, anime.LastSyncedAt);
        Assert.Null(anime.Genres);
        Assert.Empty(await f.Db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task ANotFoundIsAnAnimeMetadataNotFound()
    {
        using var f = new Fixture(failures: new Dictionary<int, Exception>
        {
            [1] = new HttpRequestException("gone", null, HttpStatusCode.NotFound),
        });

        await Assert.ThrowsAsync<AnimeMetadataNotFoundException>(() => f.Details.EnsureFetchedAsync(1));

        Assert.False(await f.Db.AnimeMetadata.AnyAsync());
    }

    private sealed class FakeSeriesBuilds : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeMalClient(Dictionary<int, MalAnimeNode> responses, Dictionary<int, Exception>? failures = null) : IMalClient
    {
        public List<int> Calls { get; } = [];

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (failures is not null && failures.TryGetValue(animeId, out var failure))
                throw failure;
            return Task.FromResult(responses[animeId]);
        }

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
