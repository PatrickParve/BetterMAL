using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Import;

// InitialImportService.RunAsync (design.md D8, tasks.md 6.2): what counts as
// work (an anime this device's list lacks, minus a pending removal), when the
// import is shown, and how a run ends.
public class InitialImportServiceWorkTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MalUserAnimeListEdge Edge(int animeId, string status = "watching", int episodesWatched = 0) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched },
    };

    private static void SeedExisting(AnimeTrackerDbContext db, int animeId)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = WatchStatus.Watching });
    }

    private static (InitialImportService Service, ListImportProgress Progress) CreateService(AnimeTrackerDbContext db, IMalClient malClient)
    {
        var progress = new ListImportProgress();
        return (new InitialImportService(malClient, db, progress, NullLogger<InitialImportService>.Instance), progress);
    }

    [Fact]
    public async Task NothingMissingShowsNotStartedAndTheGateWentThrough()
    {
        using var db = CreateDb();
        SeedExisting(db, 1);
        await db.SaveChangesAsync();

        var (service, progress) = CreateService(db, new FakeMalClient([Edge(1)]));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase);
        Assert.True(progress.Gate.WentThroughSinceStart);
    }

    [Fact]
    public async Task TwoMissingOutOfTenOnMalGivesATotalOfTwo()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 8; id++)
            SeedExisting(db, id); // ids 9 and 10 stay missing
        await db.SaveChangesAsync();

        var edges = Enumerable.Range(1, 10).Select(id => Edge(id)).ToList();
        var (service, progress) = CreateService(db, new FakeMalClient(edges));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(2, progress.Snapshot.Total);
        Assert.Equal(JobPhase.Complete, progress.Snapshot.Phase);
    }

    [Fact]
    public async Task AMetadataOnlyAnimeIsWorkWithTheEntryAddedAndNoFetch()
    {
        using var db = CreateDb();
        // Cached metadata (e.g. browsed before ever connecting MAL) but no
        // list entry yet.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "completed", 12)]);
        var (service, progress) = CreateService(db, malClient);

        await service.RunAsync(CancellationToken.None);

        Assert.DoesNotContain(1, malClient.DetailsCalledFor); // no redundant fetch
        Assert.Single(await db.UserAnimeEntries.ToListAsync());
        Assert.Equal(JobPhase.Complete, progress.Snapshot.Phase);
    }

    [Fact]
    public async Task APendingRemovalAnimeIsNeitherWorkNorReAdded()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var (service, progress) = CreateService(db, new FakeMalClient([Edge(1)]));

        await service.RunAsync(CancellationToken.None);

        Assert.Empty(await db.UserAnimeEntries.ToListAsync()); // not re-added
        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase); // no work at all
        Assert.True(progress.Gate.WentThroughSinceStart);
    }

    [Fact]
    public async Task AnUnrecognizedStatusAnimeWithNoCachedMetadataIsNotFetchedAndGetsNoEntry()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "rewatching_v2")]);
        var (service, progress) = CreateService(db, malClient);

        await service.RunAsync(CancellationToken.None);

        Assert.DoesNotContain(1, malClient.DetailsCalledFor);
        Assert.Empty(await db.UserAnimeEntries.ToListAsync());
        var snapshot = progress.Snapshot;
        Assert.Equal(JobPhase.Failed, snapshot.Phase);
        Assert.Equal(0, snapshot.Done);
        Assert.Equal("Left out 1 of 1 anime whose MyAnimeList list status this app doesn't recognize — the backend log names each one.", snapshot.Error);
        Assert.True(progress.Gate.WentThroughSinceStart);
        Assert.NotNull(progress.Gate.LastReadFailure); // a skip-only failure still starts the retry sequence
    }

    [Fact]
    public async Task AnUnrecognizedStatusAnimeWithCachedMetadataGetsNoEntry()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "rewatching_v2")]);
        var (service, progress) = CreateService(db, malClient);

        await service.RunAsync(CancellationToken.None);

        Assert.Empty(await db.UserAnimeEntries.ToListAsync());
        var snapshot = progress.Snapshot;
        Assert.Equal(JobPhase.Failed, snapshot.Phase);
        Assert.Equal("Left out 1 of 1 anime whose MyAnimeList list status this app doesn't recognize — the backend log names each one.", snapshot.Error);
        Assert.True(progress.Gate.WentThroughSinceStart);
    }

    [Fact]
    public async Task OneFetchFailurePlusOneSkippedStatusGivesBothSentences()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1), Edge(2, "rewatching_v2")]);
        malClient.FailDetailsFor(1);
        var (service, progress) = CreateService(db, malClient);

        await service.RunAsync(CancellationToken.None);

        var snapshot = progress.Snapshot;
        Assert.Equal(JobPhase.Failed, snapshot.Phase);
        Assert.Equal(
            "1 of 2 anime couldn't be fetched. Left out 1 of 2 anime whose MyAnimeList list status this app doesn't recognize — the backend log names each one.",
            snapshot.Error);
    }

    [Fact]
    public async Task OneFetchFailureEndsFailedWithGateWentThrough()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1), Edge(2)]);
        malClient.FailDetailsFor(1);
        var (service, progress) = CreateService(db, malClient);

        await service.RunAsync(CancellationToken.None);

        var snapshot = progress.Snapshot;
        Assert.Equal(JobPhase.Failed, snapshot.Phase);
        Assert.Equal("1 of 2 anime couldn't be fetched.", snapshot.Error);
        Assert.True(progress.Gate.WentThroughSinceStart);
        Assert.Single(await db.UserAnimeEntries.ToListAsync()); // the anime that did fetch is still imported
    }

    [Fact]
    public async Task AListReadFailureWithEntriesPresentLeavesTheShownSnapshotUnchangedAndTheGateNotGoneThrough()
    {
        using var db = CreateDb();
        SeedExisting(db, 1); // entries present -> not visible from the start
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([]) { ThrowOnListRead = new HttpRequestException("MAL is down") };
        var (service, progress) = CreateService(db, malClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.RunAsync(CancellationToken.None));

        // The run was quiet (entries already exist), so its failure must not
        // surface on the page — Snapshot stays whatever it was before, even
        // though the tracker itself recorded Failed underneath.
        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase);
        Assert.False(progress.Gate.WentThroughSinceStart);
        Assert.NotNull(progress.Gate.LastReadFailure);
    }

    [Fact]
    public async Task TheSameFailureWithAnEmptyListShowsFailed()
    {
        using var db = CreateDb(); // no entries at all -> visible from the start
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([]) { ThrowOnListRead = new HttpRequestException("MAL is down") };
        var (service, progress) = CreateService(db, malClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.RunAsync(CancellationToken.None));

        Assert.Equal(JobPhase.Failed, progress.Snapshot.Phase);
        Assert.Equal("MyAnimeList couldn't be reached.", progress.Snapshot.Error);
        Assert.False(progress.Gate.WentThroughSinceStart);
    }

    [Fact]
    public async Task ANoWorkRunClearsAnEarlierFailed()
    {
        using var db = CreateDb(); // empty -> the first run is visible from the start
        var malClient = new FakeMalClient([Edge(1)]) { ThrowOnListRead = new HttpRequestException("MAL is down") };
        var (service, progress) = CreateService(db, malClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.RunAsync(CancellationToken.None));
        Assert.Equal(JobPhase.Failed, progress.Snapshot.Phase);

        // The anime is now present (as if a retry had already fetched it),
        // and MAL can be read again.
        SeedExisting(db, 1);
        await db.SaveChangesAsync();
        malClient.ThrowOnListRead = null;

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase);
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public Exception? ThrowOnListRead { get; set; }
        public List<int> DetailsCalledFor { get; } = [];
        private readonly HashSet<int> _failDetailsFor = [];

        public void FailDetailsFor(int animeId) => _failDetailsFor.Add(animeId);

        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            ThrowOnListRead is not null ? throw ThrowOnListRead : Task.FromResult(edges);

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            DetailsCalledFor.Add(animeId);
            if (_failDetailsFor.Contains(animeId))
                throw new HttpRequestException("MAL fetch failed");
            return Task.FromResult(edges.First(e => e.Node.Id == animeId).Node);
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
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
