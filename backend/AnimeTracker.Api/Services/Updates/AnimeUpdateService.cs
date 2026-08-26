using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Updates;

public class AnimeUpdateService(
    AnimeTrackerDbContext db,
    IRelationResolver relationResolver,
    IBroadcastLocalTimeConverter broadcastConverter) : IAnimeUpdateService
{
    // A fixed tie-break order (task 6.3) for when an anime qualifies through
    // more than one affiliate: sequel/prequel is the most specific news an
    // affiliation can carry. Eligibility itself is no longer restricted to
    // SeriesRelations.TraversalSet (widened per user request: any relation
    // MAL reports, not just "same story", counts here) — the precedence list
    // below still favors the story-relation types first, since they're the
    // most specific kind of news, with the looser universe/cast/media links
    // appended after them.
    private static readonly string[] RelationPrecedence =
    [
        "sequel", "prequel", "side_story", "parent_story", "summary", "full_story", "spin_off", "alternative_version",
        "adaptation", "alternative_setting", "other", "character",
    ];

    private static readonly Dictionary<WatchStatus, string> StatusLabels = new()
    {
        [WatchStatus.Watching] = "Watching",
        [WatchStatus.OnHold] = "On hold",
        [WatchStatus.PlanToWatch] = "Plan to watch",
        [WatchStatus.Completed] = "Completed",
        [WatchStatus.Dropped] = "Dropped",
        [WatchStatus.Rewatching] = "Rewatching",
    };

    public async Task<List<AnimeUpdateDto>> GetRecentAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        var updates = await Query()
            .Where(u => u.DetectedAt >= since)
            .ToListAsync(ct);

        return await BuildEligibleAsync(updates, ct);
    }

    public async Task<List<AnimeUpdateDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var updates = await Query().ToListAsync(ct);
        return await BuildEligibleAsync(updates, ct);
    }

    private IQueryable<AnimeUpdate> Query() =>
        db.AnimeUpdates.AsNoTracking()
            .Include(u => u.Anime).ThenInclude(a => a.RelatedAnime)
            .Include(u => u.Anime).ThenInclude(a => a.UserEntry)
            .OrderByDescending(u => u.DetectedAt)
            .ThenByDescending(u => u.Id);

    private async Task<List<AnimeUpdateDto>> BuildEligibleAsync(List<AnimeUpdate> updates, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var reasonByAnimeId = new Dictionary<int, string?>();
        var result = new List<AnimeUpdateDto>();

        foreach (var update in updates)
        {
            if (!reasonByAnimeId.TryGetValue(update.AnimeId, out var reason))
            {
                reason = await ResolveReasonAsync(update.Anime, ct);
                reasonByAnimeId[update.AnimeId] = reason;
            }

            if (reason is null)
                continue; // ineligible: neither a non-Dropped entry of its own nor a qualifying affiliation

            result.Add(ToDto(update, reason, now));
        }

        return result;
    }

    /// <summary>Eligibility plus the reason to display, computed together
    /// since both need the same affiliation lookup (design.md D4/D5, task
    /// 6.2/6.3). Returns null when ineligible.</summary>
    private async Task<string?> ResolveReasonAsync(AnimeMetadata anime, CancellationToken ct)
    {
        var ownEntry = anime.UserEntry;
        // Short-circuits eligibility itself (task 6.2) — a non-Dropped own
        // entry alone already proves the anime qualifies, before the
        // resolver below is ever awaited. The affiliation lookup still runs
        // regardless, because an affiliation is always the preferred reason
        // once one exists, even for my own entry (spec: "the affiliation
        // SHALL be named").
        var ownEligible = ownEntry is { Status: not WatchStatus.Dropped };

        var affiliateReason = await TryDeriveAffiliateReasonAsync(anime, ct);
        if (affiliateReason is not null)
            return affiliateReason;

        return ownEligible ? OwnStatusReason(ownEntry!.Status) : null;
    }

    private async Task<string?> TryDeriveAffiliateReasonAsync(AnimeMetadata anime, CancellationToken ct)
    {
        // Every relation edge counts here, not just SeriesRelations.TraversalSet
        // (widened per user request) — an anime related to my list any way MAL
        // reports (alternative setting, shared character, a promo/other link)
        // is still franchise-adjacent news, not just a same-story continuation.
        // Confidence != Contradicted is the one guard kept: AniList actively
        // disputing an edge is evidence it isn't real, not a matter of how
        // narrowly "related" is defined.
        var edges = await relationResolver.GetEdgesAsync(anime, ct);
        var candidates = edges.Where(e => e.Confidence != RelationConfidence.Contradicted).ToList();
        if (candidates.Count == 0)
            return null;

        var farEndIds = candidates.Select(e => e.AnimeId).Distinct().ToList();
        var nonDroppedFarEndIds = await db.UserAnimeEntries.AsNoTracking()
            .Where(ue => farEndIds.Contains(ue.AnimeId) && ue.Status != WatchStatus.Dropped)
            .Select(ue => ue.AnimeId)
            .ToListAsync(ct);
        if (nonDroppedFarEndIds.Count == 0)
            return null;

        var nonDroppedFarEndSet = nonDroppedFarEndIds.ToHashSet();
        var qualifying = candidates.Where(e => nonDroppedFarEndSet.Contains(e.AnimeId)).ToList();
        if (qualifying.Count == 0)
            return null;

        var best = qualifying
            .OrderBy(e => RelationPrecedenceOf(e.RelationType))
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .First();

        var relationFromViewedSide = RelationInverse.Invert(best.RelationType) ?? best.RelationType;
        return $"{HumanizeRelation(relationFromViewedSide)} {best.Title}";
    }

    private static int RelationPrecedenceOf(string relationType)
    {
        var index = Array.IndexOf(RelationPrecedence, relationType);
        return index >= 0 ? index : int.MaxValue;
    }

    private static string HumanizeRelation(string relationType) => relationType switch
    {
        "sequel" => "Sequel to",
        "prequel" => "Prequel to",
        "side_story" => "Side story of",
        "parent_story" => "Parent story of",
        "summary" => "Summary of",
        "full_story" => "Full story of",
        "spin_off" => "Spin-off of",
        "alternative_version" => "Alternative version of",
        "alternative_setting" => "Alternative setting to",
        "adaptation" => "Adaptation of",
        "character" => "Shares characters with",
        "other" => "Related to",
        _ => "Related to",
    };

    private static string OwnStatusReason(WatchStatus status) => StatusLabels.GetValueOrDefault(status, status.ToString());

    private AnimeUpdateDto ToDto(AnimeUpdate update, string reason, DateTimeOffset now)
    {
        var anime = update.Anime;

        DayOfWeek? currentDay = null;
        TimeOnly? currentTime = null;
        DayOfWeek? previousDay = null;
        TimeOnly? previousTime = null;

        // Only relevant to a broadcast-slot-changed card (task 6.4): both
        // ends of the move, converted through the same local-time path as
        // every other broadcast time in the app.
        if ((update.Kinds & AnimeUpdateKinds.BroadcastSlotChanged) != 0)
        {
            if (MalMappingExtensions.ParseMalDayOfWeek(anime.BroadcastDayOfWeek) is { } jstDay && anime.BroadcastTime is { } jstTime)
                (currentDay, currentTime) = broadcastConverter.ConvertBroadcastSlot(jstDay, jstTime, now);

            if (MalMappingExtensions.ParseMalDayOfWeek(update.PreviousBroadcastDayOfWeek) is { } prevJstDay && update.PreviousBroadcastTime is { } prevJstTime)
                (previousDay, previousTime) = broadcastConverter.ConvertBroadcastSlot(prevJstDay, prevJstTime, now);
        }

        return new AnimeUpdateDto(
            update.Id,
            anime.Id,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            DecomposeKinds(update.Kinds),
            update.DetectedAt,
            anime.TotalEpisodes,
            anime.AiredFrom,
            update.PreviousStartDate,
            previousDay,
            previousTime,
            currentDay,
            currentTime,
            update.MovedEpisode,
            update.PreviousEpisodeDate,
            update.NewEpisodeDate,
            reason);
    }

    private static IReadOnlyList<string> DecomposeKinds(AnimeUpdateKinds kinds)
    {
        var result = new List<string>();
        foreach (AnimeUpdateKinds kind in Enum.GetValues<AnimeUpdateKinds>())
        {
            if ((kinds & kind) != 0)
                result.Add(kind.ToString());
        }
        return result;
    }
}
