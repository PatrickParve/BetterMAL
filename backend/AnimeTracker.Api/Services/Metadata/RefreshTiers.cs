using System.Linq.Expressions;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Staleness tier ladder for full-detail refresh, keyed on
/// <see cref="AnimeMetadata.LastSyncedAt"/> — the true "last full-detail
/// fetch" timestamp (only <c>ApplyTo</c> writes it; a never-fetched row
/// carries <c>default</c>). One tier ladder backs both the nightly batch's
/// DB-side "due" query and the on-demand detail-page TTL check, so the two
/// paths cannot drift apart. <see cref="IsDue"/> additionally measures from
/// the later of that timestamp and <see cref="AnimeMetadata.LastRefreshFailedAt"/>,
/// a not-found attempt the batch recorded; <see cref="TtlFor"/> (the detail
/// page) does not honour that mark and reads <c>LastSyncedAt</c> alone.</summary>
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
    /// with no recorded not-found attempt are always due. Every tier is
    /// measured from the later of <c>LastSyncedAt</c> and
    /// <c>LastRefreshFailedAt</c> — a row is at or before a cutoff on both
    /// exactly when the later of the two is. <see cref="TtlFor"/> (the detail
    /// page) reads <c>LastSyncedAt</c> alone and does not honour the
    /// mark.</summary>
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
            (a.LastSyncedAt == default && a.LastRefreshFailedAt == null) ||
            (a.AiringStatus == "currently_airing" && a.LastSyncedAt <= airingCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= airingCutoff)) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom != null &&
                a.AiredFrom <= premiereSoonCutoff && a.LastSyncedAt <= airingCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= airingCutoff)) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom != null &&
                a.AiredFrom > premiereSoonCutoff && a.LastSyncedAt <= unairedFarCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= unairedFarCutoff)) ||
            (a.AiringStatus == "not_yet_aired" && a.AiredFrom == null &&
                a.LastSyncedAt <= unairedUnknownCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= unairedUnknownCutoff)) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                a.AiredTo != null && a.AiredTo >= oneYearAgo &&
                a.LastSyncedAt <= recentCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= recentCutoff)) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                a.AiredTo != null && a.AiredTo < oneYearAgo && a.AiredTo >= twoYearsAgo &&
                a.LastSyncedAt <= agingCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= agingCutoff)) ||
            (a.AiringStatus != "currently_airing" && a.AiringStatus != "not_yet_aired" &&
                (a.AiredTo == null || a.AiredTo < twoYearsAgo) &&
                a.LastSyncedAt <= staleCutoff &&
                (a.LastRefreshFailedAt == null || a.LastRefreshFailedAt <= staleCutoff));
    }

    /// <summary>The candidates' ordering key: the later of the anime's last
    /// full-detail fetch and its last recorded not-found attempt. A null mark
    /// compares false against any DateTimeOffset comparison, so such a row
    /// falls through to <c>LastSyncedAt</c> alone, same as before this
    /// column existed. Npgsql translates the conditional to a SQL
    /// <c>CASE</c>.</summary>
    public static readonly Expression<Func<AnimeMetadata, DateTimeOffset>> LastAttemptAt =
        a => a.LastRefreshFailedAt > a.LastSyncedAt ? a.LastRefreshFailedAt.Value : a.LastSyncedAt;
}
