using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AnimeTracker.Api.Services.Airing.AniList;

public class AniListClient(HttpClient http, ILogger<AniListClient> logger) : IAniListClient
{
    // How far back to pull episode dates — enough that paging a few weeks into
    // the past on the schedule still has real data, without fetching a
    // long-runner's entire history.
    private const int LookbackDays = 150;

    private const string IdQuery =
        "query ($idMal: Int) { Media(idMal: $idMal, type: ANIME) { id } }";

    // The top-level airingSchedules query (unlike the nested Media.airingSchedule
    // field) filters by mediaId + airingAt and isn't capped at 500 entries, so
    // it returns the correct recent+upcoming window even for a 1000-episode
    // long-runner. Sorted ascending by time, so page 1 is the near-term window.
    private const string ScheduleQuery =
        "query ($mediaId: Int, $since: Int) { Page(perPage: 50) { " +
        "airingSchedules(mediaId: $mediaId, airingAt_greater: $since, sort: TIME) { episode airingAt } } }";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<EpisodeAiring>> GetAiringScheduleAsync(int malId, CancellationToken ct = default)
    {
        // airingSchedules keys off AniList's own id, so resolve it from the MAL id first.
        var idResponse = await PostAsync<IdResponse>(new { query = IdQuery, variables = new { idMal = malId } }, ct);
        if (idResponse?.Data?.Media?.Id is not { } anilistId)
            return [];

        var since = DateTimeOffset.UtcNow.AddDays(-LookbackDays).ToUnixTimeSeconds();
        var scheduleResponse = await PostAsync<ScheduleResponse>(
            new { query = ScheduleQuery, variables = new { mediaId = anilistId, since } }, ct);

        var nodes = scheduleResponse?.Data?.Page?.AiringSchedules;
        if (nodes is null || nodes.Count == 0)
            return [];

        return nodes
            .Select(node => new EpisodeAiring(node.Episode, DateTimeOffset.FromUnixTimeSeconds(node.AiringAt)))
            .OrderBy(episode => episode.AirsAtUtc)
            .ToList();
    }

    private async Task<T?> PostAsync<T>(object body, CancellationToken ct) where T : class
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await http.PostAsJsonAsync(string.Empty, body, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                logger.LogWarning("AniList rate-limited; retrying in {Seconds}s.", wait.TotalSeconds);
                await Task.Delay(wait, ct);
                continue;
            }

            // A MAL id AniList doesn't know comes back as 404 — a normal "no
            // data" answer here, not an error worth surfacing.
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
    }

    private sealed record IdResponse(IdData? Data);
    private sealed record IdData(IdMedia? Media);
    private sealed record IdMedia(int Id);

    private sealed record ScheduleResponse(ScheduleData? Data);
    private sealed record ScheduleData(SchedulePage? Page);
    private sealed record SchedulePage(List<ScheduleNode>? AiringSchedules);
    private sealed record ScheduleNode(int Episode, long AiringAt);
}
