using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.ScoreHistory;

public class AnimeScoreRecorder(AnimeTrackerDbContext db, IEpisodeScheduleService scheduleService) : IAnimeScoreRecorder
{
    public async Task RecordAsync(AnimeMetadata anime, DateTimeOffset now, CancellationToken ct = default)
    {
        // The checks run cheapest first (design.md D3), so a listing write of
        // a stranger costs no query at all and a refresh that moved nothing
        // costs one. No status means nothing is known to have aired, and the
        // aired-count read below is only worth making for a score that
        // actually differs.
        if (anime.MalScore is not { } score || anime.AiringStatus is null)
            return;

        // Compared with the latest record rather than with a before-snapshot
        // (design.md D2): the first record has nothing to differ from, a
        // score that moves while the gate is closed would otherwise be
        // absorbed by the write that opens it, and the insert branches and
        // setup have no snapshot at all.
        //
        // Pending records come first. A season listing's edges can name one
        // anime twice inside one unit of work, and two rows for one instant
        // would break the unique index and fail the whole save. Stored
        // records are read untracked, so Local only ever holds what this unit
        // of work added.
        var pending = db.AnimeScoreRecords.Local
            .Where(r => r.AnimeId == anime.Id)
            .MaxBy(r => r.DetectedAt);
        var latestScore = pending?.Score ?? await StoredLatestScoreAsync(anime.Id, ct);
        if (latestScore == score)
            return;

        var episodesAired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        if (!AiredEpisodeGate.HasConfirmedAiring(anime, episodesAired))
            return;

        // The same unit of work already recorded this anime at this very
        // instant, at another score: the later write replaces it rather than
        // adding a second row for one instant.
        if (pending is not null && pending.DetectedAt == now)
        {
            pending.Score = score;
            return;
        }

        db.AnimeScoreRecords.Add(new AnimeScoreRecord { AnimeId = anime.Id, Score = score, DetectedAt = now });
    }

    private async Task<double?> StoredLatestScoreAsync(int animeId, CancellationToken ct) =>
        await db.AnimeScoreRecords.AsNoTracking()
            .Where(r => r.AnimeId == animeId)
            .OrderByDescending(r => r.DetectedAt)
            .Select(r => (double?)r.Score)
            .FirstOrDefaultAsync(ct);
}
