using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Setup's second step: one full-detail request per list anime that was never fully
/// fetched, at the shared MyAnimeList pace (design D8; spec "Fetching anime details fills in
/// every list anime"). The order is Watching or Rewatching first, then currently airing,
/// then the list's own order (<see cref="SetupOrdering.DetailsOrder"/>).
/// <list type="bullet">
/// <item><description><b>Each anime is saved whole</b>: its row, relations and picture set in
/// one save, so a restart never leaves one half-saved. The fetch itself is what marks it done
/// (<see cref="AnimeTracker.Api.Models.AnimeMetadata.LastSyncedAt"/>), so a restart carries on
/// with exactly the anime not yet saved.</description></item>
/// <item><description><b>No news.</b> The change detector is not called: this is a first full
/// fetch, so it would record nothing, and its unconditional series-build enqueue is what setup
/// must not do, since it builds every series itself.</description></item>
/// <item><description><b>A 404 is permanent</b>: the anime is marked not found
/// (<see cref="AnimeTracker.Api.Models.AnimeMetadata.LastRefreshFailedAt"/>), no longer counts as
/// left, and doesn't hold Home. Anything else is temporary and goes on the retry ladder, an
/// unexpected exception included: dropping one would let the anime through unfetched.</description></item>
/// </list></summary>
internal sealed class SetupDetailsStep(SetupWorkerContext context, MalServiceHealth health, RetryLadder ladder)
{
    /// <summary>Runs until no list anime is left to fetch. An anime the list read has yet to
    /// store is the coordinator's to run another round for.</summary>
    public Task RunAsync(CancellationToken ct) =>
        new SetupQueue(SetupStep.Details, health, ladder, context)
            .RunAsync(GetTargetsAsync, ProcessAsync, batchSize: 1, ct);

    private async Task<IReadOnlyList<int>> GetTargetsAsync(CancellationToken ct)
    {
        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var library = await SetupLibrary.LoadAsync(db, ct);

        return SetupOrdering.DetailsOrder(library.Anime.Where(a => a.DetailsLeft), context.State.ListOrder);
    }

    private async Task ProcessAsync(IReadOnlyList<int> animeIds, CancellationToken ct)
    {
        var animeId = animeIds[0];
        try
        {
            await FetchAndSaveAsync(animeId, ct);
            ladder.Clear(animeId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (MalCallFailure.Classify(ex) == MalCallFailureKind.NotFound)
        {
            context.Logger.LogWarning(
                "Setup: MyAnimeList has no anime {AnimeId}; skipping it for good.", animeId);
            await MarkNotFoundOrRetryAsync(animeId, ct);
        }
        catch (Exception ex)
        {
            // A timeout arrives here as a TaskCanceledException with ct still live: a
            // temporary failure of this anime, never the app shutting down.
            context.Logger.LogWarning(ex, "Setup: fetching the details of anime {AnimeId} failed; it will be tried again.", animeId);
            ladder.RecordFailure(animeId, context.Clock());
        }
    }

    private async Task FetchAndSaveAsync(int animeId, CancellationToken ct)
    {
        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var malClient = scope.ServiceProvider.GetRequiredService<IMalClient>();
        var searchIndex = scope.ServiceProvider.GetRequiredService<IAnimeSearchIndex>();

        // Every anime here is a list anime, so it always qualifies for the with-pictures
        // field set (the same fetch the scheduled refresh makes for one).
        var details = await malClient.GetAnimeDetailsAsync(
            animeId, fields: [MalClient.FullDetailWithPicturesAnimeFields], ct: ct);

        // Related anime must be loaded before ApplyTo replaces the collection: without a
        // tracked snapshot EF would re-insert rows that exist instead of replacing them.
        var anime = await db.AnimeMetadata.Include(a => a.RelatedAnime).FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new InvalidOperationException($"Anime {animeId} has no stored row to fill in.");

        // ApplyTo writes the picture set too, and clears a not-found mark.
        details.ApplyTo(anime, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        searchIndex.Invalidate();
    }

    private async Task MarkNotFoundOrRetryAsync(int animeId, CancellationToken ct)
    {
        try
        {
            using var scope = context.ScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (anime is not null)
            {
                anime.LastRefreshFailedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            ladder.Clear(animeId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Without the mark the anime would be asked for again at once; let it wait its turn.
            context.Logger.LogWarning(ex, "Setup: could not record that anime {AnimeId} is not on MyAnimeList; it will be tried again.", animeId);
            ladder.RecordFailure(animeId, context.Clock());
        }
    }
}
