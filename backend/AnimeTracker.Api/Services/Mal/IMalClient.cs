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
    /// the whole season, not one page, to cache it). Returns null when MAL
    /// answers 404 for the season's first page — the only status treated as
    /// data about the season ("MAL has no listing for it yet") rather than a
    /// fetch failure; every other non-success status still throws.</summary>
    Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default);
    Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default);
    Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default);

    // --- User endpoints (bearer token) ---
    Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default);
    Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default);
    Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default);

    /// <summary>Removes an anime from my MAL list. A 404 (MAL already has no
    /// such list entry) counts as success — the desired end state already holds.</summary>
    Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default);

    /// <summary>Reads MyAnimeList's current list status for one anime,
    /// bearer-authenticated so `my_list_status` is actually populated
    /// (GetAnimeDetailsAsync is client-id authenticated and never returns it).
    /// Returns null when MyAnimeList has no list entry for the anime.</summary>
    Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default);
}
