using System.Globalization;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Services.Mal;

/// <summary>Maps MAL's wire vocabulary (snake_case status strings, partial
/// "yyyy"/"yyyy-MM"/"yyyy-MM-dd" dates, "HH:mm" broadcast times) to domain
/// types. Kept separate from MalClient/MalAnimeNode, which deliberately stay
/// close to the raw wire format.</summary>
public static class MalMappingExtensions
{
    public static WatchStatus ToWatchStatus(this string malStatus) => malStatus switch
    {
        "watching" => WatchStatus.Watching,
        "completed" => WatchStatus.Completed,
        "on_hold" => WatchStatus.OnHold,
        "dropped" => WatchStatus.Dropped,
        "plan_to_watch" => WatchStatus.PlanToWatch,
        _ => throw new ArgumentOutOfRangeException(nameof(malStatus), malStatus, "Unrecognized MAL list status."),
    };

    public static string ToMalStatusString(this WatchStatus status) => status switch
    {
        WatchStatus.Watching => "watching",
        WatchStatus.Completed => "completed",
        WatchStatus.OnHold => "on_hold",
        WatchStatus.Dropped => "dropped",
        WatchStatus.PlanToWatch => "plan_to_watch",
        // mal-write-sync: MAL has no rewatching status, so a rewatch is
        // pushed as a normal in-progress watch (design.md D4) — reconciliation
        // is taught not to read that back as a demotion (see ReconciliationService).
        WatchStatus.Rewatching => "watching",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>MAL returns partial dates ("2024", "2024-01", "2024-01-15") for
    /// aired-from/to and full dates for list start/finish dates. Unparseable or
    /// empty input maps to null rather than throwing.</summary>
    public static DateOnly? ParseMalDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var full))
            return full;
        if (DateOnly.TryParseExact(raw, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearMonth))
            return yearMonth;
        if (DateOnly.TryParseExact(raw, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var year))
            return year;
        return null;
    }

    public static TimeOnly? ParseMalTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
    }

    /// <summary>MAL broadcast day-of-week. The official MAL v2 API returns the
    /// singular lowercase form ("monday"); the unofficial Jikan API returns the
    /// plural ("Mondays"). TrimEnd('s') normalizes both so either source works.
    /// Unrecognized or empty input maps to null rather than throwing.</summary>
    public static DayOfWeek? ParseMalDayOfWeek(string? raw) => raw?.Trim().ToLowerInvariant().TrimEnd('s') switch
    {
        "sunday" => DayOfWeek.Sunday,
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        _ => null,
    };

    /// <summary>Builds a new cached metadata row from a MAL anime node.</summary>
    public static AnimeMetadata ToAnimeMetadata(this MalAnimeNode node, DateTimeOffset now)
    {
        var metadata = new AnimeMetadata { Id = node.Id, Title = node.Title };
        node.ApplyTo(metadata, now);
        return metadata;
    }

    /// <summary>Copies a MAL anime node's fields onto an existing cached row —
    /// the update-in-place counterpart of <see cref="ToAnimeMetadata"/>, so
    /// on-demand refresh, reconciliation, and import all share one mapping.
    /// Full/rich upsert: also overwrites detail-page fields (genres, synopsis,
    /// background, related anime), so only call this from a full-detail fetch
    /// — never from a lean listing refresh (see <see cref="ApplyLeanTo"/>).</summary>
    public static void ApplyTo(this MalAnimeNode node, AnimeMetadata target, DateTimeOffset now)
    {
        target.Title = node.Title;
        target.EnglishTitle = string.IsNullOrWhiteSpace(node.AlternativeTitles?.En) ? null : node.AlternativeTitles.En;

        // The single guard for the whole app (design.md D2): read whether the
        // row is overridden *before* writing either picture column. A second
        // writer of PictureUrl outside this guard would defeat it.
        var wasOverridden = target.PictureUrl != target.MalPictureUrl;
        target.MalPictureUrl = node.MainPicture?.Large ?? node.MainPicture?.Medium;
        if (!wasOverridden) target.PictureUrl = target.MalPictureUrl;

        node.ApplyPictureSetTo(target, now);

        target.MalScore = node.Mean;
        target.MediaType = node.MediaType;
        target.AiringStatus = node.Status;
        target.Rating = node.Rating;
        target.MalTotalEpisodes = node.NumEpisodes is null or 0 ? null : node.NumEpisodes;
        target.ResolveTotalEpisodes();
        target.AiredFrom = ParseMalDate(node.StartDate);
        target.AiredTo = ParseMalDate(node.EndDate);
        target.Studio = node.Studios?.FirstOrDefault()?.Name;
        target.BroadcastDayOfWeek = node.Broadcast?.DayOfTheWeek;
        target.BroadcastTime = ParseMalTime(node.Broadcast?.StartTime);
        target.PopularityRank = node.Popularity;
        target.Rank = node.Rank;
        target.Genres = node.Genres?.Select(g => g.Name).ToList();
        target.Synopsis = node.Synopsis;
        target.Background = node.Background;
        target.AverageEpisodeDurationSeconds = node.AverageEpisodeDuration;
        target.Source = node.Source;

        target.RelatedAnime = node.RelatedAnime?
            .Where(edge => edge.Node is not null)
            .Select((edge, index) => new AnimeRelatedAnime
            {
                AnimeId = target.Id,
                RelatedAnimeId = edge.Node.Id,
                RelationType = edge.RelationType ?? "",
                Title = edge.Node.Title,
                PictureUrl = edge.Node.MainPicture?.Large ?? edge.Node.MainPicture?.Medium,
                MediaType = edge.Node.MediaType,
                SortOrder = index,
            })
            .ToList() ?? [];

        target.LastSyncedAt = now;
        target.LastScoreSyncedAt = now;
    }

    /// <summary>Writes the picture set only when <paramref name="node"/> asked
    /// for `pictures` — an omitted field must not clear a stored set (spec
    /// `mal-api-integration` "An omitted field does not clear a stored set").
    /// Shared by <see cref="ApplyTo"/> and the single-anime picture backfill
    /// (Services/Artwork/PictureRefreshService), which fetches with only this
    /// field and must not touch anything else on the row.</summary>
    public static void ApplyPictureSetTo(this MalAnimeNode node, AnimeMetadata target, DateTimeOffset now)
    {
        if (node.Pictures is null) return;

        target.PictureUrls = node.Pictures
            .Select(p => p.Large ?? p.Medium)
            .Where(url => url is not null)
            .Select(url => url!)
            .ToList();
        target.PicturesSyncedAt = now;
    }

    /// <summary>Builds a new cached metadata row from a lean listing node
    /// (Season/Top-Anime browsing).</summary>
    public static AnimeMetadata ToLeanAnimeMetadata(this MalAnimeNode node, DateTimeOffset now)
    {
        var metadata = new AnimeMetadata { Id = node.Id, Title = node.Title };
        node.ApplyLeanTo(metadata, now);
        return metadata;
    }

    /// <summary>Lean upsert: writes only listing-page fields (title, picture,
    /// episode count, type, score, rank/popularity) and never touches rich
    /// detail-page fields (airing status/dates, studio, broadcast, genres,
    /// synopsis, background, related anime) or <see cref="AnimeMetadata.LastSyncedAt"/> —
    /// so a Season/Top-Anime browse can never clobber richer data a full
    /// detail fetch already populated on the same row. `Rating` is written here
    /// too — it's in both the lean and full field sets, so this always has a
    /// real value rather than clobbering one with null.</summary>
    public static void ApplyLeanTo(this MalAnimeNode node, AnimeMetadata target, DateTimeOffset now)
    {
        target.Title = node.Title;
        target.EnglishTitle = string.IsNullOrWhiteSpace(node.AlternativeTitles?.En) ? null : node.AlternativeTitles.En;

        // Same override guard as ApplyTo (design.md D2) — a Season/Year/Top/Search
        // listing refresh must not revert a chosen picture either. Lean nodes
        // never carry `pictures`, so PictureUrls/PicturesSyncedAt are untouched here.
        var wasOverridden = target.PictureUrl != target.MalPictureUrl;
        target.MalPictureUrl = node.MainPicture?.Large ?? node.MainPicture?.Medium;
        if (!wasOverridden) target.PictureUrl = target.MalPictureUrl;

        target.MalScore = node.Mean;
        target.MediaType = node.MediaType;
        target.Rating = node.Rating;
        target.MalTotalEpisodes = node.NumEpisodes is null or 0 ? null : node.NumEpisodes;
        target.ResolveTotalEpisodes();
        target.PopularityRank = node.Popularity;
        target.Rank = node.Rank;
        target.LastScoreSyncedAt = now;
    }

    /// <summary>Builds a user-list entry from MAL's list-status shape (e.g. from
    /// import or reconciliation) — always PendingSync=false since the data came
    /// from MAL and there is nothing to push back.</summary>
    public static UserAnimeEntry ToUserAnimeEntry(int animeId, MalListStatus? status, DateTimeOffset now)
    {
        var entry = new UserAnimeEntry { AnimeId = animeId, PendingSync = false };
        status.ApplyTo(entry, now);
        return entry;
    }

    /// <summary>Copies MAL list-status fields onto an existing user-list entry —
    /// the update-in-place counterpart of <see cref="ToUserAnimeEntry"/>, so a
    /// corrective re-sync can refresh an already-imported entry the same way
    /// import builds a new one. Does not touch PendingSync — callers guard that.
    /// Resolves the incoming status against the entry's current one
    /// (design.md D1) so a local Rewatching entry survives; inert when called
    /// from <see cref="ToUserAnimeEntry"/>, whose fresh entry defaults to
    /// Watching rather than Rewatching.</summary>
    public static void ApplyTo(this MalListStatus? status, UserAnimeEntry target, DateTimeOffset now)
    {
        var remoteStatus = status?.Status?.ToWatchStatus() ?? WatchStatus.PlanToWatch;
        target.Status = MalStatusResolution.ResolveAgainstLocal(target.Status, remoteStatus);
        target.EpisodesWatched = status?.NumEpisodesWatched ?? 0;
        target.MyScore = status?.Score is null or 0 ? null : status.Score;
        target.StartedAt = ParseMalDate(status?.StartDate);
        target.CompletedAt = ParseMalDate(status?.FinishDate);
        target.RewatchCount = status?.NumTimesRewatched ?? 0;
        target.LastSyncedAt = now;
    }
}
