using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Metadata;

// RefreshTiers (metadata-refresh spec, "Scheduled tiered staleness refresh
// for my-list anime"): the not-yet-aired ladder replaces the old shared
// 1-day tier, and TtlFor/IsDue must stay in lockstep since one drives the
// in-memory TTL check and the other the nightly batch's DB-side "due" query.
public class RefreshTiersTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    [Theory]
    [InlineData(null, 3)] // premiere unknown: every 3 days sits strictly between the two known-date tiers
    [InlineData(-5, 1)] // premiere already passed, MAL still says not_yet_aired: stays daily
    [InlineData(12, 1)] // premiere within 30 days: daily
    [InlineData(30, 1)] // exactly the 30-day boundary: still daily
    [InlineData(31, 7)] // premiere just past 30 days out: weekly
    [InlineData(240, 7)] // premiere far out: weekly
    public void TtlFor_UnairedLadder(int? daysFromToday, int expectedDays)
    {
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            AiredFrom = daysFromToday is { } d ? Today.AddDays(d) : null,
        };

        var ttl = RefreshTiers.TtlFor(anime);

        Assert.Equal(TimeSpan.FromDays(expectedDays), ttl);
    }

    [Fact]
    public void TtlFor_UnknownPremiereDateIsFasterThanADistantOne()
    {
        var unknown = new AnimeMetadata { Id = 1, Title = "A", AiringStatus = "not_yet_aired", AiredFrom = null };
        var distant = new AnimeMetadata { Id = 2, Title = "B", AiringStatus = "not_yet_aired", AiredFrom = Today.AddDays(240) };

        Assert.True(RefreshTiers.TtlFor(unknown) < RefreshTiers.TtlFor(distant));
    }

    [Fact]
    public void TtlFor_CurrentlyAiringStaysDaily()
    {
        var anime = new AnimeMetadata { Id = 1, Title = "A", AiringStatus = "currently_airing" };
        Assert.Equal(TimeSpan.FromDays(1), RefreshTiers.TtlFor(anime));
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData(-5, 1)]
    [InlineData(12, 1)]
    [InlineData(31, 7)]
    public async Task IsDue_UnairedLadderMatchesTtlFor(int? daysFromToday, int expectedTierDays)
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "A",
            AiringStatus = "not_yet_aired",
            AiredFrom = daysFromToday is { } d ? Today.AddDays(d) : null,
            LastSyncedAt = now - TimeSpan.FromDays(expectedTierDays) - TimeSpan.FromHours(1),
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var dueJustPastTier = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.True(dueJustPastTier);

        anime.LastSyncedAt = now - TimeSpan.FromDays(expectedTierDays) + TimeSpan.FromHours(1);
        await db.SaveChangesAsync();

        var dueWellInsideTier = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.False(dueWellInsideTier);
    }

    [Fact]
    public async Task IsDue_UnairedUnknownDateSitsBetweenTheTwoKnownDateTiers()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;

        // 4 days stale: overdue for the 3-day unknown-date tier, but still
        // inside the 7-day distant-premiere tier.
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Unknown premiere",
            AiringStatus = "not_yet_aired",
            AiredFrom = null,
            LastSyncedAt = now - TimeSpan.FromDays(4),
        });
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 2,
            Title = "Distant premiere",
            AiringStatus = "not_yet_aired",
            AiredFrom = Today.AddDays(240),
            LastSyncedAt = now - TimeSpan.FromDays(4),
        });
        await db.SaveChangesAsync();

        var due = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).Select(a => a.Id).ToListAsync();

        Assert.Contains(1, due);
        Assert.DoesNotContain(2, due);
    }

    // --- LastRefreshFailedAt: a not-found attempt is measured like a refresh ---

    [Fact]
    public async Task IsDue_FinishedAnimeWaitsOutItsTierAfterAFailedAttempt()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "A",
            AiringStatus = "finished_airing",
            AiredTo = Today.AddDays(-30), // within the last year: the 3-day RecentTtl tier
            LastSyncedAt = now - TimeSpan.FromDays(60),
            LastRefreshFailedAt = now - TimeSpan.FromDays(2),
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var dueWithRecentFailure = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.False(dueWithRecentFailure);

        anime.LastRefreshFailedAt = now - TimeSpan.FromDays(3) - TimeSpan.FromHours(1);
        await db.SaveChangesAsync();

        var dueWithStaleFailure = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.True(dueWithStaleFailure);
    }

    [Fact]
    public async Task IsDue_NeverFetchedAnimeWaitsOutItsTierAfterAFailedAttempt()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "A",
            LastSyncedAt = default,
            LastRefreshFailedAt = now - TimeSpan.FromHours(1),
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var dueWithRecentFailure = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.False(dueWithRecentFailure);

        // No AiringStatus/AiredTo: falls to the 28-day StaleTtl branch.
        anime.LastRefreshFailedAt = now - RefreshTiers.StaleTtl - TimeSpan.FromHours(1);
        await db.SaveChangesAsync();

        var dueWithStaleFailure = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.True(dueWithStaleFailure);
    }

    [Fact]
    public async Task IsDue_NeverFetchedAnimeWithNoFailureIsStillDue()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "A", LastSyncedAt = default });
        await db.SaveChangesAsync();

        var due = await db.AnimeMetadata.Where(RefreshTiers.IsDue(now)).AnyAsync();
        Assert.True(due);
    }

    [Fact]
    public async Task LastAttemptAt_OrdersARecentFailureAfterASyncBetweenTheTwo()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1,
            Title = "Old sync, recent failure",
            LastSyncedAt = now - TimeSpan.FromDays(10),
            LastRefreshFailedAt = now - TimeSpan.FromHours(1),
        });
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 2,
            Title = "Sync between the two",
            LastSyncedAt = now - TimeSpan.FromDays(1),
        });
        await db.SaveChangesAsync();

        var orderedIds = await db.AnimeMetadata.OrderBy(RefreshTiers.LastAttemptAt).Select(a => a.Id).ToListAsync();

        Assert.Equal([2, 1], orderedIds);
    }
}
