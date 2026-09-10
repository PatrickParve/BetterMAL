using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>The six user fields a before/after comparison is taken over.
/// A caller must capture this *before* mutating the entry — Diff takes it by
/// value, so a snapshot taken after ApplyTo/assignments have already run
/// compares an entry against itself.</summary>
public readonly record struct EntrySnapshot(
    WatchStatus Status,
    int EpisodesWatched,
    int? MyScore,
    DateOnly? StartedAt,
    DateOnly? CompletedAt,
    int RewatchCount)
{
    public static EntrySnapshot Of(UserAnimeEntry entry) => new(
        entry.Status, entry.EpisodesWatched, entry.MyScore, entry.StartedAt, entry.CompletedAt, entry.RewatchCount);
}

/// <summary>Builds the ActivityLog rows for applying MyAnimeList's current
/// value to an entry when I decline a held change (its only caller) — one
/// Added row for a new entry, or a field-by-field Diff for an existing one —
/// at the same granularity the local edit path produces (design D2, D8).</summary>
public static class EntryActivityRecorder
{
    public static ActivityLog Added(int animeId, WatchStatus status, DateTimeOffset now) => new()
    {
        AnimeId = animeId,
        Timestamp = now,
        ChangeType = ActivityChangeType.Added,
        ChangeDetail = ActivityDetail.Added(status),
    };

    /// <summary>One row per field that actually changed, in the order the
    /// local edit path emits them: episodes, then status, then dates, then
    /// score, then rewatch count. The score row is emitted last so it takes
    /// the higher identity, letting ActivityFeedComposer.FindCompletionScoreMerges
    /// fold a completion-plus-score application into one row (design D5).</summary>
    public static List<ActivityLog> Diff(int animeId, EntrySnapshot before, UserAnimeEntry after, DateTimeOffset now)
    {
        var rows = new List<ActivityLog>();

        if (after.EpisodesWatched != before.EpisodesWatched)
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.EpisodeIncremented,
                ChangeDetail = ActivityDetail.Episode(after.EpisodesWatched),
                PreviousEpisodesWatched = before.EpisodesWatched,
            });

        if (after.Status != before.Status)
        {
            // design D4: a status landing on Completed reads as a completion,
            // not a generic status change, so BuildActivityFeed keeps it —
            // it drops every other status change.
            var (changeType, detail) = after.Status == WatchStatus.Completed
                ? (ActivityChangeType.Completed, ActivityDetail.Completed)
                : (ActivityChangeType.StatusChanged, ActivityDetail.StatusChange(before.Status, after.Status));
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = changeType,
                ChangeDetail = detail,
            });
        }

        if (after.StartedAt != before.StartedAt)
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.StartDateChanged,
                ChangeDetail = ActivityDetail.StartDate(after.StartedAt),
            });

        if (after.CompletedAt != before.CompletedAt)
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.FinishDateChanged,
                ChangeDetail = ActivityDetail.FinishDate(after.CompletedAt),
            });

        if (after.MyScore != before.MyScore)
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.ScoreChanged,
                ChangeDetail = ActivityDetail.Score(after.MyScore),
            });

        if (after.RewatchCount != before.RewatchCount)
            rows.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.RewatchCountChanged,
                ChangeDetail = ActivityDetail.RewatchCount(after.RewatchCount),
            });

        return rows;
    }
}
