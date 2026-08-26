namespace AnimeTracker.Api.Services.Updates;

/// <summary>The moved-from values for whichever schedule-change kinds are
/// being passed to <see cref="IAnimeUpdateRecorder"/> — only the fields
/// matching a bit actually present in the recorded kinds are read; the rest
/// are ignored.</summary>
public readonly record struct ScheduleMoveDetails(
    DateOnly? PreviousStartDate = null,
    string? PreviousBroadcastDayOfWeek = null,
    TimeOnly? PreviousBroadcastTime = null,
    int? MovedEpisode = null,
    DateOnly? PreviousEpisodeDate = null,
    DateOnly? NewEpisodeDate = null);
