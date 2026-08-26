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
    public static readonly TimeSpan UnairedFarTtl = TimeSpan.FromDays(7);
    public static readonly TimeSpan UnairedUnknownDateTtl = TimeSpan.FromDays(3);
    public static readonly TimeSpan RecentTtl = TimeSpan.FromDays(3);
    public static readonly TimeSpan AgingTtl = TimeSpan.FromDays(14);
    public static readonly TimeSpan StaleTtl = TimeSpan.FromDays(28);

    // How close a known premiere date has to be (or how far past, since a
    // premiere that's already come and gone while MAL still says
    // not_yet_aired is exactly the daily tier's job to notice) before an
    // unaired anime moves off the weekly tier and onto the daily one
    // (design.md D9).
    private const int PremiereSoonWindowDays = 30;

    /// <summary>In-memory TTL check for a single already-loaded anime.</summary>
    public static TimeSpan TtlFor(AnimeMetadata anime)
    {
        if (anime.AiringStatus == "currently_airing")
            return AiringTtl;

        if (anime.AiringStatus == "not_yet_aired")
        {
            if (anime.AiredFrom is not { } premiere)
                return UnairedUnknownDateTtl;

            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
            return premiere <= today.AddDays(PremiereSoonWindowDays) ? AiringTtl : UnairedFarTtl;
        }

        if (anime.AiredTo is not { } airedTo)
            return StaleTtl;

        var todayFinished = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        if (airedTo >= todayFinished.AddYears(-1))
            return RecentTtl;
        if (airedTo >= todayFinished.AddYears(-2))
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
        var premiereSoonCutoff = today.AddDays(PremiereSoonWindowDays);
        var oneYearAgo = today.AddYears(-1);
        var twoYearsAgo = today.AddYears(-2);
        var airingCutoff = now - AiringTtl;
        var unairedFarCutoff = now - UnairedFarTtl;
        var unairedUnknownCutoff = now - UnairedUnknownDateTtl;
        var recentCutoff = now - RecentTtl;
        var agingCutoff = now - AgingTtl;
        var staleCutoff = now - StaleTtl;

        return a =>
            a.LastSyncedAt == default ||
            (a.AiringStatus == "currently_airing" && a.LastSyncedAt <= airingCutoff) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom != null &&
                a.AiredFrom <= premiereSoonCutoff && a.LastSyncedAt <= airingCutoff) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom != null &&
                a.AiredFrom > premiereSoonCutoff && a.LastSyncedAt <= unairedFarCutoff) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom == null &&
                a.LastSyncedAt <= unairedUnknownCutoff) ||
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
