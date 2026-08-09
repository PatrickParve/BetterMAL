namespace AnimeTracker.Api.Models;

/// <summary>One related-anime edge reported by MAL for a given anime (e.g.
/// prequel, sequel, side story). <c>RelationType</c> is the raw MAL wire
/// value, kept unnormalized so an unrecognized relation is stored rather than
/// dropped. <c>RelatedAnimeId</c> is intentionally not an FK: MAL routinely
/// relates an anime we've never cached, and requiring the target row to exist
/// would fail the whole upsert.</summary>
public class AnimeRelatedAnime
{
    public int AnimeId { get; set; } // owner (MAL id)
    public int RelatedAnimeId { get; set; } // MAL id of the related anime
    public string RelationType { get; set; } = "";
    public string Title { get; set; } = "";
    public string? PictureUrl { get; set; }
    public int SortOrder { get; set; } // MAL's own ordering within the anime's related_anime array

    public AnimeMetadata Anime { get; set; } = null!;
}
