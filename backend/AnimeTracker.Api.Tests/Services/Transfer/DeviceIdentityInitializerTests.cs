using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// device-transfer's "Each database carries one device identifier"
// (design.md D6, tasks.md 5.3): EnsureAsync is idempotent, touches nothing
// but DeviceIdentities, and two separately created databases diverge.
public class DeviceIdentityInitializerTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task OnAnEmptyDatabaseItCreatesExactlyOneRowWithANonEmptyId()
    {
        using var db = CreateDb();

        await new DeviceIdentityInitializer(db).EnsureAsync();

        var identity = await db.DeviceIdentities.SingleAsync();
        Assert.NotEqual(Guid.Empty, identity.DeviceId);
    }

    [Fact]
    public async Task ASecondCallLeavesTheSameIdAndStillExactlyOneRow()
    {
        using var db = CreateDb();
        var initializer = new DeviceIdentityInitializer(db);
        await initializer.EnsureAsync();
        var firstId = (await db.DeviceIdentities.SingleAsync()).DeviceId;

        await initializer.EnsureAsync();

        var identity = await db.DeviceIdentities.SingleAsync();
        Assert.Equal(firstId, identity.DeviceId);
    }

    [Fact]
    public async Task ItTouchesNoOtherSet()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        db.RankingStates.Add(new RankingState { ModifiedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await new DeviceIdentityInitializer(db).EnsureAsync();

        Assert.Equal(1, await db.AnimeMetadata.CountAsync());
        Assert.Equal(1, await db.RankingStates.CountAsync());
    }

    [Fact]
    public async Task TwoSeparateDatabasesGetDifferentIds()
    {
        using var dbOne = CreateDb();
        using var dbTwo = CreateDb();

        await new DeviceIdentityInitializer(dbOne).EnsureAsync();
        await new DeviceIdentityInitializer(dbTwo).EnsureAsync();

        var idOne = (await dbOne.DeviceIdentities.SingleAsync()).DeviceId;
        var idTwo = (await dbTwo.DeviceIdentities.SingleAsync()).DeviceId;
        Assert.NotEqual(idOne, idTwo);
    }
}
