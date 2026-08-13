namespace AnimeTracker.Api.Models;

/// <summary>One anime's membership in a <see cref="Series"/>. Keyed by
/// <c>AnimeId</c> — that's the whole enforcement of "an anime belongs to at
/// most one series": the schema, not code that has to remember.</summary>
public class SeriesMember
{
    public int AnimeId { get; set; } // PK
    public int SeriesId { get; set; }
    public bool IsMainLine { get; set; }
    public int Order { get; set; } // position within the main line, or within its extras media-type group

    /// <summary>A tie-break hint over whatever the tied-for-my-highest-score
    /// set currently is, not a stored ordering of a fixed set — an entry that
    /// drops out of the tie simply keeps an unused rank (design.md decision
    /// 11). Null means unranked, which sorts after every ranked entry.</summary>
    public int? FavouriteRank { get; set; }

    public AnimeMetadata Anime { get; set; } = null!;
}
