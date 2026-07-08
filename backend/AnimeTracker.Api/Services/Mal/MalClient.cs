using System.Net.Http.Json;
using System.Text.Json;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Mal;

public class MalClient(HttpClient http) : IMalClient
{
    // MAL uses snake_case field names throughout; this maps our PascalCase DTOs
    // without needing a [JsonPropertyName] on every property.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Lean listing fields — used for search/season/ranking/user-list, where
    // most returned anime aren't in my list and rarely change once finished
    // airing. Deliberately excludes genres/synopsis/background/related_anime.
    private const string DefaultAnimeFields =
        "id,title,alternative_titles{en},main_picture,mean,media_type,status,num_episodes,start_date,end_date,studios,broadcast,popularity";

    // Full/rich detail fields — used only for a specific anime's own detail
    // fetch (initial import, nightly tiered my-list refresh, on-demand
    // refresh, or first detail-page visit), never for a listing page.
    private const string FullDetailAnimeFields = DefaultAnimeFields + ",genres,synopsis,background,related_anime,average_episode_duration,source";

    // list_status sub-fields for the user animelist — without these, MAL omits
    // list_status entirely and every imported entry looks like "plan to watch".
    private const string UserAnimeListFields = DefaultAnimeFields +
        ",list_status{status,score,num_episodes_watched,start_date,finish_date,num_times_rewatched,is_rewatching}";

    public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
        GetAsync<MalPagedResponse<MalAnimeListEdge>>(
            $"anime?q={Uri.EscapeDataString(query)}&limit={limit}&fields={DefaultAnimeFields}",
            MalAuthMode.ClientId, ct);

    public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default)
    {
        var url = $"anime/season/{year}/{Uri.EscapeDataString(season)}?limit={limit}&offset={offset}&fields={DefaultAnimeFields}";
        if (!string.IsNullOrEmpty(sort))
            url += $"&sort={Uri.EscapeDataString(sort)}";
        return GetAsync<MalPagedResponse<MalAnimeListEdge>>(url, MalAuthMode.ClientId, ct);
    }

    /// <summary>Pages through the full season listing (the season browser
    /// caches an entire season at once, not one page at a time).</summary>
    public async Task<List<MalAnimeListEdge>> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default)
    {
        const int pageSize = 100;
        var all = new List<MalAnimeListEdge>();
        var offset = 0;

        while (true)
        {
            var page = await GetSeasonAsync(year, season, pageSize, offset, sort, ct);
            all.AddRange(page.Data);

            if (page.Paging?.Next is null || page.Data.Count == 0)
                break;

            offset += pageSize;
        }

        return all;
    }

    public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
        GetAsync<MalPagedResponse<MalAnimeListEdge>>(
            $"anime/ranking?ranking_type={Uri.EscapeDataString(rankingType)}&limit={limit}&fields={DefaultAnimeFields}",
            MalAuthMode.ClientId, ct);

    public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
    {
        var fieldsParam = fields is { Count: > 0 } ? string.Join(",", fields) : FullDetailAnimeFields;
        return GetAsync<MalAnimeNode>($"anime/{animeId}?fields={fieldsParam}", MalAuthMode.ClientId, ct);
    }

    public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default)
    {
        var url = $"users/@me/animelist?fields={UserAnimeListFields}&nsfw=true&limit={limit}&offset={offset}";
        if (!string.IsNullOrEmpty(status))
            url += $"&status={Uri.EscapeDataString(status)}";
        return GetAsync<MalPagedResponse<MalUserAnimeListEdge>>(url, MalAuthMode.Bearer, ct);
    }

    /// <summary>Pages through the full my-list (import and reconciliation both
    /// need the entire list, not one page).</summary>
    public async Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default)
    {
        const int pageSize = 100;
        var all = new List<MalUserAnimeListEdge>();
        var offset = 0;

        while (true)
        {
            var page = await GetUserAnimeListAsync(limit: pageSize, offset: offset, ct: ct);
            all.AddRange(page.Data);

            if (page.Paging?.Next is null || page.Data.Count == 0)
                break;

            offset += pageSize;
        }

        return all;
    }

    public async Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"anime/{animeId}/my_list_status")
        {
            Content = new FormUrlEncodedContent(update.ToFormFields()),
        };
        request.Options.Set(MalRequestOptions.AuthModeKey, MalAuthMode.Bearer);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MalListStatus>(JsonOptions, ct)
            ?? throw new InvalidOperationException("MAL returned an empty my_list_status response.");
    }

    public async Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"anime/{animeId}/my_list_status");
        request.Options.Set(MalRequestOptions.AuthModeKey, MalAuthMode.Bearer);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T> GetAsync<T>(string url, MalAuthMode authMode, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Options.Set(MalRequestOptions.AuthModeKey, authMode);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"MAL returned an empty response for {url}.");
    }
}
