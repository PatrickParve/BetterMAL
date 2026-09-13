using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// mal-write-sync "A MyAnimeList list status the app does not recognize is
// never guessed" (design.md D3, D4): an unrecognized status is left out of
// reconciliation entirely — nothing cached, nothing diffed — while the rest
// of the run still completes normally.
public class ReconciliationServiceUnrecognizedStatusTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MalUserAnimeListEdge RemoteEdge(int animeId, string status, int episodesWatched = 0) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched },
    };

    [Fact]
    public async Task AnUnrecognizedStatusEdgeIsLeftOutWhileTheRunStillStoresTheOtherAnimesDiff()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([
            RemoteEdge(1, "rewatching_v2"),
            RemoteEdge(2, "watching", episodesWatched: 3),
        ]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(1, result.SkippedUnrecognized);
        Assert.Equal(1, result.Added);
        var diff = await db.PendingReconciliationDiffs.Include(d => d.Entries).SingleAsync();
        var entry = Assert.Single(diff.Entries);
        Assert.Equal(2, entry.AnimeId);
    }

    [Fact]
    public async Task NoAnimeMetadataRowIsAddedForANewAnimeWithAnUnrecognizedStatus()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([RemoteEdge(1, "rewatching_v2")]);

        await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Empty(await db.AnimeMetadata.ToListAsync());
    }

    [Fact]
    public async Task NoDiffEntryMentionsTheUnrecognizedStatusAnime()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([RemoteEdge(1, "rewatching_v2")]);

        await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            Task.FromResult(edges);

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
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
