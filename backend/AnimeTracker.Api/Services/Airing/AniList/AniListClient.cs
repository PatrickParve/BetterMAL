using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AnimeTracker.Api.Services.Airing.AniList;

public class AniListClient(HttpClient http, AniListRequestPacer pacer, ILogger<AniListClient> logger) : IAniListClient
{
    private const string LookupQuery =
        "query ($idMal: Int) { Media(idMal: $idMal, type: ANIME) { " +
        "id status nextAiringEpisode { episode airingAt } } }";

    // The top-level airingSchedules query (unlike the nested Media.airingSchedule
    // field) filters by mediaId and isn't capped at 500 entries, so it returns a
    // long-runner's whole history once paged. No airingAt_greater floor: a full
    // fetch starts from the beginning. Media(id:) is folded into the same
    // request so status + nextAiringEpisode come back with no extra round-trip.
    private const string ScheduleQuery =
        "query ($mediaId: Int, $page: Int) { " +
        "Media(id: $mediaId) { status nextAiringEpisode { episode airingAt } } " +
        "Page(page: $page, perPage: 50) { pageInfo { hasNextPage } " +
        "airingSchedules(mediaId: $mediaId, sort: TIME) { episode airingAt } } }";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default)
    {
        var response = await PostAsync<LookupResponse>(new { query = LookupQuery, variables = new { idMal = malId } }, ct);
        return response?.Data?.Media is not { } media
            ? null
            : new AniListMediaLookup(media.Id, media.Status, ToInstant(media.NextAiringEpisode));
    }

    public async Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default)
    {
        var episodes = new List<AniListEpisode>();
        string? status = null;
        DateTimeOffset? nextAiringAt = null;

        var page = 1;
        while (true)
        {
            var response = await PostAsync<ScheduleResponse>(
                new { query = ScheduleQuery, variables = new { mediaId = aniListId, page } }, ct);
            if (response?.Data is not { } data)
                break;

            if (data.Media is { } media)
            {
                status = media.Status;
                nextAiringAt = ToInstant(media.NextAiringEpisode);
            }

            if (data.Page?.AiringSchedules is { } nodes)
                episodes.AddRange(nodes.Select(n => new AniListEpisode(n.Episode, DateTimeOffset.FromUnixTimeSeconds(n.AiringAt))));

            if (data.Page?.PageInfo?.HasNextPage != true)
                break;

            page++;
        }

        return new AniListScheduleResult(episodes.OrderBy(e => e.AirsAtUtc).ToList(), status, nextAiringAt);
    }

    private static DateTimeOffset? ToInstant(NextAiringEpisodeNode? node) =>
        node is null ? null : DateTimeOffset.FromUnixTimeSeconds(node.AiringAt);

    private async Task<T?> PostAsync<T>(object body, CancellationToken ct) where T : class
    {
        for (var attempt = 0; ; attempt++)
        {
            await pacer.WaitAsync(ct);
            using var response = await http.PostAsJsonAsync(string.Empty, body, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                logger.LogWarning("AniList rate-limited; retrying in {Seconds}s.", wait.TotalSeconds);
                await Task.Delay(wait, ct);
                continue;
            }

            // A MAL id (or, for the schedule query, an AniList id) unknown to
            // AniList comes back as 404 — a normal "no data" answer here, not
            // an error worth surfacing.
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
    }

    private sealed record LookupResponse(LookupData? Data);
    private sealed record LookupData(LookupMedia? Media);
    private sealed record LookupMedia(int Id, string? Status, NextAiringEpisodeNode? NextAiringEpisode);

    private sealed record ScheduleResponse(ScheduleData? Data);
    private sealed record ScheduleData(ScheduleMedia? Media, SchedulePage? Page);
    private sealed record ScheduleMedia(string? Status, NextAiringEpisodeNode? NextAiringEpisode);
    private sealed record SchedulePage(SchedulePageInfo? PageInfo, List<ScheduleNode>? AiringSchedules);
    private sealed record SchedulePageInfo(bool HasNextPage);
    private sealed record ScheduleNode(int Episode, long AiringAt);
    private sealed record NextAiringEpisodeNode(int Episode, long AiringAt);
}
