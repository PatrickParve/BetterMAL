using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Games;

// What the Higher or Lower fixtures share (add-games-higher-or-lower section 2-4):
// an InMemory context, an eligible anime to seed, and the settings every test
// starts from.
internal static class HigherOrLowerTestKit
{
    public static readonly HigherOrLowerSettings AnyAnime = new(null, null, null, HigherOrLowerListScope.All, false);

    /// <summary>The instant the pool tests ask "has it aired by now?" at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    public static AnimeTrackerDbContext CreateDb() => CreateDb(Guid.NewGuid().ToString());

    /// <summary>A context on a named InMemory database, so a second one over the
    /// same name sees the same rows, as a second request would.</summary>
    public static AnimeTrackerDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(name).Options);

    /// <summary>An eligible anime unless a parameter says otherwise: a picture, a
    /// score, a rank and a type, and no airing status, as a listing-only row has
    /// none. Pass <c>score: null</c> or <c>picture: null</c> for the two things that
    /// make one ineligible, or <c>airingStatus: "not_yet_aired"</c> for a third.</summary>
    public static AnimeMetadata Anime(
        int id,
        double? score = 7.0,
        string? picture = "",
        string? mediaType = "tv",
        DateOnly? airedFrom = null,
        string? rating = "pg_13",
        int? rank = 100,
        string? airingStatus = null)
    {
        return new AnimeMetadata
        {
            Id = id,
            Title = $"Anime {id}",
            EnglishTitle = $"English {id}",
            MalScore = score,
            PictureUrl = picture == "" ? $"https://img/{id}.jpg" : picture,
            MediaType = mediaType,
            AiredFrom = airedFrom,
            Rating = rating,
            PopularityRank = rank,
            AiringStatus = airingStatus,
        };
    }

    public static UserAnimeEntry OnList(int animeId, WatchStatus status = WatchStatus.PlanToWatch) =>
        new() { AnimeId = animeId, Status = status };

    /// <summary>Anime 1..count with distinct scores 5.01, 5.02 and so on, every one
    /// eligible. Scores are the ids' hundredths, so who scores higher is easy to
    /// read off the ids.</summary>
    public static async Task SeedAsync(AnimeTrackerDbContext db, int count)
    {
        for (var id = 1; id <= count; id++)
            db.AnimeMetadata.Add(Anime(id, score: Math.Round(5 + id / 100.0, 2)));
        await db.SaveChangesAsync();
    }

    /// <summary>A game in progress, written straight to the table so a test can put
    /// the game wherever it needs without playing there. The settings default to
    /// <see cref="AnyAnime"/>; the shown list defaults to the two on the table.</summary>
    public static async Task<HigherOrLowerRun> AddRunAsync(
        AnimeTrackerDbContext db, int left, int right, int round = 1, int streak = 0, int leftStays = 0,
        int[]? shown = null, HigherOrLowerSettings? settings = null)
    {
        var s = settings ?? AnyAnime;
        var run = new HigherOrLowerRun
        {
            Round = round, Streak = streak, LeftAnimeId = left, RightAnimeId = right, LeftStays = leftStays,
            ShownAnimeIds = [.. shown ?? [left, right]],
            YearFrom = s.YearFrom, YearTo = s.YearTo, MediaTypes = s.MediaTypes, ListScope = s.List, HideHentai = s.HideHentai,
        };
        db.HigherOrLowerRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }
}
