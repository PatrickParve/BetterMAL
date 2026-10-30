using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.ScoreHistory;

public class ScoreHistoryService(
    AnimeTrackerDbContext db,
    IBroadcastLocalTimeConverter localTime,
    IEpisodeScheduleService scheduleService) : IScoreHistoryService
{
    public async Task<ScoreHistoryDto> GetAnimeHistoryAsync(int animeId, CancellationToken ct = default) =>
        new(await AnimePointsAsync(animeId, ct), []);

    public async Task<ScoreHistoryDto> GetSeriesHistoryAsync(int seriesId, CancellationToken ct = default)
    {
        if (!await db.Series.AnyAsync(s => s.Id == seriesId, ct))
            throw new SeriesIdNotFoundException(seriesId);

        return await BuildSeriesAsync(seriesId, withMarkers: true, ct);
    }

    public async Task<bool> AnimeHasChangedAsync(int animeId, CancellationToken ct = default) =>
        ScoreHistoryBuilder.HasChanged(await AnimePointsAsync(animeId, ct));

    // Markers only repeat a score the line already holds on that date, so they
    // can never add a different one: leaving them out answers the same question
    // without the aired-episode read they need.
    public async Task<bool> SeriesHasChangedAsync(int seriesId, CancellationToken ct = default) =>
        ScoreHistoryBuilder.HasChanged((await BuildSeriesAsync(seriesId, withMarkers: false, ct)).Points);

    private async Task<List<ScoreHistoryPointDto>> AnimePointsAsync(int animeId, CancellationToken ct)
    {
        var rows = await db.AnimeScoreRecords.AsNoTracking()
            .Where(r => r.AnimeId == animeId)
            .OrderBy(r => r.DetectedAt)
            .Select(r => new { r.DetectedAt, r.Score })
            .ToListAsync(ct);

        return ScoreHistoryBuilder.BuildPoints(rows.Select(r => new ScoreRecordRow(r.DetectedAt, r.Score)), localTime.GetLocalDate);
    }

    /// <summary>The line over the series' main-line members, every alternative
    /// of every version slot included, as <see cref="SeriesAverages.Mal"/>
    /// counts them: the figure is deliberately unaffected by the version
    /// picker. Read from the current main line each time, never stored.</summary>
    private async Task<ScoreHistoryDto> BuildSeriesAsync(int seriesId, bool withMarkers, CancellationToken ct)
    {
        var members = await db.SeriesMembers.AsNoTracking()
            .Where(m => m.SeriesId == seriesId && m.IsMainLine)
            .OrderBy(m => m.Order)
            .Select(m => new
            {
                m.AnimeId,
                m.Anime.Title,
                m.Anime.EnglishTitle,
                m.Anime.PictureUrl,
                m.Anime.AiringStatus,
                m.Anime.AiredFrom,
            })
            .ToListAsync(ct);
        if (members.Count == 0)
            return new ScoreHistoryDto([], []);

        var animeIds = members.Select(m => m.AnimeId).ToList();
        var records = await db.AnimeScoreRecords.AsNoTracking()
            .Where(r => animeIds.Contains(r.AnimeId))
            .OrderBy(r => r.DetectedAt)
            .Select(r => new { r.AnimeId, r.DetectedAt, r.Score })
            .ToListAsync(ct);
        var recordsByAnime = records.ToLookup(r => r.AnimeId);

        // One batched read for the whole main line (design.md D11), and only
        // when markers are wanted: a marker needs its entry's first episode to
        // be known to have aired.
        var airedCounts = withMarkers
            ? await scheduleService.EpisodesAiredAsOfAsync(animeIds, DateTimeOffset.UtcNow, ct)
            : [];

        var entries = members
            .Select(m => new ScoreHistoryEntry(
                m.AnimeId,
                m.Title,
                m.EnglishTitle,
                m.PictureUrl,
                m.AiredFrom,
                withMarkers && AiredEpisodeGate.HasConfirmedAiring(m.AiringStatus, airedCounts.TryGetValue(m.AnimeId, out var aired) ? aired : null),
                ScoreHistoryBuilder.BuildPoints(
                    recordsByAnime[m.AnimeId].Select(r => new ScoreRecordRow(r.DetectedAt, r.Score)), localTime.GetLocalDate)))
            .ToList();

        return ScoreHistoryBuilder.BuildSeries(entries);
    }
}
