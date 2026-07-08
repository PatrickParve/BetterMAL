namespace AnimeTracker.Api.Models;

/// <summary>A manually-chosen anime occupying one of the remaining "My top
/// anime" slots (the slots left after all score-10 anime are shown),
/// overriding the default next-highest-score auto-fill. Presence of any rows
/// here means the profile page uses the manual selection instead of the
/// default fill.</summary>
public class TopAnimeSelection
{
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public DateTimeOffset SelectedAt { get; set; }
}
