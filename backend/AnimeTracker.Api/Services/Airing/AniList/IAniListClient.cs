namespace AnimeTracker.Api.Services.Airing.AniList;

/// <summary>One episode's air instant as reported by AniList. Plain DTO — kept
/// free of EF types so the client has no dependency on the persistence layer;
/// callers map it onto the persisted <see cref="Models.EpisodeAiring"/> entity.</summary>
public record AniListEpisode(int Episode, DateTimeOffset AirsAtUtc);

/// <summary>One relation edge as AniList reports it for a queried media: its
/// raw uppercase relation type, and the far end's MAL id when AniList knows
/// one (a manga, a far end AniList hasn't itself mapped to MAL, etc. reports
/// null).</summary>
public record AniListRelationEdge(string RelationType, int? RelatedMalId);

/// <summary>Result of a relations-only batch lookup for one anime: AniList's
/// own Media id (so the caller can persist it the same way the single-media
/// lookup does) plus its relation edges.</summary>
public record AniListRelationsLookup(int AniListId, IReadOnlyList<AniListRelationEdge> Relations);

/// <summary>Result of resolving a MAL id to AniList's own Media id, done once
/// per anime and then cached (see <see cref="Models.AnimeAiringSync"/>).
/// Relations ride along on the same request — free, since the media is
/// already being looked up. <paramref name="Episodes"/> is AniList's own
/// reported total episode count, normalised from 0 to null; it feeds
/// <c>AnimeMetadata.AniListTotalEpisodes</c> as a fallback when MyAnimeList
/// publishes none.</summary>
public record AniListMediaLookup(
    int AniListId, string? Status, DateTimeOffset? NextAiringEpisodeAtUtc, IReadOnlyList<AniListRelationEdge> Relations, int? Episodes);

/// <summary>One media's current state as a read by AniList id reports it: what a
/// lookup reports, without the relations (a media read is for anime whose AniList
/// id, and so whose relations, are already stored). <paramref name="Episodes"/> is
/// normalised from 0 to null, as in <see cref="AniListMediaLookup"/>.</summary>
public record AniListMediaState(int AniListId, string? Status, DateTimeOffset? NextAiringEpisodeAtUtc, int? Episodes);

/// <summary>Result of a full-history schedule fetch for a known AniList id:
/// every episode AniList reports, plus its current status, next-airing
/// instant, and total episode count from the same query — no second
/// round-trip needed to compute a recheck-due time or a total.</summary>
public record AniListScheduleResult(IReadOnlyList<AniListEpisode> Episodes, string? Status, DateTimeOffset? NextAiringEpisodeAtUtc, int? TotalEpisodes);

/// <summary>Reads per-episode air dates from AniList's public GraphQL API,
/// which — unlike MAL — publishes an explicit airing timestamp per episode
/// (including breaks). Anime are matched by MAL id (AniList's <c>idMal</c>)
/// only once; afterwards the resolved AniList id is reused.</summary>
public interface IAniListClient
{
    /// <summary>Most MAL ids one <see cref="LookupBatchByMalIdsAsync"/> takes:
    /// AniList's nested <c>relations</c> selection counts against its per-request
    /// complexity budget.</summary>
    const int MaxLookupBatch = 25;

    /// <summary>Most AniList ids one <see cref="GetMediaBatchAsync"/> takes. A
    /// caller can pass as many to <see cref="GetAiringSchedulesAsync"/>, which
    /// pages.</summary>
    const int MaxMediaBatch = 50;

    /// <summary>Resolves a MAL id to AniList's Media id (plus its current
    /// status and next-airing instant), or null when AniList has no entry
    /// linked to that MAL id. Call once per anime; the caller persists the
    /// result so later refreshes skip this lookup.</summary>
    Task<AniListMediaLookup?> LookupByMalIdAsync(int malId, CancellationToken ct = default);

    /// <summary>The complete airing schedule for a known AniList id, paged
    /// from the beginning until AniList reports no further pages — so a
    /// long-runner's whole history comes back, not just a recent window.
    /// Ordered by air instant.</summary>
    Task<AniListScheduleResult> GetAiringScheduleAsync(int aniListId, CancellationToken ct = default);

    /// <summary>Batched relations-only lookup for anime that need relation
    /// adjudication and nothing else — no airing lookup is due for them, so
    /// this is the only AniList request their relations ride along on.
    /// <paramref name="malIds"/> SHOULD be at most 25 per call (AniList's
    /// nested <c>relations</c> selection counts against its per-request
    /// complexity budget). Ids AniList doesn't resolve are simply absent from
    /// the result.</summary>
    Task<IReadOnlyDictionary<int, AniListRelationsLookup>> GetRelationsBatchAsync(
        IReadOnlyList<int> malIds, CancellationToken ct = default);

    // --- Batched reads (add-first-run-setup design D10) ---
    //
    // Equivalence contract: for every anime a batched method covers, it returns
    // exactly what the single-anime method (LookupByMalIdAsync,
    // GetAiringScheduleAsync) returns for it: the same status, the same episode
    // total (0 read as null), the same next-airing instant, the same relation
    // edges, the same episode rows ordered by air instant. A pass that batches
    // therefore stores what a pass that doesn't would. Each batched method is a
    // single request per page and never loops on a media page's hasNextPage: it
    // isn't reliable there (a media page that is exactly full reports another
    // page, with 5,000 in total, though the next one is empty).

    /// <summary>Resolves up to 25 MAL ids to AniList media in one request, each
    /// with its relations, status, episode total and next airing episode. The
    /// result is keyed by MAL id, and a MAL id AniList has no media for is simply
    /// absent: the batch's reading of the single lookup's "not found". Where
    /// AniList holds two media for one MAL id, the one with the lowest AniList id
    /// is taken and a warning is logged.</summary>
    Task<IReadOnlyDictionary<int, AniListMediaLookup>> LookupBatchByMalIdsAsync(
        IReadOnlyList<int> malIds, CancellationToken ct = default);

    /// <summary>Reads up to 50 media by AniList id in one request, without
    /// relations. The result is keyed by AniList id; an id AniList no longer has is
    /// absent.</summary>
    Task<IReadOnlyDictionary<int, AniListMediaState>> GetMediaBatchAsync(
        IReadOnlyList<int> aniListIds, CancellationToken ct = default);

    /// <summary>Reads the whole airing schedule of many media, 50 rows a page,
    /// ordered by media and then air time, until AniList reports no further page.
    /// <paramref name="onAnimeComplete"/> is awaited with an anime's AniList id and
    /// its rows (ordered by air instant) the moment they have all arrived, so a
    /// caller can store a short show without waiting for a long runner's
    /// hundreds of pages. A media with no rows is called back once the read
    /// ends, with an empty list.
    /// <para>AniList refuses a page deeper than 100 (HTTP 400, "Page depth exceeds
    /// maximum allowed for API requests (5000 entries)"), so the read stops after
    /// page 100. The AniList ids that had not been called back by then, the one
    /// whose rows were cut off and every one not reached, are returned, and the
    /// caller reads them again in smaller batches. Empty when the read
    /// finished.</para></summary>
    Task<IReadOnlyList<int>> GetAiringSchedulesAsync(
        IReadOnlyList<int> aniListIds,
        Func<int, IReadOnlyList<AniListEpisode>, Task> onAnimeComplete,
        CancellationToken ct = default);
}
