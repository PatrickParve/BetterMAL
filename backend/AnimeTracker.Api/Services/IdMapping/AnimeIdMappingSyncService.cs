using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>The weekly mapping sync (spec `external-id-mapping`, design.md
/// D2-D3). The whole new mapping is parsed in memory before the database is
/// touched, then applied as a diff (added, changed and vanished MAL ids) in a
/// single save that also stamps the sync as done, so a reader sees the old
/// mapping or the new one and never a mix. Makes exactly one request, for the
/// mapping file itself; it never calls TMDB or fetches an image. It replaces
/// the synced mapping only: the custom mapping (design.md D19) is a separate
/// file that is merged on read, so nothing here can remove or change an entry
/// of it. The one thing the sync does with it is say, after each refresh, which
/// entries the source has made unnecessary.</summary>
public class AnimeIdMappingSyncService(
    AnimeTrackerDbContext db,
    IHttpClientFactory httpClientFactory,
    ICustomIdMappings customMappings,
    ILogger<AnimeIdMappingSyncService> logger) : IAnimeIdMappingSyncService
{
    /// <summary>The named client's registration (Program.cs) gives it a
    /// 2-minute timeout for the 5.8 MB download.</summary>
    public const string HttpClientName = "anime-id-mapping";

    public const string MappingUrl = "https://raw.githubusercontent.com/Fribb/anime-lists/master/anime-list-mini.json";

    private static readonly TimeSpan SyncInterval = TimeSpan.FromDays(7);

    // Without this, a persistent upstream format change would re-download the
    // file on every hourly tick: about 140 MB a day.
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromHours(6);

    public async Task<AnimeIdMappingSyncOutcome> SyncIfDueAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var attempted = false;
        try
        {
            var state = await GetOrCreateStateAsync(ct);
            if (!IsDue(state, now))
                return AnimeIdMappingSyncOutcome.NotDue;

            attempted = true;
            var incoming = await DownloadAsync(ct);

            // A truncated or garbled download can still parse, into far too
            // few entries. The guard needs stored rows to compare against, so
            // the very first sync is never held back by it.
            var storedCount = await db.AnimeIdMappings.CountAsync(ct);
            if (storedCount > 0 && incoming.Count * 2 < storedCount)
            {
                logger.LogWarning(
                    "ID mapping sync abandoned: the file maps {Incoming} MAL ids, under half of the {Stored} stored. Keeping the stored mapping.",
                    incoming.Count, storedCount);
                await RecordFailedAttemptAsync(now, ct);
                return AnimeIdMappingSyncOutcome.Failed;
            }

            var (added, updated, removed) = await ApplyAsync(incoming, ct);

            state.LastSyncedAt = now;
            state.LastAttemptAt = now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "ID mapping synced: {Total} MAL ids mapped ({Added} added, {Updated} changed, {Removed} removed).",
                incoming.Count, added, updated, removed);
            LogUnnecessaryCustomEntries(incoming);
            return AnimeIdMappingSyncOutcome.Synced;
        }
        // An HttpClient timeout also surfaces as an OperationCanceledException,
        // but with this token still live, so only a caller's own cancellation
        // is let through; a timeout is a failed download like any other.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ID mapping sync failed. Keeping the stored mapping.");
            if (attempted)
                await RecordFailedAttemptAsync(now, ct);
            return AnimeIdMappingSyncOutcome.Failed;
        }
    }

    /// <summary>The custom file is meant to shrink: once the source holds what an
    /// entry supplies, or agrees with an override, the entry does nothing and can
    /// be deleted. Said here because this is when new source data has just
    /// arrived. Only informational, so a failure to say it is swallowed: the
    /// refresh it follows has already been saved.</summary>
    private void LogUnnecessaryCustomEntries(Dictionary<int, AnimeIdMapping> incoming)
    {
        try
        {
            foreach (var (animeId, entry) in customMappings.Current)
            {
                incoming.TryGetValue(animeId, out var synced);
                if (!AnimeIdMappingMerge.IsUnnecessary(synced, entry))
                    continue;

                logger.LogInformation(
                    "Custom id mapping for MAL id {AnimeId}{Note} is no longer needed: the synced mapping already holds these values. You can remove it from {Path}.",
                    animeId, entry.Note is null ? "" : $" ({entry.Note})", customMappings.FilePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check the custom id mapping against the refreshed mapping.");
        }
    }

    private async Task<AnimeIdMappingSyncState> GetOrCreateStateAsync(CancellationToken ct)
    {
        var state = await db.AnimeIdMappingSyncStates.FirstOrDefaultAsync(ct);
        if (state is null)
        {
            state = new AnimeIdMappingSyncState();
            db.AnimeIdMappingSyncStates.Add(state);
            await db.SaveChangesAsync(ct);
        }

        return state;
    }

    private static bool IsDue(AnimeIdMappingSyncState state, DateTimeOffset now)
    {
        if (state.LastSyncedAt is { } syncedAt && now - syncedAt <= SyncInterval)
            return false;

        // A failed attempt is one made after the last success, or, before any
        // success, any attempt at all.
        if (state.LastAttemptAt is { } attemptedAt
            && (state.LastSyncedAt is not { } lastSyncedAt || attemptedAt > lastSyncedAt)
            && now - attemptedAt < RetryBackoff)
            return false;

        return true;
    }

    private async Task<Dictionary<int, AnimeIdMapping>> DownloadAsync(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(MappingUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await AnimeIdMappingParser.ParseAsync(stream, ct);
    }

    /// <summary>Stages the diff on the context; the caller's one
    /// <c>SaveChangesAsync</c> writes it. A value that is unchanged is left
    /// alone, and an array is reassigned only when its sequence differs.</summary>
    private async Task<(int Added, int Updated, int Removed)> ApplyAsync(
        Dictionary<int, AnimeIdMapping> incoming, CancellationToken ct)
    {
        var stored = await db.AnimeIdMappings.ToDictionaryAsync(m => m.AnimeId, ct);
        int added = 0, updated = 0, removed = 0;

        foreach (var (animeId, mapping) in incoming)
        {
            if (!stored.TryGetValue(animeId, out var row))
            {
                db.AnimeIdMappings.Add(mapping);
                added++;
            }
            else if (CopyChanges(row, mapping))
            {
                updated++;
            }
        }

        foreach (var (animeId, row) in stored)
        {
            if (!incoming.ContainsKey(animeId))
            {
                db.AnimeIdMappings.Remove(row);
                removed++;
            }
        }

        return (added, updated, removed);
    }

    private static bool CopyChanges(AnimeIdMapping row, AnimeIdMapping incoming)
    {
        var changed = false;

        if (row.TmdbTvId != incoming.TmdbTvId)
        {
            row.TmdbTvId = incoming.TmdbTvId;
            changed = true;
        }

        if (row.TmdbSeasonNumber != incoming.TmdbSeasonNumber)
        {
            row.TmdbSeasonNumber = incoming.TmdbSeasonNumber;
            changed = true;
        }

        if (!row.TmdbMovieIds.SequenceEqual(incoming.TmdbMovieIds))
        {
            row.TmdbMovieIds = incoming.TmdbMovieIds;
            changed = true;
        }

        if (!row.ImdbIds.SequenceEqual(incoming.ImdbIds))
        {
            row.ImdbIds = incoming.ImdbIds;
            changed = true;
        }

        return changed;
    }

    /// <summary>Stamps <c>LastAttemptAt</c> alone, leaving <c>LastSyncedAt</c>
    /// and the mapping as they were. Never throws except on cancellation: a
    /// failure to record is logged, and the sync it belongs to still reports
    /// <see cref="AnimeIdMappingSyncOutcome.Failed"/>.</summary>
    private async Task RecordFailedAttemptAsync(DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            // Whatever the failed attempt left staged (a half-built diff, or
            // the success stamp that was never saved) must not ride along
            // with this write.
            db.ChangeTracker.Clear();

            var state = await GetOrCreateStateAsync(ct);
            state.LastAttemptAt = now;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ID mapping sync could not record its failed attempt.");
        }
    }
}
