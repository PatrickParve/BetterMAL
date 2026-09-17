using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Import;

// log-only-my-own-edits (design D7): InitialImportService is a sync path and
// records nothing, on any run, whatever it applies.
public class InitialImportServiceActivityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static InitialImportService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(malClient, db, new ListImportProgress(), new FakeAnimeSearchIndex(), NullLogger<InitialImportService>.Instance);

    private static MalUserAnimeListEdge Edge(int animeId, string status = "watching", int episodesWatched = 0) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched },
    };

    [Fact]
    public async Task AFirstImportRecordsNothing()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([Edge(1)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Single(await db.UserAnimeEntries.ToListAsync());
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AnImportRunningOnceActivityExistsRecordsNothing()
    {
        using var db = CreateDb();
        // Something has already been recorded, so history has begun.
        var priorAnime = new AnimeMetadata { Id = 99, Title = "Prior" };
        db.AnimeMetadata.Add(priorAnime);
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 99, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", 3)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task TheBackfilledEntryBranchRecordsNothing()
    {
        using var db = CreateDb();
        // Anime 1's metadata is already cached (e.g. browsed before connecting
        // MAL) but has no entry yet — the metadata-already-cached backfill branch.
        var cachedAnime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(cachedAnime);
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "completed", 12)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Single(await db.UserAnimeEntries.ToListAsync());
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AnAnimeAlreadyHavingAnEntryRecordsNothing()
    {
        using var db = CreateDb();
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 99, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added });
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", 5)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        // InitialImportService only ever creates entries — it never touches an
        // existing one, so no Diff rows are produced either.
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            Task.FromResult(edges);

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
            Task.FromResult(edges.First(e => e.Node.Id == animeId).Node);

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
