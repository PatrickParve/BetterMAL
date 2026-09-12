using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Mal.Auth;

// The lost-connection column on the single stored OAuth token row
// (design.md D13): the first refusal is kept even if noticed again, and a
// successful exchange (a fresh sign-in or re-authorization) clears it.
public class MalTokenStoreTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static OAuthToken StoredToken(DateTimeOffset? connectionLostAt = null) => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        UpdatedAt = DateTimeOffset.UtcNow,
        ConnectionLostAt = connectionLostAt,
    };

    [Fact]
    public async Task MarkConnectionLostAsyncKeepsTheFirstTime()
    {
        using var db = CreateDb();
        db.OAuthTokens.Add(StoredToken());
        await db.SaveChangesAsync();

        var store = new MalTokenStore(db);
        var firstNoticed = DateTimeOffset.UtcNow;
        await store.MarkConnectionLostAsync(firstNoticed);
        await store.MarkConnectionLostAsync(firstNoticed.AddMinutes(5));

        var token = await store.GetAsync();
        Assert.Equal(firstNoticed, token!.ConnectionLostAt);
    }

    [Fact]
    public async Task SaveAsyncClearsConnectionLostAt()
    {
        using var db = CreateDb();
        db.OAuthTokens.Add(StoredToken(DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var store = new MalTokenStore(db);
        await store.SaveAsync("new-access", "new-refresh", DateTimeOffset.UtcNow.AddDays(1));

        var token = await store.GetAsync();
        Assert.Null(token!.ConnectionLostAt);
        Assert.Equal("new-access", token.AccessToken);
    }
}
