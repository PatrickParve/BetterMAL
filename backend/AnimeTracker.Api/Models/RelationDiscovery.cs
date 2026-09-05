namespace AnimeTracker.Api.Models;

/// <summary>One relation edge that newly appeared in an anime's stored
/// relation set at refresh time — the write side of the "updates" view
/// (Services/Updates). Written only for an edge absent from the anime's
/// pre-refresh snapshot, and only when both hold of the anime whose relation
/// set changed (design.md D4): it had already been fully fetched before this
/// write — a first full-detail fetch's whole relation set arriving at once is
/// the system finally looking, not links newly appearing, whether the anime
/// had no cached row before or only a lean listing one — and it is a
/// non-Dropped list entry of the user's own, since a discovery can only ever
/// feed an announcement one relation step from the list. RelatedAnimeId is
/// intentionally not an FK, mirroring AnimeRelatedAnime: MAL routinely relates
/// an anime we've never cached. Historical edges discovered before this
/// record existed are not backfilled. ProcessedAt marks a discovery as
/// considered for an announcement
/// (IAnnouncementResolutionService) — null means still pending; every
/// pre-existing row was backfilled as processed when this column was added
/// (design.md D7), so the feature does not open with a wall of backdated news.</summary>
public class RelationDiscovery
{
    public long Id { get; set; }
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public int RelatedAnimeId { get; set; }
    public string RelationType { get; set; } = "";
    public DateTimeOffset DiscoveredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
