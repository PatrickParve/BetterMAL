namespace AnimeTracker.Api.Services.Entries;

using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;

/// <summary>Partial edit — only set fields are applied. Mirrors
/// MalListStatusUpdate's "only what changed" shape on the inbound side.
///
/// StartedAt/CompletedAt need to distinguish a third state a plain nullable
/// can't express: "absent" (field not being edited) from "present as null"
/// (clear the date) — unlike every other field here, where null already means
/// "not being edited". The HasStartedAt/HasCompletedAt flags are flipped by
/// the property setters below, and System.Text.Json only invokes a property's
/// setter for keys actually present in the request body, so a JSON body that
/// omits the key entirely leaves the flag false.</summary>
public class UserAnimeEntryEditRequest
{
    public WatchStatus? Status { get; set; }
    public int? EpisodesWatched { get; set; }
    public int? MyScore { get; set; }
    public int? RewatchCount { get; set; }

    /// <summary>Only consulted when this edit returns a Rewatching entry to
    /// Completed by choosing Completed explicitly (not by reaching the total
    /// episode count) — see design.md D2. Null/false leaves the rewatch count
    /// unchanged; true increases it by one.</summary>
    public bool? CountsAsRewatch { get; set; }

    private DateOnly? _startedAt;
    public DateOnly? StartedAt
    {
        get => _startedAt;
        set { _startedAt = value; HasStartedAt = true; }
    }

    [JsonIgnore]
    public bool HasStartedAt { get; private set; }

    private DateOnly? _completedAt;
    public DateOnly? CompletedAt
    {
        get => _completedAt;
        set { _completedAt = value; HasCompletedAt = true; }
    }

    [JsonIgnore]
    public bool HasCompletedAt { get; private set; }
}
