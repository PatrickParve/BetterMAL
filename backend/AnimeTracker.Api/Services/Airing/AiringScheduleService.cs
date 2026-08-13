using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

public class AiringScheduleService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeAiringRepository episodeAiringRepository,
    IBroadcastLocalTimeConverter broadcastConverter) : IAiringScheduleService
{
    public async Task<AiringWeekDto> GetWeekAsync(DateOnly? weekReferenceDate, CancellationToken ct = default)
    {
        var referenceDate = weekReferenceDate ?? broadcastConverter.GetLocalDate(DateTimeOffset.UtcNow);
        var weekStart = broadcastConverter.GetStartOfWeek(referenceDate);
        var weekDates = Enumerable.Range(0, 7).Select(weekStart.AddDays).ToList();

        var entries = await entryRepository.GetAllAsync(ct);
        var animeById = entries.ToDictionary(e => e.AnimeId);

        // Convert local Monday 00:00 and the following Monday 00:00 through the
        // local zone, so a DST-shifted week still covers exactly seven local
        // days — one range query across every my-list anime, rather than
        // resolving each show against each of the week's seven dates.
        var weekStartUtc = broadcastConverter.LocalMidnightUtc(weekStart);
        var weekEndUtc = broadcastConverter.LocalMidnightUtc(weekStart.AddDays(7));
        var rows = await episodeAiringRepository.GetRowsInRangeAsync(animeById.Keys.ToList(), weekStartUtc, weekEndUtc, ct);

        var rowsByDate = weekDates.ToDictionary(date => date, _ => new List<AiringDaySlotGrouper.Row>());
        foreach (var row in rows)
        {
            if (!animeById.TryGetValue(row.AnimeId, out var entry))
                continue;

            var localDate = broadcastConverter.GetLocalDate(row.AirsAtUtc);
            if (!rowsByDate.TryGetValue(localDate, out var dayRows))
                continue; // outside the requested week — guards a boundary edge, shouldn't happen given the range query

            dayRows.Add(new AiringDaySlotGrouper.Row(
                entry.AnimeId,
                entry.Anime.Title,
                entry.Anime.EnglishTitle,
                entry.Anime.PictureUrl,
                row.AirsAtUtc,
                broadcastConverter.GetLocalTime(row.AirsAtUtc).ToString("HH:mm"),
                row.Episode));
        }

        var days = weekDates
            .Select(date => new AiringDayDto(
                date,
                date.DayOfWeek,
                AiringDaySlotGrouper.Group(rowsByDate[date])))
            .ToList();

        return new AiringWeekDto(weekStart, weekStart.AddDays(6), days);
    }
}
