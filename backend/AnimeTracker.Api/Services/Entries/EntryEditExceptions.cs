using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

public class AnimeNotFoundException(int animeId) : Exception($"Anime {animeId} was not found.");

public class CannotCompleteUnknownEpisodeCountException(int animeId)
    : Exception($"Anime {animeId} has an unknown total episode count and cannot be marked Completed.");

public class EntryNotFoundException(int animeId) : Exception($"Anime {animeId} is not in my list.");

public class RewatchingNotEligibleException(int animeId, string reason)
    : Exception($"Anime {animeId} cannot be set to Rewatching: {reason}.");

// gate-editing-on-aired-episodes: rejections for edits that would record
// having watched, rated, or settled an anime that has aired no episode
// (design.md D1/D5), plus the Completed-fill counterpart of
// CannotCompleteUnknownEpisodeCountException for a currently-airing anime
// (design.md D4).

public class EpisodesWatchedRequiresAiredEpisodeException(int animeId)
    : Exception($"Anime {animeId} has aired no episode; episodes watched cannot be set above 0.");

public class StatusRequiresAiredEpisodeException(int animeId, WatchStatus status)
    : Exception($"Anime {animeId} has aired no episode; status cannot be set to {status}.");

public class ScoreRequiresAiredEpisodeException(int animeId)
    : Exception($"Anime {animeId} has aired no episode; a score cannot be set.");

public class RewatchCountRequiresAiredEpisodeException(int animeId)
    : Exception($"Anime {animeId} has aired no episode; rewatch count cannot be set above 0.");

public class CannotCompleteUnknownAiredCountException(int animeId)
    : Exception($"Anime {animeId} is currently airing with an unknown aired-so-far count and cannot be marked Completed.");
