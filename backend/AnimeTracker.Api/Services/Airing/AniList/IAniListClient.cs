namespace AnimeTracker.Api.Services.Airing.AniList;

/// <summary>Reads per-episode air dates from AniList's public GraphQL API,
/// which — unlike MAL — publishes an explicit airing timestamp per episode
/// (including breaks). Anime are matched by MAL id (AniList's <c>idMal</c>), so
/// no title matching is needed.</summary>
public interface IAniListClient
{
    /// <summary>Recent and upcoming per-episode air instants for the anime with
    /// this MAL id, or an empty list when AniList has no schedule or no entry
    /// linked to that MAL id. Ordered by episode number.</summary>
    Task<IReadOnlyList<EpisodeAiring>> GetAiringScheduleAsync(int malId, CancellationToken ct = default);
}
