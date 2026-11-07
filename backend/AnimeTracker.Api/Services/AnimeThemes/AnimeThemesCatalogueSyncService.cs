using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Search;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>The weekly catalogue sync (spec `anime-themes`,
/// add-anime-themes-and-guess-the-theme design.md D3). It follows
/// <c>AnimeIdMappingSyncService</c>: the whole new catalogue is read and built in
/// memory before the database is touched, then applied as a diff (added, changed
/// and vanished rows) in a single save that also stamps the refresh as done, so a
/// reader sees the old catalogue or the new one and never a mix.
/// <para>What differs is the pace. AnimeThemes answers a page in 5 to 20 seconds
/// and allows 90 requests a minute, and the read is about 51 pages, so it goes one
/// request at a time with a gap of at least two seconds after each. It can never
/// pass 30 requests a minute, and in practice runs at about 8. It calls AnimeThemes
/// and nothing else, and never a video or audio file.</para></summary>
public class AnimeThemesCatalogueSyncService(
    AnimeTrackerDbContext db,
    IAnimeThemesClient client,
    IThemeSearchIndex themeSearchIndex,
    ILogger<AnimeThemesCatalogueSyncService> logger) : IAnimeThemesCatalogueSyncService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromDays(7);

    /// <summary>What a catalogue made by this version holds. Bump it in the version that
    /// begins to keep one more thing from AnimeThemes: a catalogue stored with an older
    /// number is refreshed at the first chance, rather than left without the new value
    /// for up to a week (improve-search-page design.md D9). 1: the anime's name.</summary>
    public const int CurrentCatalogueFormat = 1;

    // Without this, a persistent change on AnimeThemes' side would start another
    // seven-minute read on every hourly tick.
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromHours(6);

    /// <summary>The wait after one request ends and before the next begins.</summary>
    public static readonly TimeSpan PageGap = TimeSpan.FromSeconds(2);

    /// <summary>The most pages a refresh reads, whatever AnimeThemes says about
    /// further ones. The whole catalogue is about 51, so this is four times what
    /// it needs, and a bad answer can't loop forever.</summary>
    public const int MaxPages = 200;

    /// <summary>The wait between two pages. Virtual so a test can observe it
    /// instead of sitting through it.</summary>
    protected virtual Task DelayAsync(TimeSpan wait, CancellationToken ct) => Task.Delay(wait, ct);

    public async Task<AnimeThemesCatalogueSyncOutcome> SyncIfDueAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var attempted = false;
        try
        {
            var state = await GetOrCreateStateAsync(ct);
            if (!IsDue(state, now))
                return AnimeThemesCatalogueSyncOutcome.NotDue;

            attempted = true;
            var incoming = AnimeThemesCatalogueReader.Read(await ReadPagesAsync(ct));

            // A read that is cut short, or an answer that parses into far too
            // little, must not wipe the catalogue. The comparison needs stored rows,
            // so the very first refresh is never held back by it. An empty read is
            // never taken, first or not: AnimeThemes has no empty catalogue, and
            // taking one would show "no openings or endings" everywhere for a week.
            var storedAnime = await db.AnimeThemes.Select(t => t.AnimeId).Distinct().CountAsync(ct);
            var incomingAnime = incoming.Select(t => t.AnimeId).Distinct().Count();
            if (incomingAnime == 0 || (storedAnime > 0 && incomingAnime * 2 < storedAnime))
            {
                logger.LogWarning(
                    "AnimeThemes catalogue sync abandoned: it found {Incoming} anime with openings or endings, against {Stored} stored. Keeping the stored catalogue.",
                    incomingAnime, storedAnime);
                await RecordFailedAttemptAsync(now, ct);
                return AnimeThemesCatalogueSyncOutcome.Failed;
            }

            var (added, updated, removed) = await ApplyAsync(incoming, ct);

            state.LastSyncedAt = now;
            state.LastAttemptAt = now;
            state.CatalogueFormat = CurrentCatalogueFormat;
            await db.SaveChangesAsync(ct);

            // Only now, with the new catalogue committed: until this save lands, the rows the
            // search page holds in memory are the catalogue as it is. Never before the save, and
            // never on the failed or abandoned paths, which leave the stored catalogue as it was
            // (improve-search-page design.md D6).
            themeSearchIndex.Invalidate();

            logger.LogInformation(
                "AnimeThemes catalogue synced: {Rows} openings and endings of {Anime} anime ({Added} added, {Updated} changed, {Removed} removed).",
                incoming.Count, incomingAnime, added, updated, removed);
            return AnimeThemesCatalogueSyncOutcome.Synced;
        }
        // An HttpClient timeout also surfaces as an OperationCanceledException,
        // but with this token still live, so only a caller's own cancellation is
        // let through; a timeout is a failed request like any other.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AnimeThemes catalogue sync failed. Keeping the stored catalogue.");
            if (attempted)
                await RecordFailedAttemptAsync(now, ct);
            return AnimeThemesCatalogueSyncOutcome.Failed;
        }
    }

    private async Task<AnimeThemesSyncState> GetOrCreateStateAsync(CancellationToken ct)
    {
        var state = await db.AnimeThemesSyncStates.FirstOrDefaultAsync(ct);
        if (state is null)
        {
            state = new AnimeThemesSyncState();
            db.AnimeThemesSyncStates.Add(state);
            await db.SaveChangesAsync(ct);
        }

        return state;
    }

    private static bool IsDue(AnimeThemesSyncState state, DateTimeOffset now)
    {
        // The week only counts for a catalogue made the way this version makes
        // them. One stored without something the app now keeps is due at once, but
        // not LastSyncedAt's problem: that stays as it was until a refresh succeeds,
        // so the pages keep listing the stored catalogue while it runs (design.md D9).
        if (state.CatalogueFormat == CurrentCatalogueFormat
            && state.LastSyncedAt is { } syncedAt && now - syncedAt <= SyncInterval)
            return false;

        // A failed attempt is one made after the last success, or, before any
        // success, any attempt at all. It backs off a catch-up refresh as well.
        if (state.LastAttemptAt is { } attemptedAt
            && (state.LastSyncedAt is not { } lastSyncedAt || attemptedAt > lastSyncedAt)
            && now - attemptedAt < RetryBackoff)
            return false;

        return true;
    }

    /// <summary>Every page, one request at a time, the gap kept between the end
    /// of one and the start of the next. Any failure ends the read and so the
    /// refresh. At <see cref="MaxPages"/> it stops whatever AnimeThemes says about
    /// more, and what was read is used.</summary>
    private async Task<List<AnimeThemesPage>> ReadPagesAsync(CancellationToken ct)
    {
        var pages = new List<AnimeThemesPage>();
        for (var page = 1; page <= MaxPages; page++)
        {
            if (page > 1)
                await DelayAsync(PageGap, ct);

            var read = await client.GetPageAsync(page, ct);
            pages.Add(read);
            if (!read.HasMorePages)
                return pages;
        }

        logger.LogWarning(
            "AnimeThemes still reported more pages after {Max}; stopped reading there.", MaxPages);
        return pages;
    }

    /// <summary>Stages the diff on the context; the caller's one
    /// <c>SaveChangesAsync</c> writes it. A value that is unchanged is left alone,
    /// and an array is reassigned only when its sequence differs, so a catalogue
    /// that hasn't changed writes no row.</summary>
    private async Task<(int Added, int Updated, int Removed)> ApplyAsync(List<AnimeTheme> incoming, CancellationToken ct)
    {
        var stored = await db.AnimeThemes.ToDictionaryAsync(t => (t.AnimeId, t.ThemeId), ct);
        int added = 0, updated = 0;

        foreach (var theme in incoming)
        {
            if (!stored.Remove((theme.AnimeId, theme.ThemeId), out var row))
            {
                db.AnimeThemes.Add(theme);
                added++;
            }
            else if (CopyChanges(row, theme))
            {
                updated++;
            }
        }

        // What is left in the dictionary is stored and no longer listed.
        db.AnimeThemes.RemoveRange(stored.Values);
        return (added, updated, stored.Count);
    }

    private static bool CopyChanges(AnimeTheme row, AnimeTheme incoming)
    {
        var changed = false;

        if (row.Kind != incoming.Kind)
        {
            row.Kind = incoming.Kind;
            changed = true;
        }

        if (row.Sequence != incoming.Sequence)
        {
            row.Sequence = incoming.Sequence;
            changed = true;
        }

        if (row.Label != incoming.Label)
        {
            row.Label = incoming.Label;
            changed = true;
        }

        if (row.SongId != incoming.SongId)
        {
            row.SongId = incoming.SongId;
            changed = true;
        }

        if (row.SongTitle != incoming.SongTitle)
        {
            row.SongTitle = incoming.SongTitle;
            changed = true;
        }

        if (row.AnimeName != incoming.AnimeName)
        {
            row.AnimeName = incoming.AnimeName;
            changed = true;
        }

        if (!row.Artists.SequenceEqual(incoming.Artists))
        {
            row.Artists = incoming.Artists;
            changed = true;
        }

        if (row.Episodes != incoming.Episodes)
        {
            row.Episodes = incoming.Episodes;
            changed = true;
        }

        if (row.Nsfw != incoming.Nsfw)
        {
            row.Nsfw = incoming.Nsfw;
            changed = true;
        }

        if (row.Spoiler != incoming.Spoiler)
        {
            row.Spoiler = incoming.Spoiler;
            changed = true;
        }

        if (row.Clean != incoming.Clean)
        {
            row.Clean = incoming.Clean;
            changed = true;
        }

        if (row.VideoUrl != incoming.VideoUrl)
        {
            row.VideoUrl = incoming.VideoUrl;
            changed = true;
        }

        if (row.AudioUrl != incoming.AudioUrl)
        {
            row.AudioUrl = incoming.AudioUrl;
            changed = true;
        }

        return changed;
    }

    /// <summary>Stamps <c>LastAttemptAt</c> alone, leaving <c>LastSyncedAt</c> and
    /// the catalogue as they were. Never throws except on cancellation: a failure to
    /// record is logged, and the refresh it belongs to still reports
    /// <see cref="AnimeThemesCatalogueSyncOutcome.Failed"/>.</summary>
    private async Task RecordFailedAttemptAsync(DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            // Whatever the failed attempt left staged (a half-built diff, or the
            // success stamp that was never saved) must not ride along with this
            // write.
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
            logger.LogError(ex, "AnimeThemes catalogue sync could not record its failed attempt.");
        }
    }
}
