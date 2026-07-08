namespace AnimeTracker.Api.Models;

/// <summary>Membership row recording that an anime appeared in a season's MAL
/// listing — the season browser's only source of "which anime belong to this
/// season" (nothing on AnimeMetadata itself records season/year).</summary>
public class SeasonAnimeListing
{
    public int Year { get; set; }
    public required string Season { get; set; } // winter, spring, summer, fall
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
}
