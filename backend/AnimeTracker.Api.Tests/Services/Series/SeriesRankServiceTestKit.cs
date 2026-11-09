using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The series rank reader every fixture that constructs a SeriesService needs
// (add-my-rank-badges task 2.2): the real one over the same context, reading the
// real entries, stored order and airing rows, so a fixture that scores nothing
// sees every series read report no rank and one that does is ranked as the app
// would rank it. Real rather than a fake because the rank read reaches the
// batched aired-episode overload, which the test classes' own schedule fakes
// leave unimplemented.
internal static class SeriesRankServiceTestKit
{
    public static SeriesRankService Create(AnimeTrackerDbContext db) =>
        new(
            new SeriesRankingLookup(db),
            new EpisodeScheduleService(new EpisodeAiringRepository(db), new BroadcastLocalTimeConverter()),
            new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)));
}
