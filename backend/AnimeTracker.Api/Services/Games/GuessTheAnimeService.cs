using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Runs Guess the Anime on stored data, fetching what an anime is missing
/// when the game needs it (add-guess-the-anime design.md D1-D9). The game in
/// progress is one <see cref="GuessTheAnimeRun"/> row, deleted however the game
/// ends, so nothing about a finished game survives;
/// <see cref="GuessTheAnimeAppearance"/> rows, which hold only which anime were
/// the answer, outlive it to hold them back.
///
/// The answer is in no response before the game ends, not even its id: a row is
/// the guess's own, compared with the answer on every read.</summary>
public class GuessTheAnimeService(
    AnimeTrackerDbContext db,
    Random random,
    GameLobbyReads lobby,
    IGameAnimeDetails details,
    IEpisodeAiringRepository airings,
    ILogger<GuessTheAnimeService> logger) : IGuessTheAnimeService
{
    /// <summary>How many guesses a game gives. The one place the number lives: the
    /// client reads it from the game.</summary>
    public const int MaxGuesses = 10;

    // Starting a game fetches at most this many anime before it settles for ones
    // already fetched (design D6), so a start is never a long run of MAL calls.
    private const int MaxFetchesToStart = 3;

    private const string NothingToDraw = "No anime these settings allow could be used.";

    public async Task<GuessTheAnimeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
    {
        var run = await db.GuessTheAnimeRuns.AsNoTracking().OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var game = run is null ? null : await BuildGameAsync(run, ct);

        var years = await lobby.YearsAsync(hideHentai, ct);

        // The answer's poster is left out of the tile's art: nothing the server sends
        // during a game may name the anime being hidden.
        var pictures = await lobby.PopularPostersAsync(hideHentai, run?.AnswerAnimeId, ct);

        return new GuessTheAnimeStateDto(game, years, pictures);
    }

    public async Task<GamePoolDto> GetPoolAsync(GameSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        var count = await GamePool.Query(db, settings, DateTimeOffset.UtcNow).CountAsync(ct);
        return new GamePoolDto(count, count > 0);
    }

    public async Task<GuessTheAnimeGameDto> StartAsync(GameSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        // Everything that can refuse, and every MAL call, comes before anything is
        // deleted, so a refused Start leaves a game in progress as it was and no
        // fetch happens with a half-written game.
        var now = DateTimeOffset.UtcNow;
        var pool = await GamePool.Query(db, settings, now).ToCandidates().ToListAsync(ct);
        if (pool.Count == 0)
            throw new GuessTheAnimeNotPlayableException();

        var recent = await RecentAsync(pool.Count, ct);
        var answerId = await DrawAnswerAsync(settings, pool, recent, now, ct);

        GuessTheAnimeRun run;
        for (var attempt = 1; ; attempt++)
        {
            // Only a second tab can reach Start with a game in progress. If that
            // tab's guess lands while the old row is being deleted, the delete
            // finds the count moved on: delete what's there now instead.
            db.GuessTheAnimeRuns.RemoveRange(await db.GuessTheAnimeRuns.ToListAsync(ct));
            run = new GuessTheAnimeRun
            {
                AnswerAnimeId = answerId,
                GuessedAnimeIds = [],
                GuessCount = 0,
                YearFrom = settings.YearFrom,
                YearTo = settings.YearTo,
                MediaTypes = settings.MediaTypes,
                ListScope = settings.List,
                HideHentai = settings.HideHentai,
            };
            db.GuessTheAnimeRuns.Add(run);
            db.GuessTheAnimeAppearances.Add(new GuessTheAnimeAppearance { AnimeId = answerId });

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                db.ChangeTracker.Clear();
            }
        }

        return new GuessTheAnimeGameDto(run.Id, MaxGuesses, []);
    }

    public async Task<GuessTheAnimeGuessDto> GuessAsync(int runId, int number, int animeId, CancellationToken ct = default)
    {
        // A first look, untracked, so a refused guess is refused before anything
        // is fetched: a tab behind the game shouldn't cost a MAL request.
        var seen = await db.GuessTheAnimeRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new GuessTheAnimeRunNotFoundException(runId);
        CheckGuess(seen, number, animeId);

        // The fetch comes before the run is touched. The answer needs none: the
        // draw dealt with it.
        if (animeId != seen.AnswerAnimeId)
            await EnsureGuessFetchedAsync(animeId, ct);

        // The fetch can take a while, so the run is read again, tracked, and the
        // guess checked again against what is there now.
        var run = await db.GuessTheAnimeRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new GuessTheAnimeRunNotFoundException(runId);
        CheckGuess(run, number, animeId);

        var answer = await StatsAsync(run.AnswerAnimeId, ct);
        var guess = animeId == run.AnswerAnimeId ? answer : await StatsAsync(animeId, ct);
        var row = GuessComparison.Compare(answer, guess);

        if (animeId == run.AnswerAnimeId)
        {
            db.GuessTheAnimeRuns.Remove(run);
            await SaveAsync(number, ct);
            return new GuessTheAnimeGuessDto(row, GuessTheAnimeEnd.Guessed, await AnswerAsync(answer, ct));
        }

        if (number >= MaxGuesses)
        {
            db.GuessTheAnimeRuns.Remove(run);
            await SaveAsync(number, ct);
            return new GuessTheAnimeGuessDto(row, GuessTheAnimeEnd.OutOfGuesses, await AnswerAsync(answer, ct));
        }

        run.GuessedAnimeIds = [.. run.GuessedAnimeIds, animeId];
        run.GuessCount = number;
        await SaveAsync(number, ct);

        return new GuessTheAnimeGuessDto(row, null, null);
    }

    public async Task<GuessTheAnimeEndDto> EndAsync(int runId, CancellationToken ct = default)
    {
        GuessTheAnimeRun run;
        for (var attempt = 1; ; attempt++)
        {
            run = await db.GuessTheAnimeRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
                ?? throw new GuessTheAnimeRunNotFoundException(runId);

            db.GuessTheAnimeRuns.Remove(run);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                // A guess moved the count on between the read and the delete. Ending
                // the game is still wanted: read what's there now and end that.
                db.ChangeTracker.Clear();
            }
        }

        return new GuessTheAnimeEndDto(await AnswerAsync(await StatsAsync(run.AnswerAnimeId, ct), ct));
    }

    /// <summary>The draw of design D6. Anime are drawn on this game's weights and
    /// fetched if they are missing a stat; one the fetch shows doesn't qualify is
    /// passed over. At most <see cref="MaxFetchesToStart"/> fetches are made. After a
    /// failed fetch, or the last of the three, the draw carries on among anime that
    /// have already had a full-detail fetch, with no more fetches, so a game can
    /// still be played while MyAnimeList is down.</summary>
    private async Task<int> DrawAnswerAsync(
        GameSettings settings,
        IReadOnlyList<PickCandidate> pool,
        IReadOnlyDictionary<int, int> recent,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Passed over, and once falling back, never fetched. Held in one set, which
        // the picker treats as used, because the cooldown follows the whole pool.
        var excluded = new HashSet<int>();
        var fetchesLeft = MaxFetchesToStart;
        var fallBack = false;
        string? reason = null;

        async Task FallBackAsync()
        {
            fallBack = true;
            excluded.UnionWith(await GamePool.Query(db, settings, now)
                .Where(a => a.LastSyncedAt == default).Select(a => a.Id).ToListAsync(ct));
        }

        while (true)
        {
            var drawn = GamePicker.Pick(pool, excluded, avoidScore: null, recent, GuessTheAnimeWeights.Popularity, random);
            if (drawn is null)
                throw new GuessTheAnimeNoAnswerException(reason ?? NothingToDraw);
            if (fallBack)
                return drawn.AnimeId;

            bool fetched;
            try
            {
                fetched = await details.EnsureFetchedAsync(drawn.AnimeId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Guess the Anime: fetching anime {AnimeId} to start a game failed.", drawn.AnimeId);
                db.ChangeTracker.Clear();
                reason = FailureReason(ex);
                await FallBackAsync();
                // An anime fetched before has what MyAnimeList had last time; one
                // never fetched is a row of gaps, and the fallback has put it out.
                if (await HasHadFullFetchAsync(drawn.AnimeId, ct))
                    return drawn.AnimeId;
                continue;
            }

            if (!fetched)
                return drawn.AnimeId;

            fetchesLeft--;
            // The fetch can disqualify an anime: a null rating passes Hide NSFW
            // until the fetch shows rx, and a row with no airing status passes the
            // aired test until the fetch fills it. The one pool query, narrowed to
            // this anime, is the single copy of those rules.
            if (await GamePool.Query(db, settings, now).AnyAsync(a => a.Id == drawn.AnimeId, ct))
                return drawn.AnimeId;

            excluded.Add(drawn.AnimeId);
            if (fetchesLeft == 0)
                await FallBackAsync();
        }
    }

    /// <summary>Makes sure a guessed anime has the stats a row shows. A fetch that
    /// fails for an anime that has never had a full fetch stops the guess, since
    /// its row would be gaps that read as wrong clues; for one that has, what's
    /// stored is as good as a fetch would probably give, and the guess goes
    /// ahead.</summary>
    private async Task EnsureGuessFetchedAsync(int animeId, CancellationToken ct)
    {
        try
        {
            await details.EnsureFetchedAsync(animeId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Guess the Anime: fetching guessed anime {AnimeId} failed.", animeId);
            db.ChangeTracker.Clear();
            if (!await HasHadFullFetchAsync(animeId, ct))
                throw new GuessTheAnimeDetailsUnavailableException(FailureReason(ex), ex);
        }
    }

    private async Task<bool> HasHadFullFetchAsync(int animeId, CancellationToken ct) =>
        await db.AnimeMetadata.AsNoTracking().AnyAsync(a => a.Id == animeId && a.LastSyncedAt != default, ct);

    /// <summary>A number first, then the anime: a tab behind the game names an
    /// anime that has since been guessed, and should be told it is behind, not
    /// that it is wrong.</summary>
    private static void CheckGuess(GuessTheAnimeRun run, int number, int animeId)
    {
        if (number != run.GuessCount + 1)
            throw new GuessTheAnimeStaleGuessException(number);
        if (run.GuessedAnimeIds.Contains(animeId))
            throw new GuessTheAnimeAlreadyGuessedException(animeId);
    }

    /// <summary>Saves a guess's changes. If the count has moved since it was read,
    /// a double press or a tab behind the game, the concurrency token refuses the
    /// save and the guess is stale like any other.</summary>
    private async Task SaveAsync(int number, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GuessTheAnimeStaleGuessException(number);
        }
    }

    /// <summary>How many answers ago each anime of the cooldown window was drawn,
    /// 0 for the latest. The window is a quarter of the pool, so the pool's size is
    /// needed first. This game's own list: Higher or Lower's has no effect.</summary>
    private async Task<Dictionary<int, int>> RecentAsync(int poolSize, CancellationToken ct)
    {
        var recentIds = await db.GuessTheAnimeAppearances.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Take(GameWeights.Cooldown(poolSize))
            .Select(a => a.AnimeId)
            .ToListAsync(ct);

        return GameWeights.RoundsAgo(recentIds);
    }

    private async Task<GuessTheAnimeGameDto> BuildGameAsync(GuessTheAnimeRun run, CancellationToken ct)
    {
        var answer = await StatsAsync(run.AnswerAnimeId, ct);

        // A guessed id that no longer exists is skipped: nothing deletes a
        // metadata row today, and an array can't carry a foreign key.
        var stored = await WithAiredEpisodesAsync(await db.AnimeMetadata.AsNoTracking()
            .Where(a => run.GuessedAnimeIds.Contains(a.Id))
            .Select(AnimeStats.Select)
            .ToListAsync(ct), ct);
        var byId = stored.ToDictionary(s => s.Id);

        var rows = run.GuessedAnimeIds
            .Where(byId.ContainsKey)
            .Select(id => GuessComparison.Compare(answer, byId[id]))
            .ToList();

        return new GuessTheAnimeGameDto(run.Id, MaxGuesses, rows);
    }

    private async Task<AnimeStats> StatsAsync(int animeId, CancellationToken ct)
    {
        var stats = await db.AnimeMetadata.AsNoTracking().Where(a => a.Id == animeId).Select(AnimeStats.Select).FirstAsync(ct);
        return (await WithAiredEpisodesAsync([stats], ct))[0];
    }

    /// <summary>A show MyAnimeList gives no episode total for, One Piece among them,
    /// is compared on how many episodes have aired instead. Read in one query for
    /// every such anime; one with no aired episode stored stays unknown.</summary>
    private async Task<List<AnimeStats>> WithAiredEpisodesAsync(List<AnimeStats> stats, CancellationToken ct)
    {
        var withoutTotal = stats.Where(s => s.Episodes is null).Select(s => s.Id).ToList();
        if (withoutTotal.Count == 0)
            return stats;

        var aired = await airings.GetMaxAiredEpisodesAsync(withoutTotal, DateTimeOffset.UtcNow, ct);
        return stats.Select(s => s.Episodes is null && aired.TryGetValue(s.Id, out var episodes) ? s with { Episodes = episodes } : s).ToList();
    }

    /// <summary>The answer's row, every stat the same as itself, and whether it's
    /// on my list.</summary>
    private async Task<GuessTheAnimeAnswerDto> AnswerAsync(AnimeStats answer, CancellationToken ct) =>
        new(GuessComparison.Compare(answer, answer),
            await db.UserAnimeEntries.AsNoTracking().AnyAsync(e => e.AnimeId == answer.Id, ct));

    /// <summary>Why a fetch failed, for the notice the player sees.</summary>
    private static string FailureReason(Exception ex) => MalCallFailure.Classify(ex) switch
    {
        MalCallFailureKind.NotFound => "MyAnimeList has no such anime.",
        MalCallFailureKind.Outage => "MyAnimeList couldn't be reached.",
        _ => "MyAnimeList didn't give its details.",
    };
}
