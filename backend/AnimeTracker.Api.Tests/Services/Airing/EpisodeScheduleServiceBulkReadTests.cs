using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Airing;

// EpisodeScheduleService's two bulk reads (tasks.md 4.4, design.md D6): each
// must answer what its per-anime counterpart answers for the same anime and
// instant, including a local date carrying two rows and a DST-shifted local
// day. The local-date bulk read reports every row on the date per anime, in
// air order, where the per-anime read reports the earliest.
public class EpisodeScheduleServiceBulkReadTests
{
    private static readonly IBroadcastLocalTimeConverter LocalTime = new BroadcastLocalTimeConverter();

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EpisodeScheduleService CreateService(AnimeTrackerDbContext db) =>
        new(new EpisodeAiringRepository(db), LocalTime);

    [Fact]
    public async Task ResolveOnLocalDateBulkAgreesWithPerAnimeForTheSameAnimeAndDate()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "One" };
        var date = new DateOnly(2024, 6, 1);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(9) });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var single = await service.ResolveOnLocalDateAsync(anime, date);
        var bulk = await service.ResolveOnLocalDateAsync([anime], date);

        Assert.Equal(single, Assert.Single(bulk[anime.Id]));
    }

    [Fact]
    public async Task TwoRowsOnOneLocalDateBulkReportsBothAndItsFirstIsThePerAnimeAnswer()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "One" };
        var date = new DateOnly(2024, 6, 1);
        db.EpisodeAirings.AddRange(
            new EpisodeAiring { AnimeId = 1, Episode = 6, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(21) },
            new EpisodeAiring { AnimeId = 1, Episode = 5, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(9) });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var single = await service.ResolveOnLocalDateAsync(anime, date);
        var bulk = await service.ResolveOnLocalDateAsync([anime], date);

        Assert.Equal(5, single!.EpisodeNumber); // the earlier of the two rows
        Assert.Equal(single, bulk[anime.Id][0]);
        Assert.Equal([5, 6], bulk[anime.Id].Select(e => e.EpisodeNumber));
        Assert.Equal(
            [LocalTime.LocalMidnightUtc(date).AddHours(9), LocalTime.LocalMidnightUtc(date).AddHours(21)],
            bulk[anime.Id].Select(e => e.AirsAtUtc));
        Assert.All(bulk[anime.Id], e => Assert.Equal(LocalTime.GetLocalTime(e.AirsAtUtc), e.LocalTime));
    }

    [Fact]
    public async Task BulkReportsEachAnimesRowsApartAndOmitsAnAnimeWithNothingOnTheDate()
    {
        using var db = CreateDb();
        var one = new AnimeMetadata { Id = 1, Title = "One" };
        var two = new AnimeMetadata { Id = 2, Title = "Two" };
        var idle = new AnimeMetadata { Id = 3, Title = "Idle" };
        var date = new DateOnly(2024, 6, 1);
        db.EpisodeAirings.AddRange(
            new EpisodeAiring { AnimeId = 1, Episode = 1, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(10) },
            new EpisodeAiring { AnimeId = 1, Episode = 2, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(10) },
            new EpisodeAiring { AnimeId = 2, Episode = 9, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddHours(8) },
            new EpisodeAiring { AnimeId = 3, Episode = 4, AirsAtUtc = LocalTime.LocalMidnightUtc(date).AddDays(1).AddHours(8) });
        await db.SaveChangesAsync();

        var bulk = await CreateService(db).ResolveOnLocalDateAsync([one, two, idle], date);

        Assert.Equal([1, 2], bulk.Keys.Order());
        Assert.Equal(2, bulk[1].Count);
        Assert.Equal(9, Assert.Single(bulk[2]).EpisodeNumber);
    }

    [Fact]
    public async Task AnEmptyBulkRequestMakesNoRead()
    {
        using var db = CreateDb();
        var repository = new CountingEpisodeAiringRepository(new EpisodeAiringRepository(db));
        var service = new EpisodeScheduleService(repository, LocalTime);

        var bulk = await service.ResolveOnLocalDateAsync([], new DateOnly(2024, 6, 1));

        Assert.Empty(bulk);
        Assert.Equal(0, repository.RangeReads);
    }

    [Fact]
    public async Task ABulkRequestForManyAnimeMakesOneRead()
    {
        using var db = CreateDb();
        var repository = new CountingEpisodeAiringRepository(new EpisodeAiringRepository(db));
        var service = new EpisodeScheduleService(repository, LocalTime);
        var anime = Enumerable.Range(1, 600).Select(id => new AnimeMetadata { Id = id, Title = $"A{id}" }).ToList();

        await service.ResolveOnLocalDateAsync(anime, new DateOnly(2024, 6, 1));

        Assert.Equal(1, repository.RangeReads);
    }

    // Europe/Helsinki DST begins on the last Sunday of March, so this local
    // date is only 23 hours long. A seeded mid-day row stays inside it
    // regardless, and the two reads must still land on the same row.
    [Fact]
    public async Task ADstShiftedLocalDayAgreesBetweenBulkAndPerAnime()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "One" };
        var dstDate = new DateOnly(2024, 3, 31);
        db.EpisodeAirings.Add(new EpisodeAiring { AnimeId = 1, Episode = 7, AirsAtUtc = LocalTime.LocalMidnightUtc(dstDate).AddHours(12) });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var single = await service.ResolveOnLocalDateAsync(anime, dstDate);
        var bulk = await service.ResolveOnLocalDateAsync([anime], dstDate);

        Assert.NotNull(single);
        Assert.Equal(single, Assert.Single(bulk[anime.Id]));
    }

    [Fact]
    public async Task NextAiringInstantBulkAgreesWithPerAnimeForTheSameAnimeAndInstant()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "One" };
        var afterUtc = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        db.EpisodeAirings.AddRange(
            new EpisodeAiring { AnimeId = 1, Episode = 3, AirsAtUtc = afterUtc.AddDays(1) },
            new EpisodeAiring { AnimeId = 1, Episode = 4, AirsAtUtc = afterUtc.AddDays(8) });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var single = await service.NextAiringInstantAsync(anime, afterUtc);
        var bulk = await service.NextAiringInstantAsync([anime], afterUtc);

        Assert.Equal(afterUtc.AddDays(1), single);
        Assert.Equal(single, bulk[anime.Id]);
    }

    private sealed class CountingEpisodeAiringRepository(IEpisodeAiringRepository inner) : IEpisodeAiringRepository
    {
        public int RangeReads { get; private set; }

        public Task<List<EpisodeAiring>> GetRowsInRangeAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
        {
            RangeReads++;
            return inner.GetRowsInRangeAsync(animeIds, fromUtc, toUtc, ct);
        }

        public Task<int?> GetMaxAiredEpisodeAsync(int animeId, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
            inner.GetMaxAiredEpisodeAsync(animeId, asOfUtc, ct);
        public Task<Dictionary<int, int>> GetMaxAiredEpisodesAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset asOfUtc, CancellationToken ct = default) =>
            inner.GetMaxAiredEpisodesAsync(animeIds, asOfUtc, ct);
        public Task<DateTimeOffset?> GetNextAiringInstantAsync(int animeId, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            inner.GetNextAiringInstantAsync(animeId, afterUtc, ct);
        public Task<Dictionary<int, DateTimeOffset>> GetNextAiringInstantsAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            inner.GetNextAiringInstantsAsync(animeIds, afterUtc, ct);
        public Task ReplaceForAnimeAsync(int animeId, IReadOnlyList<EpisodeAiring> rows, CancellationToken ct = default) =>
            inner.ReplaceForAnimeAsync(animeId, rows, ct);
    }
}
