using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

public class AnimeNotFoundException(int animeId) : Exception($"Anime {animeId} was not found.");

// polish-detail-dates-and-error-messages design.md D8: the one base class
// every entry-edit rejection derives from, so EntriesController can collapse
// its per-type catches into one BadRequest(ex.Message) — these messages are
// read by a person, so none of them may name an id, a parameter, or a type.
public class EntryEditRejectedException(string message) : Exception(message);

public class CannotCompleteUnknownEpisodeCountException(int animeId)
    : EntryEditRejectedException("This anime's total episode count is unknown, so it can't be marked Completed.");

public class EntryNotFoundException(int animeId) : Exception("This anime isn't in my list.");

public class RewatchingNotEligibleException(int animeId, string reason)
    : EntryEditRejectedException($"This anime can't be set to Rewatching: {reason}.");

// gate-editing-on-aired-episodes: rejections for edits that would record
// having watched, rated, or settled an anime that has aired no episode
// (design.md D1/D5), plus the Completed-fill counterpart of
// CannotCompleteUnknownEpisodeCountException for a currently-airing anime
// (design.md D4).

public class EpisodesWatchedRequiresAiredEpisodeException(int animeId)
    : EntryEditRejectedException("No episode of this anime has aired yet, so episodes watched can't be set above 0.");

public class StatusRequiresAiredEpisodeException(int animeId, WatchStatus status)
    : EntryEditRejectedException($"No episode of this anime has aired yet, so status can't be set to {status}.");

public class ScoreRequiresAiredEpisodeException(int animeId)
    : EntryEditRejectedException("No episode of this anime has aired yet, so a score can't be set.");

public class RewatchCountRequiresAiredEpisodeException(int animeId)
    : EntryEditRejectedException("No episode of this anime has aired yet, so rewatch count can't be set above 0.");

public class CannotCompleteUnknownAiredCountException(int animeId)
    : EntryEditRejectedException("This anime is currently airing and how many episodes have aired isn't known, so it can't be marked Completed.");
