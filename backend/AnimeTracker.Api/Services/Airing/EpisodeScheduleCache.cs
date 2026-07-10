using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Airing;

public class EpisodeScheduleCache : IEpisodeScheduleCache
{
    private readonly ConcurrentDictionary<int, IReadOnlyList<EpisodeAiring>> _schedules = new();

    public bool TryGet(int animeId, out IReadOnlyList<EpisodeAiring> schedule) =>
        _schedules.TryGetValue(animeId, out schedule!);

    public void Set(int animeId, IReadOnlyList<EpisodeAiring> schedule) =>
        _schedules[animeId] = schedule;
}
