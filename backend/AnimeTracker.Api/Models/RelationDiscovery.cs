namespace AnimeTracker.Api.Models;

/// <summary>One relation edge that newly appeared in an anime's stored
/// relation set at refresh time — the write side of a future "updates" view
/// (this change writes these; nothing reads them yet). Written only for an
/// edge absent from the anime's pre-refresh snapshot, and only when that
/// anime already had a cached row: an initial import's whole relation set is
/// not news, and neither is a routine refresh of unchanged data.
/// RelatedAnimeId is intentionally not an FK, mirroring AnimeRelatedAnime:
/// MAL routinely relates an anime we've never cached. Historical edges
/// discovered before this record existed are not backfilled.</summary>
public class RelationDiscovery
{
    public long Id { get; set; }
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public int RelatedAnimeId { get; set; }
    public string RelationType { get; set; } = "";
    public DateTimeOffset DiscoveredAt { get; set; }
}
