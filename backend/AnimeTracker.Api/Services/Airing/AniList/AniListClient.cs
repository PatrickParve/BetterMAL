using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Airing.AniList;

/// <summary>The AniList GraphQL client. <see cref="PostAsync{T}"/> is where every
/// request is paced, where AniList's rate-limit headers are read back into the
/// pacer, and where AniList's health is measured
/// (<see cref="AniListServiceHealth"/>, add-first-run-setup design D11 and D12):
/// one request is one strike or one success, however many anime it covered.</summary>
public class AniListClient(
    HttpClient http,
    AniListRequestPacer pacer,
    AniListServiceHealth health,
    ILogger<AniListClient> logger) : IAniListClient
{
    /// <summary>A 429 is retried this many times, so a request is attempted at most
    /// three times.</summary>
    private const int MaxRateLimitRetries = 2;

    /// <summary>How long to wait when a 429 comes with no <c>Retry-After</c>: AniList's
    /// rate-limit window.</summary>
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromMinutes(1);

    private const string LookupQuery =
        "query ($idMal: Int) { Media(idMal: $idMal, type: ANIME) { " +
        "id status episodes nextAiringEpisode { episode airingAt } " +
        "relations { edges { relationType node { idMal } } } } }";

    // idMal_in restricts Media to exactly the requested batch, so one page
    // always holds every result — no hasNextPage loop needed here, unlike
    // ScheduleQuery. perPage matches the caller's batch size.
    private const string RelationsBatchQuery =
        "query ($idMalIn: [Int], $perPage: Int) { Page(page: 1, perPage: $perPage) { " +
        "media(idMal_in: $idMalIn, type: ANIME) { id idMal relations { edges { relationType node { idMal } } } } } }";

    // The top-level airingSchedules query (unlike the nested Media.airingSchedule
    // field) filters by mediaId and isn't capped at 500 entries, so it returns a
    // long-runner's whole history once paged. No airingAt_greater floor: a full
    // fetch starts from the beginning. Media(id:) is folded into the same
    // request so status + nextAiringEpisode come back with no extra round-trip.
    private const string ScheduleQuery =
        "query ($mediaId: Int, $page: Int) { " +
        "Media(id: $mediaId) { status episodes nextAiringEpisode { episode airingAt } } " +
        "Page(page: $page, perPage: 50) { pageInfo { hasNextPage } " +
        "airingSchedules(mediaId: $mediaId, sort: TIME) { episode airingAt } } }";

    // The batched reads (add-first-run-setup design D10). A lookup asks for at
    // most 25 ids but sends them at perPage 50: two media sharing one idMal then
    // can't push a result off the page. A media page's hasNextPage is not read,
    // because it lies on a full page.
    private const string LookupBatchQuery =
        "query ($idMalIn: [Int], $perPage: Int) { Page(page: 1, perPage: $perPage) { " +
        "media(idMal_in: $idMalIn, type: ANIME) { id idMal status episodes nextAiringEpisode { episode airingAt } " +
        "relations { edges { relationType node { idMal } } } } } }";

    private const string MediaBatchQuery =
        "query ($idIn: [Int], $perPage: Int) { Page(page: 1, perPage: $perPage) { " +
        "media(id_in: $idIn) { id status episodes nextAiringEpisode { episode airingAt } } } }";

    // Unlike the media pages, hasNextPage is exact here. Sorted by media and then
    // time, so one media's rows arrive as one contiguous block.
    private const string SchedulesBatchQuery =
        "query ($mediaIdIn: [Int], $page: Int) { Page(page: $page, perPage: 50) { pageInfo { hasNextPage } " +
        "airingSchedules(mediaId_in: $mediaIdIn, sort: [MEDIA_ID, TIME]) { mediaId episode airingAt } } }";

    private const int BatchPerPage = 50;

    /// <summary>AniList answers HTTP 400 past 5,000 entries, which is page 101 at 50
    /// a page.</summary>
    private const int MaxSchedulePages = 100;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default)
    {
        var response = await PostAsync<LookupResponse>(new { query = LookupQuery, variables = new { idMal = malId } }, ct);
        return response?.Data?.Media is not { } media
            ? null
            : new AniListMediaLookup(
                media.Id, media.Status, ToInstant(media.NextAiringEpisode), ToRelationEdges(media.Relations), NormalizeEpisodes(media.Episodes));
    }

    public async Task<IReadOnlyDictionary<int, AniListRelationsLookup>> GetRelationsBatchAsync(
        IReadOnlyList<int> malIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, AniListRelationsLookup>();
        if (malIds.Count == 0)
            return result;

        var response = await PostAsync<RelationsBatchResponse>(
            new { query = RelationsBatchQuery, variables = new { idMalIn = malIds, perPage = malIds.Count } }, ct);

        foreach (var media in response?.Data?.Page?.Media ?? [])
        {
            if (media.IdMal is not { } idMal)
                continue;
            result[idMal] = new AniListRelationsLookup(media.Id, ToRelationEdges(media.Relations));
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<int, AniListMediaLookup>> LookupBatchByMalIdsAsync(
        IReadOnlyList<int> malIds, CancellationToken ct = default)
    {
        if (malIds.Count > IAniListClient.MaxLookupBatch)
            throw new ArgumentException($"A lookup batch holds at most {IAniListClient.MaxLookupBatch} ids, got {malIds.Count}.", nameof(malIds));

        var result = new Dictionary<int, AniListMediaLookup>();
        if (malIds.Count == 0)
            return result;

        var response = await PostAsync<BatchMediaResponse>(
            new { query = LookupBatchQuery, variables = new { idMalIn = malIds, perPage = BatchPerPage } }, ct);

        // Ascending by AniList id, so that where AniList holds two media for one
        // MAL id the lowest id is the one kept, whatever order it answered in.
        foreach (var media in (response?.Data?.Page?.Media ?? []).OrderBy(m => m.Id))
        {
            if (media.IdMal is not { } idMal)
                continue;

            if (result.ContainsKey(idMal))
            {
                logger.LogWarning(
                    "AniList holds more than one media for MAL id {MalId}; keeping AniList id {AniListId}, ignoring {Ignored}.",
                    idMal, result[idMal].AniListId, media.Id);
                continue;
            }

            result[idMal] = new AniListMediaLookup(
                media.Id, media.Status, ToInstant(media.NextAiringEpisode), ToRelationEdges(media.Relations), NormalizeEpisodes(media.Episodes));
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<int, AniListMediaState>> GetMediaBatchAsync(
        IReadOnlyList<int> aniListIds, CancellationToken ct = default)
    {
        if (aniListIds.Count > IAniListClient.MaxMediaBatch)
            throw new ArgumentException($"A media batch holds at most {IAniListClient.MaxMediaBatch} ids, got {aniListIds.Count}.", nameof(aniListIds));

        var result = new Dictionary<int, AniListMediaState>();
        if (aniListIds.Count == 0)
            return result;

        var response = await PostAsync<BatchMediaResponse>(
            new { query = MediaBatchQuery, variables = new { idIn = aniListIds, perPage = BatchPerPage } }, ct);

        foreach (var media in response?.Data?.Page?.Media ?? [])
            result[media.Id] = new AniListMediaState(media.Id, media.Status, ToInstant(media.NextAiringEpisode), NormalizeEpisodes(media.Episodes));

        return result;
    }

    public async Task<IReadOnlyList<int>> GetAiringSchedulesAsync(
        IReadOnlyList<int> aniListIds,
        Func<int, IReadOnlyList<AniListEpisode>, Task> onAnimeComplete,
        CancellationToken ct = default)
    {
        var requested = aniListIds.Distinct().OrderBy(id => id).ToList();
        if (requested.Count == 0)
            return [];

        var completed = new HashSet<int>();
        int? current = null;
        var rows = new List<AniListEpisode>();

        // The rows of the media being read are all here once the next media's
        // first row arrives, or the last page ends.
        async Task CompleteCurrentAsync()
        {
            if (current is not { } id)
                return;

            var complete = rows.OrderBy(e => e.AirsAtUtc).ToList();
            current = null;
            rows = [];
            completed.Add(id);
            await onAnimeComplete(id, complete);
        }

        for (var page = 1; page <= MaxSchedulePages; page++)
        {
            var response = await PostAsync<SchedulesBatchResponse>(
                new { query = SchedulesBatchQuery, variables = new { mediaIdIn = requested, page } }, ct);

            foreach (var node in response?.Data?.Page?.AiringSchedules ?? [])
            {
                if (!requested.Contains(node.MediaId))
                    continue;

                if (node.MediaId != current)
                {
                    await CompleteCurrentAsync();

                    if (completed.Contains(node.MediaId))
                    {
                        // Sorted by media, so a block never comes back. Were it
                        // to, saving its rows would replace a complete set.
                        logger.LogWarning("AniList returned airing rows for media {MediaId} again after its block ended; ignoring them.", node.MediaId);
                        continue;
                    }

                    current = node.MediaId;
                }

                if (current == node.MediaId)
                    rows.Add(new AniListEpisode(node.Episode, DateTimeOffset.FromUnixTimeSeconds(node.AiringAt)));
            }

            if (response?.Data?.Page?.PageInfo?.HasNextPage != true)
            {
                await CompleteCurrentAsync();

                // A media that never appeared has no rows. Called back empty, so
                // the caller can still record what it learned about it.
                foreach (var id in requested.Where(id => !completed.Contains(id)).ToList())
                {
                    completed.Add(id);
                    await onAnimeComplete(id, []);
                }

                return [];
            }
        }

        // Page 100 ended with more to come: the media being read lost its tail,
        // and everything after it was never reached.
        return requested.Where(id => !completed.Contains(id)).ToList();
    }

    private static List<AniListRelationEdge> ToRelationEdges(RelationsWrapper? relations) =>
        relations?.Edges?
            .Where(e => e.RelationType is not null)
            .Select(e => new AniListRelationEdge(e.RelationType!, e.Node?.IdMal))
            .ToList() ?? [];

    public async Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default)
    {
        var episodes = new List<AniListEpisode>();
        string? status = null;
        DateTimeOffset? nextAiringAt = null;
        int? totalEpisodes = null;

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
                totalEpisodes = NormalizeEpisodes(media.Episodes);
            }

            if (data.Page?.AiringSchedules is { } nodes)
                episodes.AddRange(nodes.Select(n => new AniListEpisode(n.Episode, DateTimeOffset.FromUnixTimeSeconds(n.AiringAt))));

            if (data.Page?.PageInfo?.HasNextPage != true)
                break;

            page++;
        }

        return new AniListScheduleResult(episodes.OrderBy(e => e.AirsAtUtc).ToList(), status, nextAiringAt, totalEpisodes);
    }

    private static DateTimeOffset? ToInstant(NextAiringEpisodeNode? node) =>
        node is null ? null : DateTimeOffset.FromUnixTimeSeconds(node.AiringAt);

    private static int? NormalizeEpisodes(int? episodes) => episodes is null or 0 ? null : episodes;

    private async Task<T?> PostAsync<T>(object body, CancellationToken ct) where T : class
    {
        for (var attempt = 0; ; attempt++)
        {
            await pacer.WaitAsync(ct);
            using var response = await SendAsync(body, ct);
            ObserveRateLimit(response);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Every caller waits it out, not just this one, and the wait is
                // published before it begins so a reader can show it counting down.
                var wait = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
                health.SetThrottledUntil(pacer.BlockFor(wait));

                if (attempt < MaxRateLimitRetries)
                {
                    logger.LogWarning("AniList rate-limited; retrying in {Seconds}s.", wait.TotalSeconds);
                    continue;
                }

                // Still limited after every retry: AniList is not answering.
                health.RecordTemporaryFailure();
                response.EnsureSuccessStatusCode();
            }

            health.ClearThrottle();

            // A MAL id (or, for the schedule query, an AniList id) unknown to
            // AniList comes back as 404 — a normal "no data" answer here, not
            // an error worth surfacing.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                health.RecordSuccess();
                return null;
            }

            // A 5xx is AniList failing; any other refusal (a 400 for a bad
            // query) is AniList answering, so it is no strike against it.
            if ((int)response.StatusCode >= 500)
                health.RecordTemporaryFailure();
            else
                health.RecordSuccess();
            response.EnsureSuccessStatusCode();

            return await ReadAnswerAsync<T>(response, ct);
        }
    }

    /// <summary>Sends one request. A network error or a timeout is a temporary
    /// failure of the service. The token here is the caller's own, so a
    /// cancellation that isn't theirs is a timeout.</summary>
    private async Task<HttpResponseMessage> SendAsync(object body, CancellationToken ct)
    {
        try
        {
            return await http.PostAsJsonAsync(string.Empty, body, ct);
        }
        catch (Exception ex) when (IsTemporaryFailure(ex, ct))
        {
            health.RecordTemporaryFailure();
            throw;
        }
    }

    /// <summary>Reads a successful response's body. A body with GraphQL
    /// <c>errors</c> and no <c>data</c> is a failed request, never an empty
    /// answer: read as "no data" it would record an anime as unknown to AniList
    /// when AniList simply couldn't answer.</summary>
    private async Task<T?> ReadAnswerAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        JsonElement root;
        try
        {
            root = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        }
        catch (Exception ex) when (IsTemporaryFailure(ex, ct))
        {
            // The answer was counted a success above, but it never arrived whole.
            health.RecordTemporaryFailure();
            throw;
        }

        if (GraphQlErrorWithoutData(root) is { } message)
        {
            health.RecordTemporaryFailure();
            throw new HttpRequestException($"AniList answered with an error and no data: {message}");
        }

        return root.ValueKind == JsonValueKind.Object ? root.Deserialize<T>(JsonOptions) : null;
    }

    private static bool IsTemporaryFailure(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or IOException || (ex is OperationCanceledException && !ct.IsCancellationRequested);

    /// <summary>The first error's message when the body reports errors and carries
    /// no data, else null.</summary>
    private static string? GraphQlErrorWithoutData(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
            return null;

        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
            return null;

        return errors[0].ValueKind == JsonValueKind.Object
            && errors[0].TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String
                ? message.GetString()
                : "unknown error";
    }

    private void ObserveRateLimit(HttpResponseMessage response)
    {
        var reset = HeaderNumber(response, "X-RateLimit-Reset");
        pacer.Observe(
            HeaderInt(response, "X-RateLimit-Limit"),
            HeaderInt(response, "X-RateLimit-Remaining"),
            // Epoch seconds. A value outside DateTimeOffset's range is no time at all.
            reset is > 0 and < 253402300799 ? DateTimeOffset.FromUnixTimeSeconds(reset.Value) : null);
    }

    private static int? HeaderInt(HttpResponseMessage response, string name) =>
        HeaderNumber(response, name) is { } number ? (int)Math.Min(number, int.MaxValue) : null;

    private static long? HeaderNumber(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
        && long.TryParse(values.FirstOrDefault(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private sealed record LookupResponse(LookupData? Data);
    private sealed record LookupData(LookupMedia? Media);
    private sealed record LookupMedia(int Id, string? Status, int? Episodes, NextAiringEpisodeNode? NextAiringEpisode, RelationsWrapper? Relations);

    private sealed record BatchMediaResponse(BatchMediaData? Data);
    private sealed record BatchMediaData(BatchMediaPage? Page);
    private sealed record BatchMediaPage(List<BatchMedia>? Media);
    private sealed record BatchMedia(int Id, int? IdMal, string? Status, int? Episodes, NextAiringEpisodeNode? NextAiringEpisode, RelationsWrapper? Relations);

    private sealed record SchedulesBatchResponse(SchedulesBatchData? Data);
    private sealed record SchedulesBatchData(SchedulesBatchPage? Page);
    private sealed record SchedulesBatchPage(SchedulePageInfo? PageInfo, List<SchedulesBatchNode>? AiringSchedules);
    private sealed record SchedulesBatchNode(int MediaId, int Episode, long AiringAt);

    private sealed record RelationsBatchResponse(RelationsBatchData? Data);
    private sealed record RelationsBatchData(RelationsBatchPage? Page);
    private sealed record RelationsBatchPage(List<RelationsBatchMedia>? Media);
    private sealed record RelationsBatchMedia(int Id, int? IdMal, RelationsWrapper? Relations);
    private sealed record RelationsWrapper(List<RelationsEdge>? Edges);
    private sealed record RelationsEdge(string? RelationType, RelationsNode? Node);
    private sealed record RelationsNode(int? IdMal);

    private sealed record ScheduleResponse(ScheduleData? Data);
    private sealed record ScheduleData(ScheduleMedia? Media, SchedulePage? Page);
    private sealed record ScheduleMedia(string? Status, int? Episodes, NextAiringEpisodeNode? NextAiringEpisode);
    private sealed record SchedulePage(SchedulePageInfo? PageInfo, List<ScheduleNode>? AiringSchedules);
    private sealed record SchedulePageInfo(bool HasNextPage);
    private sealed record ScheduleNode(int Episode, long AiringAt);
    private sealed record NextAiringEpisodeNode(int Episode, long AiringAt);
}
