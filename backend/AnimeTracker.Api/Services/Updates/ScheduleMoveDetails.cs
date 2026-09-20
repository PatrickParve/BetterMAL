namespace AnimeTracker.Api.Services.Updates;

/// <summary>Both ends of whichever schedule-change kinds are being passed to
/// <see cref="IAnimeUpdateRecorder"/> — only the fields matching a bit
/// actually present in the recorded kinds are read; the rest are
/// ignored.</summary>
public readonly record struct ScheduleMoveDetails(
    DateOnly? PreviousStartDate = null,
    string? PreviousBroadcastDayOfWeek = null,
    TimeOnly? PreviousBroadcastTime = null,
    DateOnly? NewStartDate = null,
    string? NewBroadcastDayOfWeek = null,
    TimeOnly? NewBroadcastTime = null,
    int? MovedEpisode = null,
    DateOnly? PreviousEpisodeDate = null,
    DateOnly? NewEpisodeDate = null);
