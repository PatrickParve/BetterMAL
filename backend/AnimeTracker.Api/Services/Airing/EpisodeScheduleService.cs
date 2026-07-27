using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

public class EpisodeScheduleService(
    IEpisodeScheduleCache cache,
    IBroadcastLocalTimeConverter converter) : IEpisodeScheduleService
{
    // Fallback run length for an airing show with no reported episode count —
    // one standard cour, so an open-ended show still fills out a plausible
    // schedule instead of appearing for a single week.
    private const int MinimumEpisodeEstimate = 12;

    public ResolvedEpisode? ResolveOnLocalDate(AnimeMetadata anime, DateOnly localDate)
    {
        if (cache.TryGet(anime.Id, out var schedule) && schedule.Count > 0)
        {
            // Trust AniList only within the span it actually covers. Episodes
            // are ordered, so first/last bound the coverage window; a date
            // inside it with no matching episode is a genuine off/break week.
            var coverageStart = converter.GetLocalDate(schedule[0].AirsAtUtc);
            var coverageEnd = converter.GetLocalDate(schedule[^1].AirsAtUtc);
            if (localDate >= coverageStart && localDate <= coverageEnd)
            {
                foreach (var episode in schedule)
                {
                    if (converter.GetLocalDate(episode.AirsAtUtc) == localDate)
                        return new ResolvedEpisode(converter.GetLocalTime(episode.AirsAtUtc), episode.Episode);
                }

                return null; // within the run, but this week is a break
            }
            // Outside AniList's window (deep past / far future) → fall through
            // to the cadence estimate below.
        }

        return EstimateOnLocalDate(anime, localDate);
    }

    public DateTimeOffset? NextAiringInstant(AnimeMetadata anime, DateTimeOffset afterUtc)
    {
        if (cache.TryGet(anime.Id, out var schedule) && schedule.Count > 0)
        {
            foreach (var episode in schedule)
            {
                if (episode.AirsAtUtc > afterUtc)
                    return episode.AirsAtUtc;
            }
            // No future episode cached → fall back to the weekly estimate,
            // which extrapolates beyond AniList's last scheduled entry.
        }

        return converter.NextBroadcastInstant(anime, afterUtc);
    }

    public int? EpisodesAiredAsOf(AnimeMetadata anime, DateTimeOffset nowUtc)
    {
        // A finished run is over by definition — trust the total rather than
        // the estimate, which can drift for a long-finished show.
        if (anime.AiringStatus == "finished_airing" && anime.TotalEpisodes is { } finishedTotal)
            return finishedTotal;

        var aired = cache.TryGet(anime.Id, out var schedule) && schedule.Count > 0
            ? AiredFromCachedSchedule(schedule, nowUtc)
            : EstimateAiredFromCadence(anime, nowUtc);

        if (aired is not { } count)
            return null;

        return anime.TotalEpisodes is { } total ? Math.Clamp(count, 0, total) : count;
    }

    private static int AiredFromCachedSchedule(IReadOnlyList<EpisodeAiring> schedule, DateTimeOffset nowUtc)
    {
        var maxAired = 0;
        foreach (var episode in schedule)
        {
            if (episode.AirsAtUtc <= nowUtc && episode.Episode > maxAired)
                maxAired = episode.Episode;
        }
        return maxAired;
    }

    // Weekly-cadence estimate: episode 1 anchors to the premiere's local date,
    // then one episode every 7 days. Simpler than re-deriving each week's exact
    // local slot (which EstimateOnLocalDate does), and good enough for a count
    // — the only place day-of-week/DST wobble matters is today's own slot.
    private int? EstimateAiredFromCadence(AnimeMetadata anime, DateTimeOffset nowUtc)
    {
        if (anime.AiredFrom is not { } airedFrom || anime.BroadcastTime is not { } broadcastTime)
            return null;

        var firstLocalDate = converter.LocalDateOfJstBroadcast(airedFrom, broadcastTime);
        var todayLocalDate = converter.GetLocalDate(nowUtc);

        // Bound the reference date at the show's last air date so a finished
        // show with no published total stops accruing a phantom episode every
        // week forever. EstimateLastLocalDate (EstimateOnLocalDate's sibling)
        // is deliberately not reused here — its MinimumEpisodeEstimate
        // fallback would fabricate a 12-episode ceiling when AiredTo and the
        // total are both missing. Bound by AiredTo only; when AiredTo is
        // absent but MAL says the run is over, FinishedAiring (decision 3)
        // carries that meaning instead.
        var nowLocalDate = todayLocalDate;
        var wasClampedToPast = false;
        if (anime.AiredTo is { } airedTo)
        {
            var lastAirLocalDate = converter.LocalDateOfJstBroadcast(airedTo, broadcastTime);
            if (lastAirLocalDate < todayLocalDate)
            {
                nowLocalDate = lastAirLocalDate;
                wasClampedToPast = true;
            }
        }

        var daysSincePremiere = nowLocalDate.DayNumber - firstLocalDate.DayNumber;
        if (daysSincePremiere < 0)
            return 0; // before the premiere

        var count = daysSincePremiere / 7 + 1;

        // If today is this week's broadcast day, the tentatively-counted
        // episode has only aired once its local broadcast time has passed.
        // Only applies when the reference date is genuinely today — once
        // clamped to a past AiredTo date, comparing against the current
        // wall-clock time would incorrectly deduct an episode that aired
        // long ago.
        if (!wasClampedToPast && daysSincePremiere % 7 == 0)
        {
            var todaySlot = converter.ResolveForDate(anime.BroadcastDayOfWeek, broadcastTime, nowLocalDate);
            if (todaySlot is not null && converter.GetLocalTime(nowUtc) < todaySlot.Time)
                count -= 1;
        }

        return count;
    }

    // Weekly-cadence fallback: place episodes one broadcast slot apart starting
    // from the show's local first-air date, bounded to its real air window.
    private ResolvedEpisode? EstimateOnLocalDate(AnimeMetadata anime, DateOnly localDate)
    {
        var slot = converter.ResolveForDate(anime.BroadcastDayOfWeek, anime.BroadcastTime, localDate);
        if (slot is null)
            return null;

        // No start date or broadcast time means we can't place episodes on a
        // timeline; surface only a currently-airing show, with no episode number.
        if (anime.AiredFrom is not { } airedFrom || anime.BroadcastTime is not { } broadcastTime)
            return anime.AiringStatus == "currently_airing" ? new ResolvedEpisode(slot.Time, null) : null;

        var firstLocalDate = converter.LocalDateOfJstBroadcast(airedFrom, broadcastTime);

        // Round rather than integer-divide: a DST shift can move a late-night
        // slot's local weekday by a day, so the gap isn't always an exact 7.
        var weeksSinceStart = (int)Math.Round((localDate.DayNumber - firstLocalDate.DayNumber) / 7.0);
        if (weeksSinceStart < 0)
            return null; // before the premiere

        var lastLocalDate = EstimateLastLocalDate(anime, broadcastTime, firstLocalDate);
        if (lastLocalDate is { } end && localDate > end)
            return null; // after the finale

        var episode = weeksSinceStart + 1;
        var episodeNumber = anime.TotalEpisodes is { } total ? Math.Min(episode, total) : episode;
        return new ResolvedEpisode(slot.Time, episodeNumber);
    }

    private DateOnly? EstimateLastLocalDate(AnimeMetadata anime, TimeOnly broadcastTime, DateOnly firstLocalDate)
    {
        if (anime.AiredTo is { } airedTo)
            return converter.LocalDateOfJstBroadcast(airedTo, broadcastTime);
        if (anime.TotalEpisodes is { } total)
            return firstLocalDate.AddDays((total - 1) * 7);

        // No episode count and open-ended. Rather than project weekly episodes
        // forever, assume a minimum cour; then, only while the show is still
        // airing with a known next episode, extend just far enough to include
        // the next upcoming broadcast so it doesn't drop off the schedule.
        // (Concrete future dates from AniList are handled by the cache path, so
        // a show that really has many scheduled episodes still shows them all.)
        var minimumCourEnd = firstLocalDate.AddDays((MinimumEpisodeEstimate - 1) * 7);
        if (anime.AiringStatus != "currently_airing" || !HasKnownUpcomingEpisode(anime))
            return minimumCourEnd;

        if (converter.NextBroadcastInstant(anime, DateTimeOffset.UtcNow) is not { } nextInstant)
            return minimumCourEnd;

        var nextUpcomingDate = converter.GetLocalDate(nextInstant);
        return nextUpcomingDate > minimumCourEnd ? nextUpcomingDate : minimumCourEnd;
    }

    // Whether the show still has an episode scheduled ahead. AniList is
    // authoritative when present (its last known episode is in the future);
    // absent any AniList data we assume an "airing" show is still going.
    private bool HasKnownUpcomingEpisode(AnimeMetadata anime) =>
        !cache.TryGet(anime.Id, out var schedule) || schedule.Count == 0
            || schedule[^1].AirsAtUtc > DateTimeOffset.UtcNow;
}
