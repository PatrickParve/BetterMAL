using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>The exact ChangeDetail strings ActivityFeedComposer parses back
/// into display phrases, falling back to raw text silently for anything it
/// does not recognise. Producing these exact formats is a correctness
/// condition rather than a convenience (design D2) — every writer of
/// ActivityLog builds its ChangeDetail through here so a second writer can
/// never drift away from what the composer parses.</summary>
public static class ActivityDetail
{
    public static string Added(WatchStatus status) => $"Added as {status}";

    public static string Episode(int episodesWatched) => $"Episode {episodesWatched}";

    public const string Completed = "Completed";

    public static string StatusChange(WatchStatus from, WatchStatus to) => $"{from} -> {to}";

    public static string Score(int? score) => score is { } value ? $"Score {value}" : "Score cleared";

    public static string RewatchCount(int rewatchCount) => $"Rewatch count {rewatchCount}";

    public static string StartDate(DateOnly? date) => date is { } value ? $"Start date {value:yyyy-MM-dd}" : "Start date cleared";

    public static string FinishDate(DateOnly? date) => date is { } value ? $"Finish date {value:yyyy-MM-dd}" : "Finish date cleared";

    public const string Removed = "Removed from list";
}
