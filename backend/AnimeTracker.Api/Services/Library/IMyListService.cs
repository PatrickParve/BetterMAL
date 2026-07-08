namespace AnimeTracker.Api.Services.Library;

/// <summary>Assembles the full my-list read model entirely from cached
/// Postgres data — no live MAL calls on this read path.</summary>
public interface IMyListService
{
    Task<List<MyListItemDto>> GetMyListAsync(CancellationToken ct = default);
}
