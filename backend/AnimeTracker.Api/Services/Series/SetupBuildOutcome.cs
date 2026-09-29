namespace AnimeTracker.Api.Services.Series;

/// <summary>What <see cref="ISeriesService.BuildForSetupAsync"/> found for one
/// anime (first-run-setup design.md D9).</summary>
public enum SetupBuildOutcome
{
    /// <summary>The anime holds a primary membership in an up-to-date series that
    /// isn't partial — built now, or already stored that way. A series stored as
    /// truncated (it reached the member cap) counts: that is a finished build,
    /// not a failed one.</summary>
    Settled,

    /// <summary>The anime's story relations lead to no other anime, so no series
    /// exists for it and none was stored.</summary>
    NoSeries,

    /// <summary>The build ran but a member fetch or a probe failed, so the series
    /// was stored partial. With no budget that is the only way to be partial: a
    /// temporary failure the caller retries.</summary>
    Partial,
}
