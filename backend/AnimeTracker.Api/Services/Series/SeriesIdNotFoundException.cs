namespace AnimeTracker.Api.Services.Series;

/// <summary>Thrown when a write targets a series id that doesn't exist.</summary>
public class SeriesIdNotFoundException(int seriesId) : Exception($"Series {seriesId} was not found.");
