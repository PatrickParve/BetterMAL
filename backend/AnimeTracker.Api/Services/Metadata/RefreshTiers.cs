using System.Linq.Expressions;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Staleness tier ladder for full-detail refresh, keyed on
/// <see cref="AnimeMetadata.LastSyncedAt"/> — the true "last full-detail
/// fetch" timestamp (only <c>ApplyTo</c> writes it; a never-fetched row
/// carries <c>default</c>). One tier ladder backs both the nightly batch's
/// DB-side "due" query and the on-demand detail-page TTL check, so the two
/// paths cannot drift apart.</summary>
public static class RefreshTiers
{
    public static readonly TimeSpan AiringTtl = TimeSpan.FromDays(1);
    public static readonly TimeSpan RecentTtl = TimeSpan.FromDays(3);
    public static readonly TimeSpan AgingTtl = TimeSpan.FromDays(14);
    public static readonly TimeSpan StaleTtl = TimeSpan.FromDays(28);

    /// <summary>In-memory TTL check for a single already-loaded anime.</summary>
    public static TimeSpan TtlFor(AnimeMetadata anime)
    {
        if (anime.AiringStatus is "currently_airing" or "not_yet_aired")
            return AiringTtl;

        if (anime.AiredTo is not { } airedTo)
            return StaleTtl;

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        if (airedTo >= today.AddYears(-1))
            return RecentTtl;
        if (airedTo >= today.AddYears(-2))
            return AgingTtl;
        return StaleTtl;
    }

    /// <summary>The same ladder expressed for EF Core translation, so
    /// <c>RefreshStaleBatchAsync</c>'s "due" query and <see cref="TtlFor"/>
    /// cannot disagree. Never-fetched anime (<c>LastSyncedAt == default</c>)
    /// are always due.</summary>
    public static Expression<Func<AnimeMetadata, bool>> IsDue(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var oneYearAgo = today.AddYears(-1);
        var twoYearsAgo = today.AddYears(-2);
        var airingCutoff = now - AiringTtl;
        var recentCutoff = now - RecentTtl;
        var agingCutoff = now - AgingTtl;
        var staleCutoff = now - StaleTtl;

        return a =>
            a.LastSyncedAt == default ||
            ((a.AiringStatus == "currently_airing" || a.AiringStatus == "not_yet_aired") &&
                a.LastSyncedAt <= airingCutoff) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                a.AiredTo != null && a.AiredTo >= oneYearAgo &&
                a.LastSyncedAt <= recentCutoff) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                a.AiredTo != null && a.AiredTo < oneYearAgo && a.AiredTo >= twoYearsAgo &&
                a.LastSyncedAt <= agingCutoff) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                (a.AiredTo == null || a.AiredTo < twoYearsAgo) &&
                a.LastSyncedAt <= staleCutoff);
    }
}
