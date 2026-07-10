namespace AnimeTracker.Api.Services.Airing;

/// <summary>One episode's actual broadcast instant (UTC), from a per-episode
/// schedule source (AniList). Unlike the weekly-cadence estimate, this captures
/// real gaps between episodes — split-cour breaks, one-week hiatuses, etc.</summary>
public record EpisodeAiring(int Episode, DateTimeOffset AirsAtUtc);
