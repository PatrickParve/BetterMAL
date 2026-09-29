using System.Data;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Transfer;

/// <summary>What Prepare (design.md D4) learned about one file series entry:
/// the local series id it resolves to, or the reason it doesn't.</summary>
internal readonly record struct SeriesResolution(int? LocalSeriesId, string? FailureReason);

/// <summary>Prepare's output, handed to Apply (design.md D4): which unseen
/// anime were fetched (and which could not be), and how every file series id
/// resolved. Nothing from the file is written yet.</summary>
internal sealed record PreparedImport(
    Dictionary<int, string> UnfetchableAnimeReasons,
    List<int> FetchedAnimeIds,
    Dictionary<int, SeriesResolution> SeriesResolutions);

internal readonly record struct DraftFailure(TransferImportFailureSubject Subject, int Id, string What, TransferImportFailureKind Kind, string Reason);

/// <summary>Prepares an import — fetching from MyAnimeList whatever the file
/// needs that this device lacks, and from TMDB the pictures a refused TMDB
/// choice names — then applies it in one transaction (design.md D4, D14).
/// <see cref="TransferImportBackgroundService"/>'s scoped dependency.</summary>
public class TransferImportRunner(
    AnimeTrackerDbContext db,
    IServiceScopeFactory scopeFactory,
    IMetadataRefreshService metadataRefreshService,
    ISeriesService seriesService,
    IArtworkSelectionService artworkSelectionService,
    IPictureRefreshService pictureRefreshService,
    ITmdbArtworkService tmdbArtwork,
    ITopAnimeSelectionRepository topAnimeSelectionRepository,
    RefreshGate refreshGate)
{
    public async Task<TransferImportReport> RunAsync(TransferFile file, ITransferImportProgressTracker progress, CancellationToken ct)
    {
        var prepared = await PrepareAsync(file, progress, ct);
        return await ApplyAsync(file, prepared, ct);
    }

    // --- Prepare (tasks.md 6.1-6.5) ---

    private async Task<PreparedImport> PrepareAsync(TransferFile file, ITransferImportProgressTracker progress, CancellationToken ct)
    {
        // 6.1: the planning read. Provisional only — apply re-decides every
        // comparison fresh, inside its own transaction (design.md D4 "Why
        // re-decide in apply"). This read only bounds the fetch work below.
        var storedEventIds = (await db.ActivityLogs.Select(l => l.EventId).ToListAsync(ct)).ToHashSet();
        var storedPictureTimes = await db.AnimeMetadata
            .Where(a => a.SelectedPictureModifiedAt != null)
            .Select(a => new { a.Id, a.SelectedPictureModifiedAt })
            .ToDictionaryAsync(a => a.Id, a => a.SelectedPictureModifiedAt!.Value, ct);
        var storedRankingTime = await topAnimeSelectionRepository.GetModifiedAtAsync(ct);
        var existingAnimeIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();

        var seenLogIds = new HashSet<Guid>();
        var newLogAnimeIds = new HashSet<int>();
        foreach (var record in file.Activity)
        {
            if (!seenLogIds.Add(record.Id)) continue; // a duplicated id: first occurrence only
            if (storedEventIds.Contains(record.Id)) continue;
            newLogAnimeIds.Add(record.AnimeId);
        }

        var picturesCandidateAnimeIds = file.AnimePictures
            .Where(p => !storedPictureTimes.TryGetValue(p.AnimeId, out var stored) || p.ModifiedAt > stored)
            .Select(p => p.AnimeId)
            .ToHashSet();

        var rankingIsCandidate = file.Ranking.ModifiedAt is { } fileRankingTime
            && (storedRankingTime is null || fileRankingTime > storedRankingTime);
        var rankingAnimeIds = rankingIsCandidate ? file.Ranking.AnimeIds.ToHashSet() : [];

        var seriesRootIds = file.Series.Select(s => s.SeriesId).ToHashSet();

        var idsToFetch = newLogAnimeIds
            .Union(picturesCandidateAnimeIds)
            .Union(rankingAnimeIds)
            .Union(seriesRootIds)
            .Where(id => !existingAnimeIds.Contains(id))
            .ToList();

        progress.Start(idsToFetch.Count + file.Series.Count);
        var done = 0;

        // 6.2: fetch every unseen anime a candidate or a series entry names.
        var unfetchableReasons = new Dictionary<int, string>();
        var fetchedAnimeIds = new List<int>();
        foreach (var animeId in idsToFetch)
        {
            ct.ThrowIfCancellationRequested();
            using (await refreshGate.LockAsync($"anime:{animeId}", ct))
            {
                // Re-checked after the gate is acquired, the detail page's own
                // pattern: a visit racing the import cannot insert the row twice.
                if (!await db.AnimeMetadata.AnyAsync(a => a.Id == animeId, ct))
                {
                    try
                    {
                        await metadataRefreshService.RefreshOneAsync(animeId, ct);
                        fetchedAnimeIds.Add(animeId);
                    }
                    catch (AnimeMetadataNotFoundException)
                    {
                        unfetchableReasons[animeId] = "MyAnimeList has no anime with this id.";
                    }
                    catch (Exception) when (ct.IsCancellationRequested is false)
                    {
                        unfetchableReasons[animeId] = "This anime could not be fetched from MyAnimeList.";
                    }
                }
            }
            progress.ReportProgress(++done);
        }

        // 6.3: resolve every file series id, building where needed.
        var seriesResolutions = new Dictionary<int, SeriesResolution>();
        foreach (var entry in file.Series)
        {
            ct.ThrowIfCancellationRequested();
            seriesResolutions[entry.SeriesId] = await ResolveSeriesAsync(entry.SeriesId, ct);
            progress.ReportProgress(++done);
        }

        // 6.4: pre-check picture-setting candidates; retry once after a refresh.
        // A refused choice is refreshed from the source its URL names (design.md
        // D14): a TMDB image refetches the TMDB sets the anime or series draws
        // from, anything else refetches MyAnimeList's picture sets. The retry
        // itself is Apply's own check, which reads what the refresh stored.
        // With no TMDB key configured the TMDB refresh is a no-op, so the choice
        // stays refused and Apply reports it like any other.
        foreach (var picture in file.AnimePictures)
        {
            if (picture.SelectedPictureUrl is not { } url) continue; // a clear needs no check
            if (!picturesCandidateAnimeIds.Contains(picture.AnimeId)) continue;
            if (unfetchableReasons.ContainsKey(picture.AnimeId)) continue;

            try
            {
                await artworkSelectionService.CheckAnimePictureAsync(picture.AnimeId, url, ct);
            }
            catch (ArtworkSelectionRejectedException) when (TmdbImageUrl.IsTmdbImage(url))
            {
                progress.AddToTotal(1);
                await tmdbArtwork.RefreshAnimeAsync(picture.AnimeId, force: true, ct);
                progress.ReportProgress(++done);
            }
            catch (ArtworkSelectionRejectedException)
            {
                progress.AddToTotal(1);
                await pictureRefreshService.RefreshOneAsync(picture.AnimeId, evenIfFetched: true, ct: ct);
                progress.ReportProgress(++done);
            }
        }

        foreach (var entry in file.Series)
        {
            if (entry.Picture?.Value is not { } url) continue;
            if (seriesResolutions[entry.SeriesId].LocalSeriesId is not { } localSeriesId) continue;

            try
            {
                await artworkSelectionService.CheckSeriesPictureAsync(localSeriesId, url, ct);
            }
            catch (ArtworkSelectionRejectedException) when (TmdbImageUrl.IsTmdbImage(url))
            {
                // Every set the series draws from, whatever its age, and past
                // the per-visit budget of a series page: an import is not a
                // page visit, and a set left unfetched is a choice refused.
                progress.AddToTotal(1);
                await tmdbArtwork.RefreshSeriesAsync(localSeriesId, int.MaxValue, force: true, ct);
                progress.ReportProgress(++done);
            }
            catch (ArtworkSelectionRejectedException)
            {
                var memberIds = await MyListMainLineMemberIdsAsync(db, localSeriesId, ct);
                progress.AddToTotal(memberIds.Count);
                foreach (var memberId in memberIds)
                {
                    await pictureRefreshService.RefreshOneAsync(memberId, evenIfFetched: true, ct: ct);
                    progress.ReportProgress(++done);
                }
            }
        }

        return new PreparedImport(unfetchableReasons, fetchedAnimeIds, seriesResolutions);
    }

    private async Task<SeriesResolution> ResolveSeriesAsync(int fileSeriesId, CancellationToken ct)
    {
        const string notPartOfSeries = "This anime is not part of a series on this device.";
        try
        {
            var localId = await seriesService.FindSeriesIdAsync(fileSeriesId, ct);
            if (localId is null)
            {
                await seriesService.GetSeriesAsync(fileSeriesId, ct); // builds; a partial build still resolves
                localId = await seriesService.FindSeriesIdAsync(fileSeriesId, ct);
            }
            return localId is { } id ? new SeriesResolution(id, null) : new SeriesResolution(null, notPartOfSeries);
        }
        catch (SeriesNotFoundException)
        {
            return new SeriesResolution(null, notPartOfSeries);
        }
        catch (Exception) when (ct.IsCancellationRequested is false)
        {
            return new SeriesResolution(null, "The series could not be built.");
        }
    }

    private static Task<List<int>> MyListMainLineMemberIdsAsync(AnimeTrackerDbContext scopedDb, int seriesId, CancellationToken ct) =>
        scopedDb.SeriesMembers
            .Where(m => m.SeriesId == seriesId && m.IsMainLine && m.Anime.UserEntry != null)
            .OrderBy(m => m.Order)
            .Select(m => m.AnimeId)
            .ToListAsync(ct);

    // --- Apply (tasks.md 7.1-7.7) ---

    private async Task<TransferImportReport> ApplyAsync(TransferFile file, PreparedImport prepared, CancellationToken ct)
    {
        // 7.1: a fresh scope, so this DbContext tracks nothing prepare read —
        // a tracking query would otherwise return prepare's stale instances
        // rather than refreshing them (design.md D11).
        using var scope = scopeFactory.CreateScope();
        var applyDb = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var applyArtwork = scope.ServiceProvider.GetRequiredService<IArtworkSelectionService>();
        var applyRanking = scope.ServiceProvider.GetRequiredService<ITopAnimeSelectionRepository>();

        var failures = new List<DraftFailure>();
        var rankingAdded = new List<int>();
        var rankingRemoved = new List<int>();

        var transaction = await applyDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await using (transaction)
        {
            await ApplyLogRecordsAsync(applyDb, file, prepared, failures, ct);
            await ApplyAnimePicturesAsync(applyDb, applyArtwork, file, prepared, failures, ct);
            await ApplySeriesChoicesAsync(applyDb, applyArtwork, file, prepared, failures, ct);
            await ApplyRankingAsync(applyRanking, file, prepared, failures, rankingAdded, rankingRemoved, ct);

            // 7.6: any other exception (a serialization failure from a
            // concurrent save, say) propagates out of this using block
            // uncommitted — the transaction rolls back on disposal, and the
            // background service's own catch reaches the job's Fail.
            await transaction.CommitAsync(ct);
        }

        return await BuildReportAsync(applyDb, prepared, rankingAdded, rankingRemoved, failures, ct);
    }

    // 7.2: one insert, one save, per new record, in file order — the only
    // way local ids are guaranteed to follow file order (design.md D10).
    private static async Task ApplyLogRecordsAsync(
        AnimeTrackerDbContext applyDb, TransferFile file, PreparedImport prepared, List<DraftFailure> failures, CancellationToken ct)
    {
        var storedEventIds = (await applyDb.ActivityLogs.Select(l => l.EventId).ToListAsync(ct)).ToHashSet();
        var seenInFile = new HashSet<Guid>();
        var skippedByAnimeId = new Dictionary<int, (int Count, string Reason)>();

        void RecordSkip(int animeId, string reason)
        {
            skippedByAnimeId[animeId] = skippedByAnimeId.TryGetValue(animeId, out var existing)
                ? (existing.Count + 1, existing.Reason)
                : (1, reason);
        }

        foreach (var record in file.Activity)
        {
            if (!seenInFile.Add(record.Id)) continue;
            if (storedEventIds.Contains(record.Id)) continue;

            if (prepared.UnfetchableAnimeReasons.TryGetValue(record.AnimeId, out var fetchFailureReason))
            {
                RecordSkip(record.AnimeId, fetchFailureReason);
                continue;
            }

            var mappedType = TransferFileReader.MapChangeType(record.ChangeType);
            if (mappedType is null)
            {
                RecordSkip(record.AnimeId, "This build does not know one or more of these records' change types.");
                continue;
            }

            applyDb.ActivityLogs.Add(new ActivityLog
            {
                EventId = record.Id,
                Timestamp = record.Timestamp,
                AnimeId = record.AnimeId,
                ChangeType = mappedType.Value,
                ChangeDetail = record.ChangeDetail,
                PreviousEpisodesWatched = record.PreviousEpisodesWatched,
            });
            await applyDb.SaveChangesAsync(ct);
            storedEventIds.Add(record.Id);
        }

        foreach (var (animeId, (count, reason)) in skippedByAnimeId)
            failures.Add(new DraftFailure(
                TransferImportFailureSubject.Anime, animeId, $"edit history ({count} record{(count == 1 ? "" : "s")})", TransferImportFailureKind.EditHistory, reason));
    }

    // 7.3
    private static async Task ApplyAnimePicturesAsync(
        AnimeTrackerDbContext applyDb, IArtworkSelectionService applyArtwork, TransferFile file, PreparedImport prepared,
        List<DraftFailure> failures, CancellationToken ct)
    {
        foreach (var picture in file.AnimePictures)
        {
            if (prepared.UnfetchableAnimeReasons.TryGetValue(picture.AnimeId, out var fetchFailureReason))
            {
                failures.Add(new DraftFailure(TransferImportFailureSubject.Anime, picture.AnimeId, "chosen picture", TransferImportFailureKind.ChosenPicture, fetchFailureReason));
                continue;
            }

            var storedTime = await applyDb.AnimeMetadata.AsNoTracking()
                .Where(a => a.Id == picture.AnimeId)
                .Select(a => a.SelectedPictureModifiedAt)
                .FirstOrDefaultAsync(ct);
            if (storedTime is { } t && picture.ModifiedAt <= t) continue; // not newer: left exactly as it is

            try
            {
                await applyArtwork.AdoptAnimePictureAsync(picture.AnimeId, picture.SelectedPictureUrl, picture.ModifiedAt, ct);
            }
            catch (ArtworkSelectionRejectedException ex)
            {
                failures.Add(new DraftFailure(TransferImportFailureSubject.Anime, picture.AnimeId, "chosen picture", TransferImportFailureKind.ChosenPicture, ex.Message));
            }
        }
    }

    // 7.4: per resolved series and block, in file order — so two file
    // entries resolving to one local series (a moved root) compare against
    // whatever the earlier entry just stored (design.md D5).
    private static async Task ApplySeriesChoicesAsync(
        AnimeTrackerDbContext applyDb, IArtworkSelectionService applyArtwork, TransferFile file, PreparedImport prepared,
        List<DraftFailure> failures, CancellationToken ct)
    {
        foreach (var entry in file.Series)
        {
            var resolution = prepared.SeriesResolutions[entry.SeriesId];
            if (resolution.LocalSeriesId is not { } localSeriesId)
            {
                if (entry.Title is not null)
                    failures.Add(new DraftFailure(TransferImportFailureSubject.Series, entry.SeriesId, "series title", TransferImportFailureKind.SeriesTitle, resolution.FailureReason!));
                if (entry.Picture is not null)
                    failures.Add(new DraftFailure(TransferImportFailureSubject.Series, entry.SeriesId, "series picture", TransferImportFailureKind.SeriesPicture, resolution.FailureReason!));
                continue;
            }

            if (entry.Title is { } titleBlock)
            {
                var storedTime = await applyDb.Series.AsNoTracking()
                    .Where(s => s.Id == localSeriesId).Select(s => s.SelectedTitleModifiedAt).FirstOrDefaultAsync(ct);
                if (storedTime is null || titleBlock.ModifiedAt > storedTime)
                {
                    try
                    {
                        await applyArtwork.AdoptSeriesTitleAsync(localSeriesId, titleBlock.Value, titleBlock.ModifiedAt, ct);
                    }
                    catch (ArtworkSelectionRejectedException ex)
                    {
                        failures.Add(new DraftFailure(TransferImportFailureSubject.Series, entry.SeriesId, "series title", TransferImportFailureKind.SeriesTitle, ex.Message));
                    }
                }
            }

            if (entry.Picture is { } pictureBlock)
            {
                var storedTime = await applyDb.Series.AsNoTracking()
                    .Where(s => s.Id == localSeriesId).Select(s => s.SelectedPictureModifiedAt).FirstOrDefaultAsync(ct);
                if (storedTime is null || pictureBlock.ModifiedAt > storedTime)
                {
                    try
                    {
                        await applyArtwork.AdoptSeriesPictureAsync(localSeriesId, pictureBlock.Value, pictureBlock.ModifiedAt, ct);
                    }
                    catch (ArtworkSelectionRejectedException ex)
                    {
                        failures.Add(new DraftFailure(TransferImportFailureSubject.Series, entry.SeriesId, "series picture", TransferImportFailureKind.SeriesPicture, ex.Message));
                    }
                }
            }
        }
    }

    // 7.5
    private static async Task ApplyRankingAsync(
        ITopAnimeSelectionRepository applyRanking, TransferFile file, PreparedImport prepared, List<DraftFailure> failures,
        List<int> rankingAdded, List<int> rankingRemoved, CancellationToken ct)
    {
        var storedRankingTime = await applyRanking.GetModifiedAtAsync(ct);
        var rankingIsCandidate = file.Ranking.ModifiedAt is { } fileRankingTime
            && (storedRankingTime is null || fileRankingTime > storedRankingTime);
        if (!rankingIsCandidate) return;

        var blockingIds = file.Ranking.AnimeIds.Where(id => prepared.UnfetchableAnimeReasons.ContainsKey(id)).ToList();
        if (blockingIds.Count > 0)
        {
            foreach (var id in blockingIds)
                failures.Add(new DraftFailure(TransferImportFailureSubject.Anime, id, "ranking", TransferImportFailureKind.Ranking, prepared.UnfetchableAnimeReasons[id]));
            return;
        }

        try
        {
            var before = await applyRanking.GetOrderedAnimeIdsAsync(ct);
            await applyRanking.ReplaceAllAsync(file.Ranking.AnimeIds, file.Ranking.ModifiedAt!.Value, ct);
            rankingAdded.AddRange(file.Ranking.AnimeIds.Except(before));
            rankingRemoved.AddRange(before.Except(file.Ranking.AnimeIds));
        }
        catch (UnknownAnimeIdsException ex)
        {
            // The (unexpected) case of a row vanishing between prepare and
            // apply (design.md D9's fallback) — ReplaceAllAsync itself
            // refuses the whole list before touching a row.
            foreach (var id in ex.AnimeIds)
                failures.Add(new DraftFailure(TransferImportFailureSubject.Anime, id, "ranking", TransferImportFailureKind.Ranking, "This anime could not be fetched from MyAnimeList."));
        }
    }

    // 7.7: titles are resolved once, when the run ends, so a title known "by
    // then" is used even for an anime this same run just fetched.
    private static async Task<TransferImportReport> BuildReportAsync(
        AnimeTrackerDbContext applyDb, PreparedImport prepared, List<int> rankingAdded, List<int> rankingRemoved,
        List<DraftFailure> failures, CancellationToken ct)
    {
        var animeIdsNeeded = rankingAdded
            .Concat(rankingRemoved)
            .Concat(prepared.FetchedAnimeIds)
            .Concat(failures.Where(f => f.Subject == TransferImportFailureSubject.Anime).Select(f => f.Id))
            .Distinct()
            .ToList();
        var animeTitles = await applyDb.AnimeMetadata.AsNoTracking()
            .Where(a => animeIdsNeeded.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => (a.Title, a.EnglishTitle), ct);

        var seriesTitles = new Dictionary<int, (string? Title, string? EnglishTitle)>();
        foreach (var fileSeriesId in failures.Where(f => f.Subject == TransferImportFailureSubject.Series).Select(f => f.Id).Distinct())
        {
            var localId = prepared.SeriesResolutions.TryGetValue(fileSeriesId, out var resolution) ? resolution.LocalSeriesId : null;
            seriesTitles[fileSeriesId] = await ResolveSeriesTitleAsync(applyDb, fileSeriesId, localId, ct);
        }

        TransferReportAnime AnimeLine(int id)
        {
            var (title, englishTitle) = animeTitles[id];
            return new TransferReportAnime(id, title, englishTitle);
        }

        return new TransferImportReport(
            rankingAdded.Select(AnimeLine).ToList(),
            rankingRemoved.Select(AnimeLine).ToList(),
            prepared.FetchedAnimeIds.Select(AnimeLine).ToList(),
            failures.Select(f =>
            {
                var (title, englishTitle) = f.Subject == TransferImportFailureSubject.Anime
                    ? animeTitles.GetValueOrDefault(f.Id)
                    : seriesTitles.GetValueOrDefault(f.Id);
                return new TransferImportFailure(f.Subject, f.Id, title, englishTitle, f.What, f.Kind, f.Reason);
            }).ToList());
    }

    // A series' displayed title/English title through SeriesIdentity when it
    // resolved, otherwise its root anime's own title (design.md D13) — the
    // AnimeMetadata row named by the file's own series id, which is that
    // series' root's MAL id whether or not this device ever built the series.
    private static async Task<(string? Title, string? EnglishTitle)> ResolveSeriesTitleAsync(
        AnimeTrackerDbContext applyDb, int fileSeriesId, int? localSeriesId, CancellationToken ct)
    {
        if (localSeriesId is { } resolvedId)
        {
            var series = await applyDb.Series.AsNoTracking().FirstOrDefaultAsync(s => s.Id == resolvedId, ct);
            var root = await applyDb.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == resolvedId, ct);
            if (series is not null && root is not null)
            {
                var (title, englishTitle, _) = SeriesIdentity.Resolve(
                    series.SelectedTitle, series.SelectedPictureUrl, root.Title, root.EnglishTitle, root.MalPictureUrl);
                return (title, englishTitle);
            }
        }

        var rootAnime = await applyDb.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == fileSeriesId, ct);
        return rootAnime is not null ? (rootAnime.Title, rootAnime.EnglishTitle) : (null, null);
    }
}
