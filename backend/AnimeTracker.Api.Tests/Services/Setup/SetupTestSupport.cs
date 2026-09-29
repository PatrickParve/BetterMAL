using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Setup;

/// <summary>A token store that holds whatever the test puts in <see cref="Token"/> and
/// counts the reads.</summary>
internal sealed class FakeTokenStore : IMalTokenStore
{
    public OAuthToken? Token { get; set; }
    public int Reads { get; private set; }

    public Task<OAuthToken?> GetAsync(CancellationToken ct = default)
    {
        Reads++;
        return Task.FromResult(Token);
    }

    public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) => throw new NotImplementedException();

    public static OAuthToken StoredToken(DateTimeOffset? lostAt = null) => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        ConnectionLostAt = lostAt,
    };
}

/// <summary>Everything the setup status read stands on, over an in-memory database
/// the test seeds directly. Both services' health share one clock the test moves.</summary>
internal sealed class SetupStatusFixture : IDisposable
{
    private readonly ServiceProvider _provider;
    private DateTimeOffset _now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    public SetupStatusFixture()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        _provider = services.BuildServiceProvider();

        Db = _provider.CreateScope().ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        Gate = new SetupGate(_provider.GetRequiredService<IServiceScopeFactory>());
        Mal = new MalServiceHealth(() => _now);
        AniList = new AniListServiceHealth(() => _now);
        Service = new SetupStatusService(
            Db, Gate, Coordinator, Options.Create(MalOptions), Tokens, Mal, AniList, new BroadcastLocalTimeConverter());
    }

    public AnimeTrackerDbContext Db { get; }
    public SetupGate Gate { get; }
    public FakeSetupCoordinator Coordinator { get; } = new();
    public FakeTokenStore Tokens { get; } = new() { Token = FakeTokenStore.StoredToken() };
    public MalOptions MalOptions { get; } = new() { ClientId = "id", ClientSecret = "secret" };
    public MalServiceHealth Mal { get; }
    public AniListServiceHealth AniList { get; }
    public SetupStatusService Service { get; }

    public DateTimeOffset Now
    {
        get => _now;
        set => _now = value;
    }

    /// <summary>A list anime. <paramref name="fetched"/> false leaves it a basic row (never
    /// fully fetched); <paramref name="notOnMal"/> gives it the details step's 404 mark.</summary>
    public async Task<AnimeMetadata> SeedAsync(
        int id, string? title = null, bool fetched = false, bool notOnMal = false, bool marked = false,
        string? airingStatus = null, DateOnly? airedFrom = null, WatchStatus status = WatchStatus.PlanToWatch)
    {
        var anime = new AnimeMetadata
        {
            Id = id,
            Title = title ?? $"Anime {id}",
            AiringStatus = airingStatus,
            AiredFrom = airedFrom,
            LastSyncedAt = fetched ? DateTimeOffset.UtcNow : default,
            LastRefreshFailedAt = notOnMal ? DateTimeOffset.UtcNow : null,
        };
        Db.AnimeMetadata.Add(anime);
        Db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id, Status = status });
        if (marked)
            Db.AnimeAiringSyncs.Add(new AnimeAiringSync { AnimeId = id, AniListId = 900 + id, LastFetchedAt = DateTimeOffset.UtcNow });
        await Db.SaveChangesAsync();
        return anime;
    }

    /// <summary>A stored series with these members, all primary, built under the current
    /// rules unless said otherwise.</summary>
    public async Task SeedSeriesAsync(int seriesId, bool partial = false, DateTimeOffset? builtAt = null, params int[] memberIds)
    {
        Db.Series.Add(new Models.Series
        {
            Id = seriesId,
            BuiltAt = builtAt ?? SeriesGraphBuilder.ClassificationRevisedAt.AddDays(30),
            IsPartial = partial,
        });
        foreach (var animeId in memberIds)
            Db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = animeId, IsPrimary = true, IsMainLine = true });
        await Db.SaveChangesAsync();
    }

    public void Dispose() => _provider.Dispose();
}
