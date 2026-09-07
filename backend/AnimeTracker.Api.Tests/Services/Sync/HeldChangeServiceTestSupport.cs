using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// Shared across the HeldChangeService test files (9.4-9.7): a configurable
// IMalClient covering exactly what HeldChangeService/EntryPushService need —
// a per-anime GetMyListStatusAsync result (or read failure) and a
// recorded/optionally-failing push.
internal sealed class FakeHeldChangeMalClient : IMalClient
{
    private readonly Dictionary<int, MalListStatus?> _statuses = new();
    private readonly HashSet<int> _failReads = [];
    private readonly HashSet<int> _failPushes = [];

    public List<int> UpdatedAnimeIds { get; } = [];
    public List<int> DeletedAnimeIds { get; } = [];

    public void SetStatus(int animeId, MalListStatus? status) => _statuses[animeId] = status;
    public void FailReadFor(int animeId) => _failReads.Add(animeId);
    public void FailPushFor(int animeId) => _failPushes.Add(animeId);

    public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default)
    {
        if (_failReads.Contains(animeId))
            throw new InvalidOperationException("Simulated MyAnimeList read failure.");
        return Task.FromResult(_statuses.TryGetValue(animeId, out var status) ? status : null);
    }

    public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default)
    {
        if (_failPushes.Contains(animeId))
            throw new InvalidOperationException("Simulated push failure.");
        UpdatedAnimeIds.Add(animeId);
        return Task.FromResult(new MalListStatus());
    }

    public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default)
    {
        if (_failPushes.Contains(animeId))
            throw new InvalidOperationException("Simulated push failure.");
        DeletedAnimeIds.Add(animeId);
        return Task.CompletedTask;
    }

    public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();
    public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
        throw new NotImplementedException();
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
}

internal sealed class NoOpEntrySyncScheduler : IEntrySyncScheduler
{
    public void ScheduleSync(int animeId) { }
}

internal static class HeldChangeServiceTestFactory
{
    public static HeldChangeService Create(AnimeTrackerDbContext db, FakeHeldChangeMalClient malClient) => new(
        db,
        malClient,
        new EntryPushService(db, malClient, new NoOpEntrySyncScheduler(), NullLogger<EntryPushService>.Instance),
        NullLogger<HeldChangeService>.Instance);
}
