using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Setup's first step: read the whole MyAnimeList list, a page at a time, and store
/// every entry with a basic anime row (design D7; spec "Reading your list stores every entry
/// and a basic anime row"). It is the only step that needs the login.
/// <list type="bullet">
/// <item><description><b>A page is stored before the next is asked for</b>: the missing rows,
/// the new entries and the refreshed old ones in one save, then the search index is told. A
/// row that already exists is never replaced by a basic one, so a fully fetched anime keeps
/// its details.</description></item>
/// <item><description><b>Nothing is deleted.</b> The list is read again at the start of every
/// run, so a re-read only adds what is missing and refreshes the entries. During setup no
/// local edit can exist (the API refuses them), so MyAnimeList's values are the truth.</description></item>
/// <item><description><b>An unrecognized status is left out</b>: no entry and no row, a
/// warning, and a record in memory of its title and status for Settings.</description></item>
/// <item><description><b>A refused login</b> marks the step as waiting for Reconnect and lets
/// the steps that need no login start on what is stored; the read runs again when the OAuth
/// callback signals.</description></item>
/// <item><description><b>Any other failure</b> retries on the same ladder as everything
/// else, and a MyAnimeList that is down holds the read like the other MyAnimeList
/// steps.</description></item>
/// </list></summary>
internal sealed class SetupListStep(
    SetupWorkerContext context,
    ISetupTrigger trigger,
    MalServiceHealth health,
    RetryLadder ladder)
{
    // The read's own place on the ladder. An anime id is never negative, so it can't collide
    // with one.
    private const int ListKey = -1;

    /// <summary>Runs until the list has been read. <paramref name="stored"/> completes when
    /// the steps that read stored entries may start: the read finished, or the login was
    /// refused and there is nothing more to wait for. <paramref name="read"/> completes only
    /// when the read finished, which is what setup finishing needs.</summary>
    public async Task RunAsync(TaskCompletionSource stored, TaskCompletionSource read, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var woken = context.Wake.Next();
            var now = context.Clock();

            if (health.IsDown && health.NextTryAt is { } tryAt && tryAt > now)
            {
                context.State.SetPhase(SetupStep.List, SetupStepPhase.Paused);
                await SetupQueue.SleepAsync(woken, tryAt - now, ct);
                continue;
            }

            if (ladder.EarliestDue([ListKey], now) is { } retryAt)
            {
                await SetupQueue.SleepAsync(woken, retryAt - now, ct);
                continue;
            }

            var probing = health.IsDown;
            context.State.SetPhase(SetupStep.List, probing ? SetupStepPhase.Paused : SetupStepPhase.Running);
            try
            {
                await ReadAsync(ct);

                ladder.Clear(ListKey);
                context.State.SetWaitingForReconnect(false);
                context.State.MarkListRead();
                context.State.SetPhase(SetupStep.List, SetupStepPhase.Done);
                // read first: whoever wakes on stored finds the read already complete, so no
                // step runs a second round for nothing.
                read.TrySetResult();
                stored.TrySetResult();
                context.Progress.Pulse();
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (MalAuthorizationRequiredException)
            {
                context.Logger.LogWarning(
                    "Setup: MyAnimeList refused the stored login while reading the list. Reconnect resumes the read; the other steps carry on with what is stored.");
                context.State.SetWaitingForReconnect(true);
                context.State.SetPhase(SetupStep.List, SetupStepPhase.Paused);
                stored.TrySetResult();

                // The OAuth callback signals after a Reconnect. Retry now doesn't reach here:
                // it can't make MyAnimeList accept a login it refused.
                await trigger.WaitAsync(ct);
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Setup: reading the list failed; trying again on the retry schedule.");
                ladder.RecordFailure(ListKey, context.Clock());
                if (probing && health.IsDown)
                    health.Advance();
            }
        }
    }

    private async Task ReadAsync(CancellationToken ct)
    {
        using var scope = context.ScopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IMalSetupClient>();

        var total = await TryGetTotalAsync(client, ct);
        context.State.SetListProgress(0, total);

        // A read starts from no pace: a retry after a failure, or after a Reconnect, is not
        // the pages read before it.
        context.State.ResetEta(SetupStep.List);

        var order = new List<int>();
        var unrecognized = new List<UnrecognizedStatusSkip>();
        var readCount = 0;

        await foreach (var page in client.GetUserAnimeListPagesAsync(ct))
        {
            await SavePageAsync(page, order, unrecognized, ct);

            readCount += page.Count;
            // A list that grew since the count was asked for still ends at 100%.
            var shownTotal = total is { } known ? Math.Max(known, readCount) : (int?)null;
            context.State.SetListProgress(readCount, shownTotal);
            // With no total there is nothing to subtract from, so no estimate: the bar moves
            // without one, as the unknown-total presentation says.
            context.State.RecordProgress(
                SetupStep.List, page.Count, shownTotal is { } t ? t - readCount : null, context.Clock());
            context.State.SetUnrecognized(unrecognized);
            context.Progress.Pulse();
        }

        context.State.SetListOrder(order);
        context.Logger.LogInformation(
            "Setup: read {Count} list entries ({Skipped} left out for an unrecognized status).", readCount, unrecognized.Count);
    }

    // MyAnimeList's list pages carry no total (design D7), so the real one comes from one
    // statistics request. It only feeds the progress bar, so its failure is not the read's:
    // the bar then takes the unknown-total presentation until the last page. A refused login
    // is the read's own problem, and is left to it.
    private async Task<int?> TryGetTotalAsync(IMalSetupClient client, CancellationToken ct)
    {
        try
        {
            return await client.GetUserAnimeCountAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (MalAuthorizationRequiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Setup: could not ask MyAnimeList how long the list is; the total stays unknown until the last page.");
            return null;
        }
    }

    private async Task SavePageAsync(
        List<MalUserAnimeListEdge> page, List<int> order, List<UnrecognizedStatusSkip> unrecognized, CancellationToken ct)
    {
        var recognized = new List<MalUserAnimeListEdge>(page.Count);
        foreach (var edge in page)
        {
            // Never guessed at: an unrecognized status leaves the anime out entirely, before
            // anything is written for it (the same rule as the list import).
            if (edge.ListStatus.HasRecognizedStatus())
            {
                recognized.Add(edge);
                continue;
            }

            context.Logger.LogWarning(
                "Setup: skipping anime {AnimeId} ({Title}): MyAnimeList list status '{Status}' is not recognized.",
                edge.Node.Id, edge.Node.Title, edge.ListStatus?.Status);
            unrecognized.Add(new UnrecognizedStatusSkip(edge.Node.Id, edge.Node.Title, edge.ListStatus?.Status ?? ""));
        }

        if (recognized.Count == 0)
            return;

        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var searchIndex = scope.ServiceProvider.GetRequiredService<IAnimeSearchIndex>();

        var ids = recognized.Select(e => e.Node.Id).ToList();
        var existingAnimeIds = (await db.AnimeMetadata.Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var existingEntries = await db.UserAnimeEntries.Where(e => ids.Contains(e.AnimeId)).ToDictionaryAsync(e => e.AnimeId, ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var edge in recognized)
        {
            var animeId = edge.Node.Id;
            order.Add(animeId);

            // A row that exists is kept as it is, whatever it holds: never downgraded. What
            // this page adds goes into the same sets, so an anime listed twice is stored once.
            if (existingAnimeIds.Add(animeId))
                db.AnimeMetadata.Add(edge.Node.ToBasicAnimeMetadata(now));

            if (existingEntries.TryGetValue(animeId, out var entry))
            {
                edge.ListStatus.ApplyTo(entry, now);
            }
            else
            {
                entry = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);
                db.UserAnimeEntries.Add(entry);
                existingEntries[animeId] = entry;
            }
        }

        await db.SaveChangesAsync(ct);

        // After the save, never before: a search that rebuilds in between would miss the rows.
        searchIndex.Invalidate();
    }
}
