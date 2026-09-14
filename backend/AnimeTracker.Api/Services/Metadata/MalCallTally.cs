namespace AnimeTracker.Api.Services.Metadata;

/// <summary>One tally per stage of one pass (announcement resolution, or the
/// staleness batch). An attempt is recorded immediately before its MAL call,
/// so it is counted even when the method that made it throws afterwards —
/// for example when the stage's final save fails. <see cref="UnavailableAnimeId"/>
/// names the anime whose call ended the stage on an outage-type failure, for
/// the background service to skip on the next pass (design D6).</summary>
public sealed class MalCallTally
{
    public int Attempts { get; private set; }
    public int Succeeded { get; private set; }
    public int? UnavailableAnimeId { get; private set; }

    public bool MalUnavailable => UnavailableAnimeId is not null;

    public void RecordAttempt() => Attempts++;

    public void RecordSuccess() => Succeeded++;

    public void RecordUnavailable(int animeId) => UnavailableAnimeId = animeId;
}
