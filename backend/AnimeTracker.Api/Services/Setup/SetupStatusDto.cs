namespace AnimeTracker.Api.Services.Setup;

/// <summary>What <c>GET api/setup/status</c> serves (design D17): everything the
/// setup screen and Settings' Library data entry show. Enum values serialize as
/// strings, so a step's phase reads <c>Waiting</c>, <c>Running</c>, <c>Paused</c> or
/// <c>Done</c>.</summary>
public record SetupStatusDto(
    bool Finished,
    IReadOnlyList<string> MissingCredentials,
    SetupConnectionDto Connection,
    bool WaitingForReconnect,
    SetupStepsDto Steps,
    SetupProgressDto AiringPriority,
    bool AiringDraining,
    IReadOnlyList<SetupServiceDto> Services,
    IReadOnlyList<SetupSkippedDto> Skipped);

/// <summary>The MyAnimeList login's state, as <c>api/mal-auth/status</c> reports it:
/// <c>Connected</c>, <c>Lost</c> or <c>NotConnected</c>.</summary>
public record SetupConnectionDto(string State, DateTimeOffset? LostAt);

public record SetupStepsDto(SetupStepDto List, SetupStepDto Details, SetupStepDto Series, SetupStepDto Airing);

/// <summary><paramref name="Total"/> is null while the step doesn't know it yet (the list
/// read, before MyAnimeList has said how long the list is).</summary>
public record SetupStepDto(
    SetupStepPhase Phase, int Done, int? Total, double? EtaSeconds, SetupWaitingRetryDto? WaitingRetry);

public record SetupWaitingRetryDto(int Count, DateTimeOffset NextAt);

public record SetupProgressDto(int Done, int Total);

/// <summary>One outside service: whether it appears down, when its paused queue tries
/// again, and until when it is limiting requests.</summary>
public record SetupServiceDto(string Name, bool Down, DateTimeOffset? NextTryAt, DateTimeOffset? ThrottledUntil);

/// <summary>An anime setup left out for good. <paramref name="Reason"/> is
/// <c>NotOnMal</c> (MyAnimeList answers 404 for it) or <c>UnrecognizedStatus</c>, in
/// which case <paramref name="MalStatus"/> is the status MyAnimeList gave.</summary>
public record SetupSkippedDto(int AnimeId, string Title, SetupSkipReason Reason, string? MalStatus);

public enum SetupSkipReason
{
    NotOnMal,
    UnrecognizedStatus,
}
