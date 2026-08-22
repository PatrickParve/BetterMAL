using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Profile;

/// <summary>A collapsed run of consecutive episode-watched entries for one
/// anime, used to render "Episodes a-b" instead of one row per episode.</summary>
public readonly record struct EpisodeRun(int From, int To);

/// <summary>A ScoreChanged row absorbed into an adjacent Completed row. Keyed
/// by the Completed row's Id wherever it's looked up (see
/// <see cref="ActivityFeedComposer.FindCompletionScoreMerges"/>); ScoreLogId
/// is the row to drop from the surface's own output, Score is the parsed
/// value to render on the completion's row (null when the merged row read
/// "Score cleared").</summary>
public readonly record struct CompletionScoreMerge(long ScoreLogId, int? Score);

public enum ActivityFieldGroup
{
    Progress,
    Score,
    RewatchCount,
    Membership,
}

/// <summary>Turns ActivityLog rows into the single display phrase shared by
/// "Latest updates" and the full history, and owns the grouping/merging rules
/// both surfaces apply. Stateless: every ChangeDetail format ever written by
/// any version of the app must parse here, with a fallback to the raw detail
/// (or a humanized change-type name) for anything that doesn't match, since
/// the log is append-only and never migrated.</summary>
public static class ActivityFeedComposer
{
    private static readonly TimeSpan CompletionScoreMergeWindow = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<WatchStatus, string> StatusLabels = new()
    {
        [WatchStatus.Watching] = "Watching",
        [WatchStatus.OnHold] = "On hold",
        [WatchStatus.PlanToWatch] = "Plan to watch",
        [WatchStatus.Completed] = "Completed",
        [WatchStatus.Dropped] = "Dropped",
        [WatchStatus.Rewatching] = "Rewatching",
    };

    public static ActivityFieldGroup? FieldGroupOf(ActivityChangeType changeType) => changeType switch
    {
        ActivityChangeType.EpisodeIncremented or ActivityChangeType.Completed => ActivityFieldGroup.Progress,
        ActivityChangeType.ScoreChanged => ActivityFieldGroup.Score,
        ActivityChangeType.RewatchCountChanged => ActivityFieldGroup.RewatchCount,
        ActivityChangeType.Added or ActivityChangeType.Removed => ActivityFieldGroup.Membership,
        _ => null,
    };

    public static string Summarize(ActivityLog log, int? mergedScore = null, EpisodeRun? episodeRun = null) => log.ChangeType switch
    {
        ActivityChangeType.Added => ComposeAdded(log),
        ActivityChangeType.EpisodeIncremented => ComposeEpisodeIncrement(log, episodeRun),
        ActivityChangeType.Completed => mergedScore is { } score ? $"Completed — Score {score}" : "Completed",
        ActivityChangeType.StatusChanged => ComposeStatusChange(log),
        ActivityChangeType.Removed => "Removed from list",
        _ => Fallback(log), // ScoreChanged, RewatchCountChanged, Start/FinishDateChanged already write display-ready detail
    };

    // logs must be most-recent-first (as the repository returns them).
    // Adjacency is tracked per anime via the last row seen for it, so a merge
    // only fires when no other change for that anime falls between the two —
    // exactly "adjacent in that anime's own event stream".
    public static Dictionary<long, CompletionScoreMerge> FindCompletionScoreMerges(IReadOnlyList<ActivityLog> logs)
    {
        var merges = new Dictionary<long, CompletionScoreMerge>();
        var lastByAnime = new Dictionary<int, ActivityLog>();

        foreach (var log in logs)
        {
            if (lastByAnime.TryGetValue(log.AnimeId, out var newer)
                && newer.ChangeType == ActivityChangeType.ScoreChanged
                && log.ChangeType == ActivityChangeType.Completed
                && newer.Timestamp >= log.Timestamp
                && newer.Timestamp - log.Timestamp <= CompletionScoreMergeWindow)
            {
                merges[log.Id] = new CompletionScoreMerge(newer.Id, ParseScore(newer.ChangeDetail));
            }

            lastByAnime[log.AnimeId] = log;
        }

        return merges;
    }

    private static string ComposeAdded(ActivityLog log)
    {
        const string prefix = "Added as ";
        if (log.ChangeDetail is { } detail && detail.StartsWith(prefix, StringComparison.Ordinal)
            && TryParseWatchStatus(detail[prefix.Length..], out var status))
        {
            return $"Added to list as {HumanizeStatus(status)}";
        }

        return Fallback(log);
    }

    private static string ComposeEpisodeIncrement(ActivityLog log, EpisodeRun? episodeRun)
    {
        if (episodeRun is { } run)
            return run.From == run.To ? $"Episode {run.To}" : $"Episodes {run.From}-{run.To}";

        return ParseEpisodeNumber(log.ChangeDetail) is { } n ? $"Episode {n}" : Fallback(log);
    }

    private static string ComposeStatusChange(ActivityLog log)
    {
        const string separator = " -> ";
        if (log.ChangeDetail is { } detail)
        {
            var separatorIndex = detail.IndexOf(separator, StringComparison.Ordinal);
            if (separatorIndex >= 0
                && TryParseWatchStatus(detail[..separatorIndex], out var from)
                && TryParseWatchStatus(detail[(separatorIndex + separator.Length)..], out var to))
            {
                return $"{HumanizeStatus(from)} → {HumanizeStatus(to)}";
            }
        }

        return Fallback(log);
    }

    private static string HumanizeStatus(WatchStatus status) => StatusLabels.GetValueOrDefault(status, status.ToString());

    private static bool TryParseWatchStatus(string text, out WatchStatus status) =>
        Enum.TryParse(text.Trim(), out status) && Enum.IsDefined(status);

    private static int? ParseEpisodeNumber(string? detail)
    {
        const string prefix = "Episode ";
        return detail is { } d && d.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(d[prefix.Length..], out var n)
            ? n
            : null;
    }

    private static int? ParseScore(string? detail)
    {
        const string prefix = "Score ";
        return detail is { } d && d.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(d[prefix.Length..], out var n)
            ? n
            : null;
    }

    // Falls back to the raw stored text, or, if there is none, a humanized
    // change-type name — what keeps rows written by any earlier version of
    // the app, or in a shape this composer doesn't recognize, rendering
    // sensibly instead of blank or wrong.
    private static string Fallback(ActivityLog log) => log.ChangeDetail ?? HumanizeChangeType(log.ChangeType);

    private static string HumanizeChangeType(ActivityChangeType changeType)
    {
        var name = changeType.ToString();
        var humanized = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c))
                humanized.Append(' ');
            humanized.Append(i == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
        }

        return humanized.ToString();
    }
}
