namespace AnimeTracker.Api.Services.Updates;

/// <summary>One shown update — an eligible <c>AnimeUpdate</c> row (design.md
/// D4/D5) carrying everything a card or history row needs to render. Episode
/// count and premiere date are the anime's <em>current</em> values, read live
/// rather than from anything captured when the row was detected, so a later
/// correction shows up here without rewriting the row (design.md D3). A
/// schedule move's two ends are the opposite: <c>NewStartDate</c>,
/// <c>NewBroadcastDayOfWeek</c> and <c>NewBroadcastTime</c> mean "the value
/// it moved to, as recorded — or the anime's current value where the row
/// predates this being stored" (record-both-ends-of-a-schedule-move design.md
/// D3), and <c>Previous*</c> means exactly what was recorded, with no live
/// fallback. Neither is re-derived from the anime's current record once
/// recorded, so the pair a card reports cannot change because the anime moved
/// again.</summary>
public record AnimeUpdateDto(
    long Id,
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    IReadOnlyList<string> Kinds,
    DateTimeOffset DetectedAt,
    int? TotalEpisodes,
    DateOnly? AiredFrom,
    DateOnly? PreviousStartDate,
    DayOfWeek? PreviousBroadcastDayOfWeek, // local time (task 6.4), null unless Kinds includes BroadcastSlotChanged
    TimeOnly? PreviousBroadcastTime, // local time
    // The value the schedule change moved to, as recorded, or the anime's
    // current value where the row predates this being stored (design.md D3).
    DateOnly? NewStartDate,
    DayOfWeek? NewBroadcastDayOfWeek, // local time
    TimeOnly? NewBroadcastTime, // local time
    int? MovedEpisode,
    DateOnly? PreviousEpisodeDate,
    DateOnly? NewEpisodeDate,
    // Why this update concerns me: the affiliated entry + its relation
    // ("Sequel to <title>"), or, when there is no affiliation, my own entry's
    // own status ("Plan to watch") — task 6.3.
    string Reason,
    bool Seen);
