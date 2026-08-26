namespace AnimeTracker.Api.Services.Ranking;

/// <summary>Thrown when a tier order request places an anime id under a
/// score that doesn't match that entry's actual current score — keeps score
/// dominance impossible to violate through the API.</summary>
public class AnimeRankingTierScoreMismatchException(IReadOnlyCollection<int> animeIds)
    : Exception($"Anime id(s) do not match their stated tier score: {string.Join(", ", animeIds)}");
