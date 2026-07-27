namespace AnimeTracker.Api.Services.Profile;

/// <summary>Thrown when a top-anime order request places an anime id under a
/// tier score that doesn't match that entry's actual current score — keeps
/// score dominance impossible to violate through the API.</summary>
public class TopAnimeTierScoreMismatchException(IReadOnlyCollection<int> animeIds)
    : Exception($"Anime id(s) do not match their stated tier score: {string.Join(", ", animeIds)}");
