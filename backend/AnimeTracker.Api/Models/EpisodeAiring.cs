namespace AnimeTracker.Api.Models;

/// <summary>One episode's confirmed air instant for one anime, as reported by
/// AniList — the sole source behind every airing-timing read (aired-so-far
/// count, weekly schedule slot, next-episode countdown). Stores past and
/// future episodes alike; "aired" is a runtime predicate (AirsAtUtc &lt;= now),
/// not a stored flag, so the same rows back all three reads.</summary>
public class EpisodeAiring
{
    public int AnimeId { get; set; } // MAL id
    public int Episode { get; set; } // AniList's episode number
    public DateTimeOffset AirsAtUtc { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}
