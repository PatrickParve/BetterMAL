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
/// already being looked up.</summary>
public record AniListMediaLookup(
    int AniListId, string? Status, DateTimeOffset? NextAiringEpisodeAtUtc, IReadOnlyList<AniListRelationEdge> Relations);

/// <summary>Result of a full-history schedule fetch for a known AniList id:
/// every episode AniList reports, plus its current status and next-airing
/// instant from the same query — no second round-trip needed to compute a
/// recheck-due time.</summary>
public record AniListScheduleResult(IReadOnlyList<AniListEpisode> Episodes, string? Status, DateTimeOffset? NextAiringEpisodeAtUtc);

/// <summary>Reads per-episode air dates from AniList's public GraphQL API,
/// which — unlike MAL — publishes an explicit airing timestamp per episode
/// (including breaks). Anime are matched by MAL id (AniList's <c>idMal</c>)
/// only once; afterwards the resolved AniList id is reused.</summary>
public interface IAniListClient
{
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
}
