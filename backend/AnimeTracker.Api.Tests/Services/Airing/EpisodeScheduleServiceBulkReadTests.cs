using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Airing;

// EpisodeScheduleService's two bulk reads (tasks.md 4.4, design.md D6): each
// must answer exactly what its per-anime counterpart answers for the same
// anime and instant, including a local date carrying two rows and a
// DST-shifted local day.
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

        Assert.Equal(single, bulk[anime.Id]);
    }

    [Fact]
    public async Task TwoRowsOnOneLocalDateBulkTakesTheSameEarliestRowAsPerAnime()
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
        Assert.Equal(single, bulk[anime.Id]);
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
        Assert.Equal(single, bulk[anime.Id]);
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
}
