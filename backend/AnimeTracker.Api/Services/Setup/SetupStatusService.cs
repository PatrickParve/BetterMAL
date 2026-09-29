using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Builds the setup status (design D17) that the setup screen polls once a
/// second and Settings' Library data entry polls after it. It makes no outside call:
/// every count is read from the database and the rest from the coordinator's
/// in-memory snapshot, the two services' health and the credentials. Two reads of
/// the list's anime and one of the series coverage keep it cheap enough to poll.
/// <para>What a step has left is never held in memory (design D1): it is read here
/// from the same rows the workers read, so the screen and the work can't disagree, and
/// an install that finished setup long ago reports every step done from its data
/// alone.</para></summary>
public class SetupStatusService(
    AnimeTrackerDbContext db,
    SetupGate gate,
    ISetupCoordinator coordinator,
    IOptions<MalOptions> malOptions,
    IMalTokenStore tokenStore,
    MalServiceHealth malHealth,
    AniListServiceHealth aniListHealth,
    IBroadcastLocalTimeConverter localTimeConverter)
{
    public async Task<SetupStatusDto> GetAsync(CancellationToken ct = default)
    {
        var snapshot = coordinator.GetSnapshot();
        var finished = gate.IsFinished;

        // The same read the workers and the Home check make (SetupLibrary), so what the
        // screen counts is what the work is working on.
        var library = await SetupLibrary.LoadAsync(db, ct);
        var listAnime = library.Anime;
        var listIds = listAnime.Select(a => a.AnimeId).ToHashSet();
        var marked = library.Marked;
        var covered = library.Covered;

        var token = await tokenStore.GetAsync(ct);

        // MyAnimeList answered 404 for these when the details step asked: exactly
        // that step's permanent mark, so one a later scheduled refresh fetches
        // drops out on its own.
        var notOnMal = listAnime.Where(a => a.NotOnMal).ToList();
        var notOnMalIds = notOnMal.Select(a => a.AnimeId).ToHashSet();

        var detailsDone = listAnime.Count(a => !a.DetailsLeft);

        var seriesTargets = listIds.Where(id => !notOnMalIds.Contains(id)).ToHashSet();
        var seriesSettled = seriesTargets.Count(id => covered.Contains(id) || snapshot.NoSeriesAnimeIds.Contains(id));

        var today = localTimeConverter.GetLocalDate(DateTimeOffset.UtcNow);
        var priority = listAnime
            .Where(a => AiringPriority.IsPriority(AiringPriority.TierOf(a.AiringStatus, a.AiredFrom, today)))
            .ToList();

        var listCount = listAnime.Count;

        // Steps whose counts the database gives are done as soon as nothing is
        // left, once the list has been read: before that, "nothing left" only means
        // nothing has been stored yet.
        var listSettled = finished || snapshot.ListReadThisRun;

        // A step's estimate is left out while its service is limiting requests, and the list
        // read's while it waits for Reconnect: the workers' pace says nothing about how long
        // those last. Applied here, at read time, so a stored estimate can't outlive the
        // throttle it was made before.
        var malThrottled = malHealth.ThrottledUntil is not null;
        var steps = new SetupStepsDto(
            List: ListStep(snapshot, finished, listCount, malThrottled || snapshot.WaitingForReconnect),
            Details: Step(snapshot.Details, detailsDone, listCount, listSettled, malThrottled),
            Series: SeriesStep(snapshot, finished, seriesSettled, seriesTargets.Count, listSettled, malThrottled),
            Airing: Step(snapshot.Airing, marked.Count, listCount, listSettled, aniListHealth.ThrottledUntil is not null));

        var skipped = notOnMal
            .Select(a => new SetupSkippedDto(a.AnimeId, a.Title, SetupSkipReason.NotOnMal, null))
            .Concat(snapshot.UnrecognizedStatuses
                .Select(s => new SetupSkippedDto(s.AnimeId, s.Title, SetupSkipReason.UnrecognizedStatus, s.MalStatus)))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SetupStatusDto(
            Finished: finished,
            MissingCredentials: MalCredentials.Missing(malOptions.Value),
            Connection: new SetupConnectionDto(MalConnectionState.Of(token), token?.ConnectionLostAt),
            WaitingForReconnect: snapshot.WaitingForReconnect,
            Steps: steps,
            AiringPriority: new SetupProgressDto(priority.Count(a => marked.Contains(a.AnimeId)), priority.Count),
            AiringDraining: snapshot.AiringDraining,
            // After Home a service's trouble is setup's to show only while setup's leftover work is
            // using it: MyAnimeList's steps are over, and the airing rest uses AniList only while it
            // drains. Health is fed by every request the app makes, so outside that it would show an
            // outage an ordinary job met, under "Setup retries automatically", with a Retry now that
            // wakes nothing (found in task 12.4).
            Services: [ServiceOf(malHealth, inUse: !finished), ServiceOf(aniListHealth, inUse: !finished || snapshot.AiringDraining)],
            Skipped: skipped);
    }

    private static SetupStepDto Step(SetupStepState state, int done, int total, bool listSettled, bool estimateHeld)
    {
        var complete = listSettled && done >= total;
        return new SetupStepDto(
            complete ? SetupStepPhase.Done : state.Phase,
            done,
            total,
            complete ? null : EtaOf(state, estimateHeld),
            complete ? null : RetryWaitOf(state));
    }

    // "Settled" is a series or a finding that there is none, and the second is remembered only in
    // memory, by the process that built it (design D9). Once setup has finished and this process
    // did not run it (a restart, or an install upgraded past setup), a list anime with no series
    // has nothing in the database to show it was ever built, so the count would sit below the
    // total for ever. The step ended when setup did: Home doesn't open before every list anime is
    // settled. That is the rule the list step follows below.
    private static SetupStepDto SeriesStep(
        SetupSnapshot snapshot, bool finished, int settled, int total, bool listSettled, bool estimateHeld) =>
        finished && !snapshot.ListReadThisRun
            ? new SetupStepDto(SetupStepPhase.Done, total, total, null, null)
            : Step(snapshot.Series, settled, total, listSettled, estimateHeld);

    // No estimate for a step that is paused, or whose service is throttled or login refused
    // (design D17): an estimate then would be a guess at how long something else takes.
    private static double? EtaOf(SetupStepState state, bool estimateHeld) =>
        estimateHeld || state.Phase == SetupStepPhase.Paused ? null : state.EtaSeconds;

    // The list step's count is the read's own, in memory: the database holds every
    // entry the read stored, but not how far this run's read has got. A finished
    // install has nothing to read.
    private static SetupStepDto ListStep(SetupSnapshot snapshot, bool finished, int listCount, bool estimateHeld)
    {
        if (finished && !snapshot.ListReadThisRun)
            return new SetupStepDto(SetupStepPhase.Done, listCount, listCount, null, null);

        var state = snapshot.List;
        var phase = snapshot.ListReadThisRun ? SetupStepPhase.Done : state.Phase;
        // Once the read is over the total is what was read, whether or not
        // MyAnimeList ever stated one.
        var total = snapshot.ListReadThisRun ? snapshot.ListTotal ?? snapshot.ListRead : snapshot.ListTotal;
        return new SetupStepDto(
            phase,
            snapshot.ListRead,
            total,
            phase == SetupStepPhase.Done ? null : EtaOf(state, estimateHeld),
            phase == SetupStepPhase.Done ? null : RetryWaitOf(state));
    }

    private static SetupWaitingRetryDto? RetryWaitOf(SetupStepState state) =>
        state.WaitingRetry is { } wait ? new SetupWaitingRetryDto(wait.Count, wait.NextAt) : null;

    private static SetupServiceDto ServiceOf(ServiceHealth health, bool inUse) =>
        inUse
            ? new(health.Name, health.IsDown, health.IsDown ? health.NextTryAt : null, health.ThrottledUntil)
            : new(health.Name, false, null, null);
}
