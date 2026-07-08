namespace AnimeTracker.Api.Services.Entries;

public class AnimeNotFoundException(int animeId) : Exception($"Anime {animeId} was not found.");

public class CannotCompleteUnknownEpisodeCountException(int animeId)
    : Exception($"Anime {animeId} has an unknown total episode count and cannot be marked Completed.");
