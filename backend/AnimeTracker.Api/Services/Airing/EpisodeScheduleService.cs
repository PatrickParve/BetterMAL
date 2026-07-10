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
