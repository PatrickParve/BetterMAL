namespace AnimeTracker.Api.Models;

/// <summary>A franchise derived from the related-anime graph (see
/// Services/Series/SeriesGraphBuilder). <c>Id</c> is the MAL id of the
/// series' root entry — the earliest main-line member — so the same
/// franchise is identified by the same number on every installation and
/// after any rebuild from empty (spec `series-page` "A series is identified
/// by its root entry's MAL id"). It is supplied by the builder, never
/// generated, and it moves when the root moves, carrying the series' members
/// and choices with it.</summary>
public class Series
{
    public int Id { get; set; }
    public DateTimeOffset BuiltAt { get; set; }
    public bool IsPartial { get; set; } // build hit its MAL-fetch budget
    public bool IsTruncated { get; set; } // build hit the 60-member cap

    // Survive a rebuild: the rebuild keeps this row and simply never writes
    // these fields.
    public string? SelectedTitle { get; set; }
    public string? SelectedPictureUrl { get; set; }

    public List<SeriesMember> Members { get; set; } = [];
}
