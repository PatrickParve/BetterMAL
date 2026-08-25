using System.Net.Http;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Artwork;

// metadata-refresh spec "Visit-triggered picture backfill…" (tasks.md 3.4/3.6):
// the single-anime and series-bounded backfills that populate PictureUrls
// without waiting for the anime's next full-detail refresh.
public class PictureRefreshServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PictureRefreshService CreateService(AnimeTrackerDbContext db, FakeMalClient malClient) =>
        new(db, malClient, new RefreshGate(), NullLogger<PictureRefreshService>.Instance);

    [Fact]
    public async Task RefreshOneAsync_NonListAnimeIsSkippedWithNoMalCall()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient();
        var service = CreateService(db, malClient);

        var result = await service.RefreshOneAsync(1);

        Assert.False(result);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshOneAsync_AlreadyFetchedAnimeIsSkipped()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", PicturesSyncedAt = DateTimeOffset.UtcNow });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient();
        var service = CreateService(db, malClient);

        var result = await service.RefreshOneAsync(1);

        Assert.False(result);
        Assert.Empty(malClient.Calls);
    }

    [Fact]
    public async Task RefreshOneAsync_FailingFetchLeavesPicturesSyncedAtNull()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient { ThrowOnFetch = true };
        var service = CreateService(db, malClient);

        var result = await service.RefreshOneAsync(1);

        Assert.False(result);
        var anime = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Null(anime.PicturesSyncedAt);
    }

    [Fact]
    public async Task RefreshSeriesMainLineAsync_StopsAtBudgetAndReportsRemainder()
    {
        using var db = CreateDb();
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, RootAnimeId = 1, BuiltAt = DateTimeOffset.UtcNow });
        for (var i = 1; i <= 5; i++)
        {
            db.AnimeMetadata.Add(new AnimeMetadata { Id = i, Title = $"Anime {i}" });
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = i });
            db.SeriesMembers.Add(new SeriesMember { AnimeId = i, SeriesId = 1, IsMainLine = true, Order = i });
        }
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient();
        var service = CreateService(db, malClient);

        var remaining = await service.RefreshSeriesMainLineAsync(1, budget: 3);

        Assert.Equal(3, malClient.Calls.Count);
        Assert.Equal(2, remaining);
    }

    [Fact]
    public async Task RefreshSeriesMainLineAsync_NothingEligibleMakesNoCall()
    {
        using var db = CreateDb();
        db.Series.Add(new AnimeTracker.Api.Models.Series { Id = 1, RootAnimeId = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", PicturesSyncedAt = DateTimeOffset.UtcNow });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient();
        var service = CreateService(db, malClient);

        var remaining = await service.RefreshSeriesMainLineAsync(1, budget: 8);

        Assert.Empty(malClient.Calls);
        Assert.Equal(0, remaining);
    }

    private sealed class FakeMalClient : IMalClient
    {
        public List<int> Calls { get; } = [];
        public bool ThrowOnFetch { get; set; }

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (ThrowOnFetch)
                throw new HttpRequestException("MAL unavailable");
            return Task.FromResult(new MalAnimeNode
            {
                Id = animeId,
                Title = $"Anime {animeId}",
                Pictures = [new MalMainPicture { Large = $"https://mal/{animeId}.jpg" }],
            });
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
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
