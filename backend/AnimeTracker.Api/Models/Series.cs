namespace AnimeTracker.Api.Models;

/// <summary>A franchise derived from the related-anime graph (see
/// Services/Series/SeriesGraphBuilder). <c>Id</c> is stable across rebuilds —
/// a build that overlaps an existing series keeps that series' <c>Id</c>
/// rather than minting a new one — because a later "my top series" feature
/// ranks series by it.</summary>
public class Series
{
    public int Id { get; set; }
    public int RootAnimeId { get; set; } // the earliest main-line member; unique
    public DateTimeOffset BuiltAt { get; set; }
    public bool IsPartial { get; set; } // build hit its MAL-fetch budget
    public bool IsTruncated { get; set; } // build hit the 60-member cap
    public List<SeriesMember> Members { get; set; } = [];
}
