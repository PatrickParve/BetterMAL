using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// gate-editing-on-aired-episodes list-editing requirements (tasks.md 5.1-5.4):
// the aired-episode predicate and its rejections, the clear-always-allowed
// asymmetry, and the reworked Completed-fill rule.
public class UserAnimeEntryEditServiceAiringGateTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntryEditService CreateService(AnimeTrackerDbContext db, int? airedSoFar = null) =>
        new(
            db,
            new FakeEntrySyncScheduler(),
            new FakeEpisodeScheduleService(airedSoFar),
            new FakeAiringRefreshTrigger(),
            new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)),
            new FakeServiceScopeFactory(),
            NullLogger<UserAnimeEntryEditService>.Instance);

    private static async Task<AnimeMetadata> SeedAsync(
        AnimeTrackerDbContext db, int animeId, int? totalEpisodes, WatchStatus status, int episodesWatched,
        string? airingStatus = null, int rewatchCount = 0, int? myScore = null)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", TotalEpisodes = totalEpisodes, AiringStatus = airingStatus };
        var entry = new UserAnimeEntry
        {
            AnimeId = animeId,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            RewatchCount = rewatchCount,
            MyScore = myScore,
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return anime;
    }

    // --- 5.1: rejections on an anime that has aired no episode ---

    [Fact]
    public async Task EpisodesWatchedAboveZeroIsRejectedWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 0, airingStatus: "not_yet_aired");

        await Assert.ThrowsAsync<EpisodesWatchedRequiresAiredEpisodeException>(() =>
            CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 1 }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(0, stored.EpisodesWatched);
    }

    [Theory]
    [InlineData(WatchStatus.Completed)]
    [InlineData(WatchStatus.Dropped)]
    [InlineData(WatchStatus.OnHold)]
    public async Task StatusIsRejectedWhenNothingHasAired(WatchStatus rejectedStatus)
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired");

        await Assert.ThrowsAsync<StatusRequiresAiredEpisodeException>(() =>
            CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = rejectedStatus }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.PlanToWatch, stored.Status);
    }

    [Fact]
    public async Task ScoreAboveZeroIsRejectedWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired");

        await Assert.ThrowsAsync<ScoreRequiresAiredEpisodeException>(() =>
            CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { MyScore = 7 }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Null(stored.MyScore);
    }

    [Fact]
    public async Task RewatchCountAboveZeroIsRejectedWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired");

        await Assert.ThrowsAsync<RewatchCountRequiresAiredEpisodeException>(() =>
            CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { RewatchCount = 1 }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(0, stored.RewatchCount);
    }

    [Fact]
    public async Task WatchingAndPlanToWatchStayAvailableWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired");

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Watching });

        Assert.Equal(WatchStatus.Watching, result.Status);
    }

    // --- 5.2: clearing is always allowed, even with nothing aired ---

    [Fact]
    public async Task AnExistingScoreCanStillBeClearedWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired", myScore: 8);

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { MyScore = 0 });

        Assert.Null(result.MyScore);
    }

    [Fact]
    public async Task AnExistingRewatchCountCanStillBeClearedWhenNothingHasAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired", rewatchCount: 3);

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { RewatchCount = 0 });

        Assert.Equal(0, result.RewatchCount);
    }

    [Fact]
    public async Task EpisodesWatchedCanStillBeSetToZeroWhenNothingHasAired()
    {
        using var db = CreateDb();
        // A stale value from before these rules existed — walked back to 0.
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 3, airingStatus: "not_yet_aired");

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 0 });

        Assert.Equal(0, result.EpisodesWatched);
    }

    // --- 5.3: the predicate's branches ---

    [Fact]
    public async Task AStoredAiredEpisodeBeatsANotYetAiredStatus()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "not_yet_aired");

        // A score above 0 would be rejected if the anime counted as unaired;
        // here the known aired count of 5 overrides the stale not_yet_aired status.
        var result = await CreateService(db, airedSoFar: 5).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { MyScore = 7 });

        Assert.Equal(7, result.MyScore);
    }

    [Fact]
    public async Task FinishedAiringWithNoStoredRowsCountsAsAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: "finished_airing");

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { MyScore = 7 });

        Assert.Equal(7, result.MyScore);
    }

    [Fact]
    public async Task UnrecordedAiringStatusCountsAsAired()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.PlanToWatch, episodesWatched: 0, airingStatus: null);

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { MyScore = 7 });

        Assert.Equal(7, result.MyScore);
    }

    // --- 5.4: the Completed fill/eligibility rules ---

    [Fact]
    public async Task CompletingAFinishedAnimeFillsToTheTotal()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 24, WatchStatus.Watching, episodesWatched: 24, airingStatus: "finished_airing");

        var result = await CreateService(db, airedSoFar: 24).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(24, result.EpisodesWatched);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task CompletingACurrentlyAiringAnimeFillsToTheAiredCountNotTheTotal()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 7, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 7).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(7, result.EpisodesWatched);
    }

    [Fact]
    public async Task CompletingACurrentlyAiringAnimeSetsNoFinishDate()
    {
        // The anime hasn't actually finished — Completed here means "caught up
        // on what's aired," not "done," so no finish date is stamped (unlike
        // completing a finished anime, covered by CompletingAFinishedAnimeFillsToTheTotal).
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 7, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 7).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Null(result.CompletedAt);
    }

    [Fact]
    public async Task IncrementingToTheAiredCountAutoCompletesACurrentlyAiringAnime()
    {
        // The "+"/set-episodes-watched path auto-completes at the aired-so-far
        // count for a currently-airing anime, mirroring the explicit Completed
        // edit's fill target (design.md D4) — reaching what's aired is "caught
        // up", not just reaching the eventual total.
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 13, WatchStatus.Watching, episodesWatched: 6, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 7).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Equal(7, result.EpisodesWatched);
        Assert.Null(result.CompletedAt);
    }

    [Fact]
    public async Task IncrementingPastTheAiredCountWithoutReachingItDoesNotComplete()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 13, WatchStatus.Watching, episodesWatched: 5, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 7).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 6 });

        Assert.Equal(WatchStatus.Watching, result.Status);
        Assert.Equal(6, result.EpisodesWatched);
    }

    [Fact]
    public async Task LoweringTheCountBelowTheAiredTotalOnACaughtUpCurrentlyAiringAnimeReturnsToWatching()
    {
        // The strict Completed rule's count-drop fallback (design.md D3),
        // exercised against the aired-so-far target rather than the total —
        // always Watching for a currently-airing anime, never Rewatching,
        // since Rewatching's own eligibility rule requires finished airing.
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 13, WatchStatus.Completed, episodesWatched: 7, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 7).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 5 });

        Assert.Equal(WatchStatus.Watching, result.Status);
        Assert.Equal(5, result.EpisodesWatched);
    }

    [Fact]
    public async Task WithNoKnownAiredCountIncrementingOnACurrentlyAiringAnimeNeverAutoCompletes()
    {
        // Unlike the explicit Completed edit (which throws), the passive
        // increment/set path just silently declines to complete when there's
        // no known aired count to compare against — mirroring how it already
        // silently declines for a finished anime with an unknown total.
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 13, WatchStatus.Watching, episodesWatched: 6, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 7 });

        Assert.Equal(WatchStatus.Watching, result.Status);
        Assert.Equal(7, result.EpisodesWatched);
    }

    [Fact]
    public async Task AutoCompletingAtTheTotalWhileStillAiringSetsNoFinishDate()
    {
        // The episodes-watched "+" path auto-completes at a known total
        // (unrelated to this gate, unchanged by it) — it shares the same
        // no-finish-date-while-airing rule as an explicit Completed edit.
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 11, airingStatus: "currently_airing");

        var result = await CreateService(db, airedSoFar: 12).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { EpisodesWatched = 12 });

        Assert.Equal(WatchStatus.Completed, result.Status);
        Assert.Null(result.CompletedAt);
    }

    [Fact]
    public async Task CompletingACurrentlyAiringAnimeWithAnUnknownAiredCountIsRefused()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: 12, WatchStatus.Watching, episodesWatched: 3, airingStatus: "currently_airing");

        await Assert.ThrowsAsync<CannotCompleteUnknownAiredCountException>(() =>
            CreateService(db, airedSoFar: null).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
        Assert.Equal(3, stored.EpisodesWatched);
    }

    [Fact]
    public async Task CompletingAFinishedAnimeWithAnUnknownTotalIsStillRefused()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1, totalEpisodes: null, WatchStatus.Watching, episodesWatched: 3, airingStatus: "finished_airing");

        await Assert.ThrowsAsync<CannotCompleteUnknownEpisodeCountException>(() =>
            CreateService(db, airedSoFar: 3).UpdateEntryAsync(1, new UserAnimeEntryEditRequest { Status = WatchStatus.Completed }));

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
    }

    private sealed class FakeEntrySyncScheduler : IEntrySyncScheduler
    {
        public void ScheduleSync(int animeId) { }
    }

    private sealed class FakeAiringRefreshTrigger : IAiringRefreshTrigger
    {
        public void Enqueue(int animeId) { }
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService(int? airedSoFar) : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(airedSoFar);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
