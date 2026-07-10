using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

public class AiringScheduleService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeScheduleService scheduleService,
    IBroadcastLocalTimeConverter broadcastConverter) : IAiringScheduleService
{
    public async Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default)
    {
        var referenceDate = weekReferenceDate ?? broadcastConverter.GetLocalDate(DateTimeOffset.UtcNow);
        var weekStart = broadcastConverter.GetStartOfWeek(referenceDate);
        var weekDates = Enumerable.Range(0, 7).Select(weekStart.AddDays).ToList();

        var entries = await entryRepository.GetAllAsync(ct);
        var slotsByDate = weekDates.ToDictionary(date => date, _ => new List<AiringSlotDto>());

        // Resolve every my-list show against each of the week's seven local
        // dates. The schedule service bounds each show to the weeks it actually
        // airs (and skips break weeks), so navigating to a past/future week
        // shows exactly what aired then — not the same shows every week.
        foreach (var entry in entries)
        {
            foreach (var date in weekDates)
            {
                var episode = scheduleService.ResolveOnLocalDate(entry.Anime, date);
                if (episode is null)
                    continue;

                slotsByDate[date].Add(new AiringSlotDto(
                    entry.AnimeId,
                    entry.Anime.Title,
                    entry.Anime.EnglishTitle,
                    entry.Anime.PictureUrl,
                    episode.LocalTime.ToString("HH:mm"),
                    episode.EpisodeNumber));
            }
        }

        var days = weekDates
            .Select(date => new AiringDayDto(
                date,
                date.DayOfWeek,
                slotsByDate[date].OrderBy(s => s.LocalTime, StringComparer.Ordinal).ToList()))
            .ToList();

        return new AiringWeekDto(weekStart, weekStart.AddDays(6), days);
    }
}
