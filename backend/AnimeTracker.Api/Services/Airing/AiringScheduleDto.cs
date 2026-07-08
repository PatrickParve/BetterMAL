namespace AnimeTracker.Api.Services.Airing;

public record AiringSlotDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, string LocalTime, int? EpisodeNumber);

public record AiringDayDto(DateOnly LocalDate, DayOfWeek DayOfWeek, List<AiringSlotDto> Slots);

public record AiringWeekDto(DateOnly WeekStart, DateOnly WeekEnd, List<AiringDayDto> Days);
