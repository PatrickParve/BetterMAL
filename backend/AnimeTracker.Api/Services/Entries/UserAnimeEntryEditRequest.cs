namespace AnimeTracker.Api.Services.Entries;

using AnimeTracker.Api.Models;

/// <summary>Partial edit — only set fields are applied. Mirrors
/// MalListStatusUpdate's "only what changed" shape on the inbound side.</summary>
public class UserAnimeEntryEditRequest
{
    public WatchStatus? Status { get; set; }
    public int? EpisodesWatched { get; set; }
    public int? MyScore { get; set; }
    public int? RewatchCount { get; set; }
}
