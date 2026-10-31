using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// What the Guess the Anime fixtures share (add-guess-the-anime sections 3-5): an
// anime that holds every stat, so a test breaks exactly the one it is about.
// The database, the clock and the plain eligible anime are HigherOrLowerTestKit's.
internal static class GuessTheAnimeTestKit
{
    /// <summary>An eligible anime that holds every stat a guess is compared on,
    /// and has had a full-detail fetch. A parameter overrides one stat; pass
    /// <c>null</c> where one allows it to take that stat away.</summary>
    public static AnimeMetadata Whole(
        int id,
        double? score = 7.0,
        string? mediaType = "tv",
        DateOnly? airedFrom = null,
        int? episodes = 12,
        List<string>? genres = null,
        List<string>? studios = null,
        int? rank = 100,
        string? rating = "pg_13")
    {
        var anime = Anime(id, score: score, mediaType: mediaType, airedFrom: airedFrom, rating: rating, rank: rank);
        anime.TotalEpisodes = episodes;
        anime.Genres = genres ?? ["Action"];
        anime.Studios = studios ?? ["Madhouse"];
        anime.LastSyncedAt = Now;
        return anime;
    }

    /// <summary>The same, with a start date so a year, a season and a pool
    /// setting can see it.</summary>
    public static AnimeMetadata WholeAired(int id, DateOnly? airedFrom = null) =>
        Whole(id, airedFrom: airedFrom ?? new DateOnly(2013, 4, 7));

    /// <summary>An eligible anime that has never had a full-detail fetch, as most
    /// stored rows are: a picture and a score, and no genres, studios, episode
    /// count or start date.</summary>
    public static AnimeMetadata Lean(int id, int? rank = 100, string? rating = null) =>
        Anime(id, rank: rank, rating: rating);

    /// <summary>A game in progress, written straight to the table so a test can put
    /// the game wherever it needs without playing there. The settings default to
    /// <see cref="HigherOrLowerTestKit.AnyAnime"/>.</summary>
    public static async Task<GuessTheAnimeRun> AddGuessRunAsync(
        AnimeTrackerDbContext db, int answer, int[]? guessed = null, GameSettings? settings = null)
    {
        var s = settings ?? AnyAnime;
        var ids = guessed ?? [];
        var run = new GuessTheAnimeRun
        {
            AnswerAnimeId = answer,
            GuessedAnimeIds = [.. ids],
            GuessCount = ids.Length,
            YearFrom = s.YearFrom, YearTo = s.YearTo, MediaTypes = s.MediaTypes, ListScope = s.List, HideHentai = s.HideHentai,
        };
        db.GuessTheAnimeRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    /// <summary>Stands in for MyAnimeList behind <see cref="IGameAnimeDetails"/>:
    /// the same test as the real one (an anime that isn't stored, or is missing a
    /// stat, is fetched), and a fetch leaves the anime holding every stat unless it
    /// is told to fail. <see cref="Fills"/> changes what a particular anime comes
    /// back as, such as rated <c>rx</c>.</summary>
    public sealed class FakeGameDetails(AnimeTrackerDbContext db) : IGameAnimeDetails
    {
        public List<int> Calls { get; } = [];
        public Dictionary<int, Exception> Failures { get; } = [];
        public Dictionary<int, Action<AnimeMetadata>> Fills { get; } = [];

        /// <summary>Runs inside a fetch, before it fails or fills, such as another
        /// tab's guess landing while this one waits.</summary>
        public Func<int, Task>? OnFetch { get; set; }

        public async Task<bool> EnsureFetchedAsync(int animeId, CancellationToken ct = default)
        {
            var stored = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct);
            if (stored is not null && !AnimeStats.IsMissingAny(stored))
                return false;

            Calls.Add(animeId);
            if (OnFetch is not null)
                await OnFetch(animeId);
            if (Failures.TryGetValue(animeId, out var failure))
                throw failure;

            var filled = Whole(animeId, airedFrom: new DateOnly(2013, 4, 7));
            if (stored is null)
            {
                stored = filled;
                db.AnimeMetadata.Add(stored);
            }
            else
            {
                stored.AiredFrom = filled.AiredFrom;
                stored.TotalEpisodes = filled.TotalEpisodes;
                stored.MediaType = filled.MediaType;
                stored.MalScore ??= filled.MalScore;
                stored.Genres = filled.Genres;
                stored.Studios = filled.Studios;
                stored.LastSyncedAt = filled.LastSyncedAt;
            }

            if (Fills.TryGetValue(animeId, out var fill))
                fill(stored);

            await db.SaveChangesAsync(ct);
            return true;
        }
    }
}
