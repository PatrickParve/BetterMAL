namespace AnimeTracker.Api.Services.Series;

public class SeriesNotFoundException(int animeId) : Exception($"Anime {animeId} is not part of a series.");
