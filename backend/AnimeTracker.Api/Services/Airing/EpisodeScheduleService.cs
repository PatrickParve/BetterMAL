using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Thin reader over <see cref="IEpisodeAiringRepository"/>'s stored
/// AniList rows. No estimation, no fallback: a value this can't read from a
/// stored row comes back null, meaning "unknown" to every caller.</summary>
public class EpisodeScheduleService(
    IEpisodeAiringRepository repository,
    IBroadcastLocalTimeConverter converter) : IEpisodeScheduleService
{
    public async Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default)
    {
        var dayStartUtc = converter.LocalMidnightUtc(localDate);
        var dayEndUtc = converter.LocalMidnightUtc(localDate.AddDays(1));
        var rows = await repository.GetRowsInRangeAsync([anime.Id], dayStartUtc, dayEndUtc, ct);
        if (rows.Count == 0)
            return null;

        var row = rows[0];
        return new ResolvedEpisode(converter.GetLocalTime(row.AirsAtUtc), row.Episode);
    }

    public async Task<Dictionary<int, ResolvedEpisode>> ResolveOnLocalDateAsync(IReadOnlyCollection<AnimeMetadata> anime, DateOnly localDate, CancellationToken ct = default)
    {
        var animeIds = anime.Select(a => a.Id).ToList();
        var dayStartUtc = converter.LocalMidnightUtc(localDate);
        var dayEndUtc = converter.LocalMidnightUtc(localDate.AddDays(1));
        var rows = await repository.GetRowsInRangeAsync(animeIds, dayStartUtc, dayEndUtc, ct);

        // Rows come back ordered by AirsAtUtc (GetRowsInRangeAsync's contract),
        // so the first row seen per anime is the same earliest row the
        // per-anime read takes.
        var result = new Dictionary<int, ResolvedEpisode>();
        foreach (var row in rows)
            if (!result.ContainsKey(row.AnimeId))
                result[row.AnimeId] = new ResolvedEpisode(converter.GetLocalTime(row.AirsAtUtc), row.Episode);
        return result;
    }

    public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
        repository.GetNextAiringInstantAsync(anime.Id, afterUtc, ct);

    public Task<Dictionary<int, DateTimeOffset>> NextAiringInstantAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
        repository.GetNextAiringInstantsAsync(anime.Select(a => a.Id).ToList(), afterUtc, ct);

    public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
        repository.GetMaxAiredEpisodeAsync(anime.Id, nowUtc, ct);

    public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
        EpisodesAiredAsOfAsync(anime.Select(a => a.Id).ToList(), nowUtc, ct);

    public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
        repository.GetMaxAiredEpisodesAsync(animeIds, nowUtc, ct);
}
