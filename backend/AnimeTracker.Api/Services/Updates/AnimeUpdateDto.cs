namespace AnimeTracker.Api.Services.Updates;

/// <summary>One shown update — an eligible <c>AnimeUpdate</c> row (design.md
/// D4/D5) carrying everything a card or history row needs to render. Episode
/// count and premiere date are the anime's <em>current</em> values, read live
/// rather than from anything captured when the row was detected, so a later
/// correction shows up here without rewriting the row (design.md D3). The
/// moved-from values are the opposite: exactly what was recorded, since for
/// those the news *is* the movement.</summary>
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
    DayOfWeek? CurrentBroadcastDayOfWeek, // local time, alongside the previous slot
    TimeOnly? CurrentBroadcastTime, // local time
    int? MovedEpisode,
    DateOnly? PreviousEpisodeDate,
    DateOnly? NewEpisodeDate,
    // Why this update concerns me: the affiliated entry + its relation
    // ("Sequel to <title>"), or, when there is no affiliation, my own entry's
    // own status ("Plan to watch") — task 6.3.
    string Reason,
    bool Seen);
