namespace AnimeTracker.Api.Services.Metadata;

public class AnimeMetadataNotFoundException(int animeId) : Exception($"Anime {animeId} was not found.");
