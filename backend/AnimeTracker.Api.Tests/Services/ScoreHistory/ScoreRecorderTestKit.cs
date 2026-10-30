using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.ScoreHistory;

namespace AnimeTracker.Api.Tests.Services.ScoreHistory;

// The recorder every fixture that constructs a score-writing service needs
// (add-score-history-graph tasks 2.3/2.4): the real one over the same context,
// reading the real airing rows, so a fixture that seeds neither a score nor a
// status sees it do nothing at all.
internal static class ScoreRecorderTestKit
{
    public static AnimeScoreRecorder Create(AnimeTrackerDbContext db) =>
        new(db, new EpisodeScheduleService(new EpisodeAiringRepository(db), new BroadcastLocalTimeConverter()));
}

// The read service every fixture that constructs a page-read service needs
// (task 4.4): the real one over the same context, so a fixture that records no
// score sees every page report no history.
internal static class ScoreHistoryServiceTestKit
{
    public static ScoreHistoryService Create(AnimeTrackerDbContext db)
    {
        var localTime = new BroadcastLocalTimeConverter();
        return new ScoreHistoryService(db, localTime, new EpisodeScheduleService(new EpisodeAiringRepository(db), localTime));
    }
}
