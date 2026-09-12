using System.Net;
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
    // `rating` is included here (not just in the full detail set) so every
    // listing path records MAL's content rating with no extra request — it
    // backs the season browser's hentai filter.
    private const string DefaultAnimeFields =
        "id,title,alternative_titles{en},main_picture,mean,media_type,status,num_episodes,start_date,end_date,studios,broadcast,popularity,rank,start_season,rating";

    // Full/rich detail fields — used only for a specific anime's own detail
    // fetch (initial import, nightly tiered my-list refresh, on-demand
    // refresh, or first detail-page visit), never for a listing page.
    // `related_anime{node{media_type}}` — MAL does honor nested field
    // selection here (verified by hand: bare `related_anime` omits
    // media_type, this form returns it per node alongside the always-present
    // id/title/main_picture) — so the More overlay's media types come free
    // with this fetch, with no separate backfill call needed.
    private const string FullDetailAnimeFields = DefaultAnimeFields + ",genres,synopsis,background,related_anime{node{media_type}},average_episode_duration,source";

    // Same as FullDetailAnimeFields, plus the picture set — used only when the
    // anime being fetched is in my list (Services/Metadata/MetadataRefreshService).
    // MAL rate-limits per request rather than per field, so carrying pictures
    // on a fetch that is happening anyway is free; FullDetailAnimeFields itself
    // is untouched so a non-my-list fetch keeps asking for exactly what it did
    // before.
    public const string FullDetailWithPicturesAnimeFields = FullDetailAnimeFields + ",pictures";

    // list_status sub-fields for the user animelist — without these, MAL omits
    // list_status entirely and every imported entry looks like "plan to watch".
    private const string UserAnimeListFields = DefaultAnimeFields +
        ",list_status{status,score,num_episodes_watched,start_date,finish_date,num_times_rewatched,is_rewatching}";

    public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
        // nsfw=true — without it MAL silently omits R+/Rx-rated entries from
        // search results, same as the season listing and user animelist fetches.
        GetAsync<MalPagedResponse<MalAnimeListEdge>>(
            $"anime?q={Uri.EscapeDataString(query)}&limit={limit}&fields={DefaultAnimeFields}&nsfw=true",
            MalAuthMode.ClientId, ct);

    public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
        // nsfw=true — without it MAL silently omits R+/Rx-rated entries from the
        // season listing, same as the user animelist fetch below.
        GetAsync<MalPagedResponse<MalAnimeListEdge>>(BuildSeasonUrl(year, season, limit, offset, sort), MalAuthMode.ClientId, ct);

    /// <summary>Pages through the full season listing (the season browser
    /// caches an entire season at once, not one page at a time). Only the
    /// first page is 404-tolerant: MAL 404s the whole season when it has no
    /// listing yet, and returning null here is what lets
    /// SeasonBrowseService record that as an answer rather than a failure. A
    /// 404 on a later page would mean the listing vanished mid-page-through —
    /// a real error — so every page after the first goes through the
    /// ordinary (non-tolerant) GetSeasonAsync.</summary>
    public async Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default)
    {
        var firstPage = await GetAsyncOrNotFound<MalPagedResponse<MalAnimeListEdge>>(
            BuildSeasonUrl(year, season, FullListPageSize, 0, sort), MalAuthMode.ClientId, ct);
        if (firstPage is null)
            return null; // MAL has no listing for this season (404) — an answer, not a fetch failure

        var all = new List<MalAnimeListEdge>(firstPage.Data);
        var next = firstPage.Paging?.Next;
        var lastPageCount = firstPage.Data.Count;
        var offset = FullListPageSize;

        while (next is not null && lastPageCount > 0)
        {
            var page = await GetSeasonAsync(year, season, FullListPageSize, offset, sort, ct);
            all.AddRange(page.Data);
            next = page.Paging?.Next;
            lastPageCount = page.Data.Count;
            offset += FullListPageSize;
        }

        return all;
    }

    private static string BuildSeasonUrl(int year, string season, int limit, int offset, string? sort)
    {
        var url = $"anime/season/{year}/{Uri.EscapeDataString(season)}?limit={limit}&offset={offset}&fields={DefaultAnimeFields}&nsfw=true";
        if (!string.IsNullOrEmpty(sort))
            url += $"&sort={Uri.EscapeDataString(sort)}";
        return url;
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
    public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
        GetAllPagesAsync(offset => GetUserAnimeListAsync(limit: FullListPageSize, offset: offset, ct: ct), onPageRead);

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

    public async Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default)
    {
        var node = await GetAsyncOrNotFound<MalAnimeNode>($"anime/{animeId}?fields=my_list_status", MalAuthMode.Bearer, ct);
        return node?.MyListStatus;
    }

    public async Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"anime/{animeId}/my_list_status");
        request.Options.Set(MalRequestOptions.AuthModeKey, MalAuthMode.Bearer);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return; // already absent on MAL — the goal state already holds

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

    /// <summary>Same as <see cref="GetAsync{T}"/> but a 404 returns null
    /// instead of throwing — the same "already absent on MAL" tolerance
    /// <see cref="DeleteMyListStatusAsync"/> gives a missing list entry.</summary>
    private async Task<T?> GetAsyncOrNotFound<T>(string url, MalAuthMode authMode, CancellationToken ct) where T : class
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Options.Set(MalRequestOptions.AuthModeKey, authMode);

        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
            ?? throw new InvalidOperationException($"MAL returned an empty response for {url}.");
    }

    private const int FullListPageSize = 100;

    /// <summary>Pages through a MAL cursor-paginated endpoint (season listing,
    /// full my-list) until a page comes back empty or without a next link,
    /// concatenating every page's data. <paramref name="onPageRead"/>, when
    /// given, is called after each page with the running entry count.</summary>
    private static async Task<List<T>> GetAllPagesAsync<T>(Func<int, Task<MalPagedResponse<T>>> fetchPage, Action<int>? onPageRead = null)
    {
        var all = new List<T>();
        var offset = 0;

        while (true)
        {
            var page = await fetchPage(offset);
            all.AddRange(page.Data);
            onPageRead?.Invoke(all.Count);

            if (page.Paging?.Next is null || page.Data.Count == 0)
                break;

            offset += FullListPageSize;
        }

        return all;
    }
}
