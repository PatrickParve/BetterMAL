using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

public class AiringScheduleService(
    IUserAnimeEntryRepository entryRepository,
    IBroadcastLocalTimeConverter broadcastConverter) : IAiringScheduleService
{
    public async Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default)
    {
        var referenceDate = weekReferenceDate ?? broadcastConverter.GetLocalDate(DateTimeOffset.UtcNow);
        var weekStart = broadcastConverter.GetStartOfWeek(referenceDate);
        var weekDates = Enumerable.Range(0, 7).Select(weekStart.AddDays).ToList();

        var entries = await entryRepository.GetAllAsync(ct);
        var slotsByDate = weekDates.ToDictionary(date => date, _ => new List<AiringSlotDto>());

        foreach (var entry in entries)
        {
            var slot = broadcastConverter.ResolveForWeek(entry.Anime, weekStart);
            if (slot is null)
                continue;

            var offset = ((int)slot.DayOfWeek - (int)weekStart.DayOfWeek + 7) % 7;
            var localDate = weekStart.AddDays(offset);

            slotsByDate[localDate].Add(new AiringSlotDto(
                entry.AnimeId,
                entry.Anime.Title,
                entry.Anime.EnglishTitle,
                entry.Anime.PictureUrl,
                slot.Time.ToString("HH:mm"),
                ComputeEpisodeNumber(entry.Anime, localDate)));
        }

        var days = weekDates
            .Select(date => new AiringDayDto(
                date,
                date.DayOfWeek,
                slotsByDate[date].OrderBy(s => s.LocalTime, StringComparer.Ordinal).ToList()))
            .ToList();

        return new AiringWeekDto(weekStart, weekStart.AddDays(6), days);
    }

    // Episode airing on localAirDate, assuming the show's weekly cadence
    // holds since it started — MAL doesn't provide a per-episode air date, so
    // this is the standard estimate: weeks elapsed since AiredFrom, +1,
    // capped at the known episode count.
    private static int? ComputeEpisodeNumber(AnimeMetadata anime, DateOnly localAirDate)
    {
        if (anime.AiredFrom is not { } airedFrom)
            return null;

        var daysSince = localAirDate.DayNumber - airedFrom.DayNumber;
        if (daysSince < 0)
            return null;

        var episode = daysSince / 7 + 1;
        return anime.TotalEpisodes is { } total ? Math.Min(episode, total) : episode;
    }
}
