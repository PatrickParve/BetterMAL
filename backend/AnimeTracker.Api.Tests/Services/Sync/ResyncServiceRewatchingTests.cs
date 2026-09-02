using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// design.md D1: a corrective re-sync goes through MalListStatus.ApplyTo, the
// same rewatch-preserving mapper ReconciliationService's remote-shape
// construction uses — so ResyncService needs no carve-out of its own
// (refine-sync-status-and-episode-totals tasks.md 2.2/9.2).
public class ResyncServiceRewatchingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ResyncService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(malClient, db, new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db), new SeriesBuildTrigger()), new ResyncProgressTracker(), NullLogger<ResyncService>.Instance);

    private static MalUserAnimeListEdge Edge(int animeId, string status, int episodesWatched, int rewatchCount = 0) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched, NumTimesRewatched = rewatchCount },
    };

    [Fact]
    public async Task ARewatchingEntrySurvivesACorrectiveResyncAgainstARemoteWatchingStatusWhileItsOtherFieldsApply()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Rewatching, EpisodesWatched = 5, RewatchCount = 2,
        });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([Edge(1, "watching", episodesWatched: 7, rewatchCount: 2)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status);
        Assert.Equal(7, stored.EpisodesWatched); // the other field still applied
    }

    [Fact]
    public async Task ANewEntryFromARemoteWatchingStatusIsCreatedAsWatching()
    {
        // ToUserAnimeEntry's fresh entry defaults to Watching, so the
        // rewatch-preserving resolution is inert on the create path
        // (design.md D1).
        using var db = CreateDb();
        var malClient = new FakeMalClient([Edge(1, "watching", episodesWatched: 3)]);

        await CreateService(db, malClient).RunAsync(CancellationToken.None);

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
        Assert.Equal(3, stored.EpisodesWatched);
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
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
    }
}
