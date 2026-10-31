using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime task 4.1: EnsureFetchedAsync fetches an anime that isn't
// stored or is missing a stat, behind the detail page's own RefreshGate key, and
// checks again once it holds the gate. The fetch itself is a fake that fills the
// row in; task 4.2 puts the real MetadataRefreshService behind it.
public class GameAnimeDetailsTests
{
    private static readonly DateOnly Aired = new(2013, 4, 7);

    private static GameAnimeDetails Service(AnimeTrackerDbContext db, FakeRefresh refresh, RefreshGate? gate = null) =>
        new(db, refresh, gate ?? new RefreshGate());

    private static AnimeMetadata WithoutStat(string stat, int id = 1)
    {
        var anime = Whole(id, airedFrom: Aired);
        switch (stat)
        {
            case "start date": anime.AiredFrom = null; break;
            case "episodes": anime.TotalEpisodes = null; break;
            case "type": anime.MediaType = null; break;
            case "score": anime.MalScore = null; break;
            case "genres": anime.Genres = null; break;
            case "empty genres": anime.Genres = []; break;
            case "studio list": anime.Studios = null; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }

        return anime;
    }

    // ---- when it fetches ----

    [Fact]
    public async Task NoFetchForAnAnimeHoldingEveryStatHoweverOld()
    {
        using var db = CreateDb();
        var anime = Whole(1, airedFrom: Aired);
        anime.LastSyncedAt = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);

        var fetched = await Service(db, refresh).EnsureFetchedAsync(1);

        Assert.False(fetched);
        Assert.Empty(refresh.Calls);
    }

    [Theory]
    [InlineData("start date")]
    [InlineData("episodes")]
    [InlineData("type")]
    [InlineData("score")]
    [InlineData("genres")]
    [InlineData("empty genres")]
    [InlineData("studio list")]
    public async Task OneFetchForAnAnimeMissingAnyStat(string stat)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat(stat));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);

        var fetched = await Service(db, refresh).EnsureFetchedAsync(1);

        Assert.True(fetched);
        Assert.Equal([1], refresh.Calls);
    }

    [Fact]
    public async Task AnEmptyStudioListIsKnownSoItIsNotFetched()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Whole(1, airedFrom: Aired, studios: []));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);

        Assert.False(await Service(db, refresh).EnsureFetchedAsync(1));
        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task OneFetchForAnAnimeThatIsNotStored()
    {
        using var db = CreateDb();
        var refresh = new FakeRefresh(db);

        var fetched = await Service(db, refresh).EnsureFetchedAsync(42);

        Assert.True(fetched);
        Assert.Equal([42], refresh.Calls);
        Assert.True(await db.AnimeMetadata.AnyAsync(a => a.Id == 42));
    }

    [Fact]
    public async Task AnimeThatNeedNothingStayNotFetchedWhileTheOtherIs()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Whole(1, airedFrom: Aired), WithoutStat("genres", id: 2));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);
        var service = Service(db, refresh);

        await service.EnsureFetchedAsync(1);
        await service.EnsureFetchedAsync(2);

        Assert.Equal([2], refresh.Calls);
    }

    [Fact]
    public async Task OnceFetchedAnAnimeIsNotFetchedAgain()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);
        var service = Service(db, refresh);

        Assert.True(await service.EnsureFetchedAsync(1));
        Assert.False(await service.EnsureFetchedAsync(1));

        Assert.Equal([1], refresh.Calls);
    }

    [Fact]
    public async Task AnAnimeMyAnimeListHasNothingMoreForIsFetchedEachTime()
    {
        // Still airing with no announced total: the fetch fills nothing in, so the
        // next caller finds the same stat missing (design D6).
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("episodes"));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db, fills: false);
        var service = Service(db, refresh);

        await service.EnsureFetchedAsync(1);
        await service.EnsureFetchedAsync(1);

        Assert.Equal([1, 1], refresh.Calls);
    }

    // ---- the gate ----

    [Fact]
    public async Task ASecondCallerFindsItAlreadyDoneAfterTheGate()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db);
        var gate = new RefreshGate();

        // Another path holds the anime's gate, as the detail page's visit fetch does.
        var held = await gate.LockAsync("anime:1", CancellationToken.None);
        var waiting = Service(db, refresh, gate).EnsureFetchedAsync(1);
        await Task.Delay(100);
        Assert.False(waiting.IsCompleted);

        // That fetch lands while this caller waits, then lets go.
        await new FakeRefresh(db).RefreshOneAsync(1);
        held.Dispose();

        Assert.False(await waiting);
        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task TwoCallersForOneAnimeMakeOneFetch()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var inFetch = new TaskCompletionSource();
        var letItFinish = new TaskCompletionSource();
        var refresh = new FakeRefresh(db) { OnFetch = async () => { inFetch.SetResult(); await letItFinish.Task; } };
        var gate = new RefreshGate();

        var first = Service(db, refresh, gate).EnsureFetchedAsync(1);
        await inFetch.Task;
        var second = Service(db, refresh, gate).EnsureFetchedAsync(1);
        await Task.Delay(100);
        letItFinish.SetResult();

        Assert.True(await first);
        Assert.False(await second);
        Assert.Equal([1], refresh.Calls);
    }

    [Fact]
    public async Task AFetchOfOneAnimeDoesNotHoldUpAnother()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WithoutStat("genres", id: 1), WithoutStat("genres", id: 2));
        await db.SaveChangesAsync();
        var gate = new RefreshGate();

        using var held = await gate.LockAsync("anime:1", CancellationToken.None);
        var fetched = await Service(db, new FakeRefresh(db), gate).EnsureFetchedAsync(2);

        Assert.True(fetched);
    }

    // ---- a failure ----

    [Fact]
    public async Task AFailedFetchIsThrownToTheCaller()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var failure = new HttpRequestException("MAL is down");
        var refresh = new FakeRefresh(db) { Failure = failure };

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => Service(db, refresh).EnsureFetchedAsync(1));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task ANotFoundIsThrownToTheCaller()
    {
        using var db = CreateDb();
        var refresh = new FakeRefresh(db) { Failure = new AnimeMetadataNotFoundException(9) };

        await Assert.ThrowsAsync<AnimeMetadataNotFoundException>(() => Service(db, refresh).EnsureFetchedAsync(9));
    }

    [Fact]
    public async Task AFailedFetchReleasesTheGate()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var gate = new RefreshGate();
        var refresh = new FakeRefresh(db) { Failure = new HttpRequestException("MAL is down") };

        await Assert.ThrowsAsync<HttpRequestException>(() => Service(db, refresh, gate).EnsureFetchedAsync(1));

        using var shortWait = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        using var taken = await gate.LockAsync("anime:1", shortWait.Token);
    }

    [Fact]
    public async Task AFailedFetchIsTriedAgainByTheNextCall()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WithoutStat("genres"));
        await db.SaveChangesAsync();
        var refresh = new FakeRefresh(db) { Failure = new HttpRequestException("MAL is down") };
        var service = Service(db, refresh);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.EnsureFetchedAsync(1));
        refresh.Failure = null;

        Assert.True(await service.EnsureFetchedAsync(1));
        Assert.Equal([1, 1], refresh.Calls);
    }

    /// <summary>Stands in for MyAnimeList behind <see cref="IMetadataRefreshService"/>:
    /// records the call and, unless told to fail or to fill nothing in, leaves the
    /// anime holding every stat.</summary>
    private sealed class FakeRefresh(AnimeTrackerDbContext db, bool fills = true) : IMetadataRefreshService
    {
        public List<int> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public Func<Task>? OnFetch { get; init; }

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (OnFetch is not null)
                await OnFetch();
            if (Failure is not null)
                throw Failure;
            if (!fills)
                return;

            var filled = Whole(animeId, airedFrom: Aired);
            var stored = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (stored is null)
            {
                db.AnimeMetadata.Add(filled);
            }
            else
            {
                stored.AiredFrom = filled.AiredFrom;
                stored.TotalEpisodes = filled.TotalEpisodes;
                stored.MediaType = filled.MediaType;
                stored.MalScore = filled.MalScore;
                stored.Genres = filled.Genres;
                stored.Studios = filled.Studios;
                stored.LastSyncedAt = filled.LastSyncedAt;
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
