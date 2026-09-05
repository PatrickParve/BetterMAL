using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// record-mal-origin-activity design D9 (tasks.md 8.4): ResyncService.RunAsync
// records what it applied per anime with MalResync, inside the same per-anime
// try/save so a failure records nothing for that anime and doesn't stop the run.
// Also anime-updates: a corrective re-sync is still a write to cached anime
// metadata, so it goes through IAnimeMetadataChangeDetector like every other
// refresh path.
public class ResyncServiceActivityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ResyncService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(malClient, db, new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), new SeriesBuildTrigger()), new ResyncProgressTracker(), NullLogger<ResyncService>.Instance);

    private static MalUserAnimeListEdge Edge(int animeId, string status = "watching", int episodesWatched = 0, int? score = null) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched, Score = score ?? 0 },
    };

    [Fact]
    public async Task AReSyncThatChangesFieldsRecordsThemWithMalResync()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", 7)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal(ActivityChangeType.EpisodeIncremented, row.ChangeType);
        Assert.Equal("Episode 7", row.ChangeDetail);
        Assert.Equal(ActivityChangeSource.MalResync, row.Source);
    }

    [Fact]
    public async Task AReSyncOverAnAlreadyMatchingListRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 7 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", 7)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task ANewEntryRecordsAdded()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([Edge(1, "completed", 12)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal(ActivityChangeType.Added, row.ChangeType);
        Assert.Equal("Added as Completed", row.ChangeDetail);
        Assert.Equal(ActivityChangeSource.MalResync, row.Source);
    }

    [Fact]
    public async Task APendingSyncEntryRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3, PendingSync = true });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", 9)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(3, stored.EpisodesWatched); // untouched
    }

    [Fact]
    public async Task APerAnimeFailureRecordsNothingForThatAnimeAndDoesNotStopTheRun()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([Edge(1, "watching", 5), Edge(2, "completed", 12)], failDetailsForAnimeId: 1);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Empty(await db.UserAnimeEntries.Where(e => e.AnimeId == 1).ToListAsync());

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 2).ToListAsync());
        Assert.Equal(ActivityChangeType.Added, row.ChangeType);
    }

    [Fact]
    public async Task AResyncThatRevealsAnUnknownEpisodeCountRecordsAnUpdate()
    {
        using var db = CreateDb();
        // Fully fetched once before, long enough ago that this resync's fetch
        // is a diff rather than a first observation: detection reads
        // LastSyncedAt to tell the two apart (anime-updates: "the first time
        // SHALL mean the anime's first full-detail fetch"), and a row left at
        // default is a never-fetched one, which records nothing by design.
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            TotalEpisodes = null,
            LastSyncedAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(60),
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        await db.SaveChangesAsync();

        var edge = Edge(1, "watching", 7);
        edge.Node.NumEpisodes = 24;
        var malClient = new FakeMalClient([edge]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(1, update.AnimeId);
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    [Fact]
    public async Task AResyncOfANewlyImportedAnimeRecordsNoUpdate()
    {
        using var db = CreateDb();
        var edge = Edge(1, "completed", 12);
        edge.Node.NumEpisodes = 12;
        var malClient = new FakeMalClient([edge]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges, int? failDetailsForAnimeId = null) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
            Task.FromResult(edges);

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            if (animeId == failDetailsForAnimeId)
                throw new InvalidOperationException("Simulated fetch failure.");
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
    }
}
