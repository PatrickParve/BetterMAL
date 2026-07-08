namespace AnimeTracker.Api.Services.Detail;

public interface IAnimeDetailService
{
    /// <summary>Reads one anime's full detail from Postgres. Per design.md's
    /// caching-first decision, rich fields (genres/synopsis/background/studio/
    /// aired dates/prequel-sequel) are fetched live exactly once — the first
    /// time this anime's own detail page is opened — if they were never
    /// populated by import, nightly refresh, or a prior visit.</summary>
    Task<AnimeDetailDto> GetDetailAsync(int animeId, CancellationToken ct = default);
}
