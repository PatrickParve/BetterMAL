using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>Typed MAL API v2 client. Read methods use only X-MAL-Client-ID;
/// user methods require a valid OAuth2 bearer token. Auth header selection,
/// pacing, and 403 backoff are all handled by MalAuthPacingHandler — callers
/// just call the methods below.</summary>
public interface IMalClient
{
    // --- Public/read endpoints (client-id only) ---
    Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default);
    Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default);

    /// <summary>Pages through an entire season's listing (season browsing needs
    /// the whole season, not one page, to cache it).</summary>
    Task<List<MalAnimeListEdge>> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default);
    Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default);
    Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default);

    // --- User endpoints (bearer token) ---
    Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default);
    Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default);
    Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default);
    Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default);
}
