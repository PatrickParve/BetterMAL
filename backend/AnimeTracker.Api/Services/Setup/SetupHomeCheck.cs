using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Decides whether the library is complete and Home may open (design D14; spec "Home
/// opens when the library is complete"). All four must hold:
/// <list type="number">
/// <item><description>the list has been read in the current run;</description></item>
/// <item><description>every list anime is fully fetched or skipped for good;</description></item>
/// <item><description>every list anime that isn't skipped is covered by an up-to-date,
/// non-partial series, or was found to belong to none;</description></item>
/// <item><description>every anime in the airing priority set has its airing-fetched mark, or is
/// waiting on an AniList retry, or AniList is down.</description></item>
/// </list>
/// The last is what keeps AniList from ever holding Home: an outage, or an anime that keeps failing,
/// is airing work for the background. A MyAnimeList failure is different: the anime it failed for
/// still has details or a series to get, so the first three hold Home until it is resolved.
/// <para>Everything is read from the database each time (<see cref="SetupLibrary"/>), so a restart,
/// or a series build that settled several anime at once, needs no bookkeeping.</para></summary>
internal sealed class SetupHomeCheck(
    SetupWorkerContext context,
    RetryLadder aniListLadder,
    AniListServiceHealth aniListHealth)
{
    public async Task<bool> HoldsAsync(CancellationToken ct)
    {
        var snapshot = context.State.Snapshot();
        if (!snapshot.ListReadThisRun)
            return false;

        using var scope = context.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var library = await SetupLibrary.LoadAsync(db, ct);

        if (library.Anime.Any(a => a.DetailsLeft))
            return false;

        if (library.Anime.Any(a =>
                !a.NotOnMal && !library.Covered.Contains(a.AnimeId) && !snapshot.NoSeriesAnimeIds.Contains(a.AnimeId)))
            return false;

        var today = scope.ServiceProvider.GetRequiredService<IBroadcastLocalTimeConverter>().GetLocalDate(context.Clock());
        var aniListDown = aniListHealth.IsDown;
        return library.Anime
            .Where(a => AiringPriority.IsPriority(AiringPriority.TierOf(a.AiringStatus, a.AiredFrom, today)))
            .All(a => library.Marked.Contains(a.AnimeId) || aniListDown || aniListLadder.HasFailed(a.AnimeId));
    }
}
