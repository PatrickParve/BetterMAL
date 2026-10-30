using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Runs Higher or Lower on stored data (add-games-higher-or-lower
/// design.md D1-D9). The game in progress is one <see cref="HigherOrLowerRun"/>
/// row, deleted however the game ends, so nothing about a finished game
/// survives; <see cref="HigherOrLowerAppearance"/> rows, which hold only which
/// anime were shown, outlive it to hold them back.</summary>
public class HigherOrLowerService(AnimeTrackerDbContext db, Random random) : IHigherOrLowerService
{
    // The tile's art is two posters out of this many of the most popular anime.
    private const int PopularPosterPool = 200;
    private const int PostersOnTheTile = 2;

    // An anime is on screen for two rounds at most, its first appearance counting
    // as one, so it can stay once and then it has to go (design D8). Only the
    // first round has two new anime to choose between; after it the left anime
    // has always stayed once, and the new one on the right always takes over.
    private const int MaxStays = 1;

    private static readonly IReadOnlySet<int> NoAnime = new HashSet<int>();

    public async Task<HigherOrLowerStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
    {
        var run = await db.HigherOrLowerRuns.AsNoTracking().OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var game = run is null ? null : await BuildGameAsync(run, ct);

        var eligible = HigherOrLowerPool.Query(
            db, new HigherOrLowerSettings(null, null, null, HigherOrLowerListScope.All, hideHentai), DateTimeOffset.UtcNow);
        var earliest = await eligible.MinAsync(a => a.AiredFrom, ct);
        var latest = await eligible.MaxAsync(a => a.AiredFrom, ct);
        var years = earliest is { } min && latest is { } max ? new HigherOrLowerYearsDto(min.Year, max.Year) : null;

        return new HigherOrLowerStateDto(game, years, await PopularPostersAsync(hideHentai, ct));
    }

    public async Task<HigherOrLowerPoolDto> GetPoolAsync(HigherOrLowerSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        var query = HigherOrLowerPool.Query(db, settings, DateTimeOffset.UtcNow);
        var count = await query.CountAsync(ct);
        if (count < 2)
            return new HigherOrLowerPoolDto(count, false);

        // A few hundred distinct scores at most, however many anime there are.
        var scores = await query.Select(a => a.MalScore!.Value).Distinct().ToListAsync(ct);
        return new HigherOrLowerPoolDto(count, HigherOrLowerPool.IsPlayable(scores));
    }

    public async Task<HigherOrLowerGameDto> StartAsync(HigherOrLowerSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        // Everything that can refuse comes before anything is deleted, so a
        // refused Start leaves a game in progress as it was.
        var pool = await HigherOrLowerPool.Query(db, settings, DateTimeOffset.UtcNow).ToCandidates().ToListAsync(ct);
        if (!HigherOrLowerPool.IsPlayable(pool.Select(c => c.Score)))
            throw new HigherOrLowerNotPlayableException();

        var recent = await RecentAsync(pool.Count, ct);
        var left = HigherOrLowerPicker.Pick(pool, NoAnime, avoidScore: null, recent, random);
        var right = left is null
            ? null
            : HigherOrLowerPicker.Pick(pool, new HashSet<int> { left.AnimeId }, left.Score, recent, random);
        if (left is null || right is null)
            throw new HigherOrLowerNotPlayableException();

        HigherOrLowerRun run;
        for (var attempt = 1; ; attempt++)
        {
            // Only a second tab can reach Start with a game in progress. If that
            // tab's pick lands while the old row is being deleted, the delete
            // finds the round moved on: delete what's there now instead.
            db.HigherOrLowerRuns.RemoveRange(await db.HigherOrLowerRuns.ToListAsync(ct));
            run = new HigherOrLowerRun
            {
                Round = 1,
                Streak = 0,
                LeftAnimeId = left.AnimeId,
                RightAnimeId = right.AnimeId,
                LeftStays = 0,
                ShownAnimeIds = [left.AnimeId, right.AnimeId],
                YearFrom = settings.YearFrom,
                YearTo = settings.YearTo,
                MediaTypes = settings.MediaTypes,
                ListScope = settings.List,
                HideHentai = settings.HideHentai,
            };
            db.HigherOrLowerRuns.Add(run);
            db.HigherOrLowerAppearances.AddRange(
                new HigherOrLowerAppearance { AnimeId = left.AnimeId },
                new HigherOrLowerAppearance { AnimeId = right.AnimeId });

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

        return await BuildGameAsync(run, ct);
    }

    public async Task<HigherOrLowerPickDto> PickAsync(int runId, int round, int animeId, CancellationToken ct = default)
    {
        var run = await db.HigherOrLowerRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new HigherOrLowerRunNotFoundException(runId);
        // Round before card: a tab behind the game names an anime that has since
        // left the table, and it should be told it is behind, not that it is wrong.
        if (run.Round != round)
            throw new HigherOrLowerStaleRoundException(round);
        if (animeId != run.LeftAnimeId && animeId != run.RightAnimeId)
            throw new HigherOrLowerCardNotOnTableException(animeId);

        // Judged on the scores stored now, which a refresh may have moved since
        // the cards appeared (design D7). An anime that has somehow lost its score
        // counts as the lowest, so the pick can still be judged.
        var stored = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == run.LeftAnimeId || a.Id == run.RightAnimeId)
            .Select(a => new { a.Id, a.MalScore })
            .ToListAsync(ct);
        var leftScore = stored.Single(a => a.Id == run.LeftAnimeId).MalScore ?? 0;
        var rightScore = stored.Single(a => a.Id == run.RightAnimeId).MalScore ?? 0;

        var pickedScore = animeId == run.LeftAnimeId ? leftScore : rightScore;
        var otherScore = animeId == run.LeftAnimeId ? rightScore : leftScore;
        // A tie counts as right: nobody is blamed for a score that moved.
        var correct = HigherOrLowerPicker.Hundredths(pickedScore) >= HigherOrLowerPicker.Hundredths(otherScore);

        if (!correct)
        {
            db.HigherOrLowerRuns.Remove(run);
            await SaveAsync(round, ct);
            return new HigherOrLowerPickDto(false, leftScore, rightScore, run.Streak, null, HigherOrLowerEnd.Wrong, null);
        }

        var streak = run.Streak + 1;
        var milestoneLevel = HigherOrLowerMilestones.Level(streak);

        // In the first round the higher score stays, the left one on a tie. After
        // that the left anime has already stayed once, so it leaves whatever it
        // scored and the new one on the right takes over. An anime that takes over
        // has stayed once by doing so (design D8).
        var leftStays = HigherOrLowerPicker.Hundredths(leftScore) >= HigherOrLowerPicker.Hundredths(rightScore)
            && run.LeftStays < MaxStays;
        var stayerId = leftStays ? run.LeftAnimeId : run.RightAnimeId;
        var stayerScore = leftStays ? leftScore : rightScore;
        var stays = leftStays ? run.LeftStays + 1 : 1;

        // The next anime comes from the pool as the settings and my list stand
        // now, never repeats one this game has shown, and never ties the stayer.
        var pool = await HigherOrLowerPool.Query(db, HigherOrLowerSettings.From(run), DateTimeOffset.UtcNow)
            .ToCandidates().ToListAsync(ct);
        var recent = await RecentAsync(pool.Count, ct);
        var next = HigherOrLowerPicker.Pick(pool, run.ShownAnimeIds.ToHashSet(), stayerScore, recent, random);

        if (next is null)
        {
            // Nothing left to draw: a win, not an error.
            db.HigherOrLowerRuns.Remove(run);
            await SaveAsync(round, ct);
            return new HigherOrLowerPickDto(true, leftScore, rightScore, streak, milestoneLevel, HigherOrLowerEnd.Cleared, null);
        }

        run.Round = round + 1;
        run.Streak = streak;
        run.LeftAnimeId = stayerId;
        run.RightAnimeId = next.AnimeId;
        run.LeftStays = stays;
        run.ShownAnimeIds = [.. run.ShownAnimeIds, next.AnimeId];
        db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = next.AnimeId });
        await SaveAsync(round, ct);

        return new HigherOrLowerPickDto(
            true, leftScore, rightScore, streak, milestoneLevel, null, await BuildGameAsync(run, ct));
    }

    public async Task<HigherOrLowerEndDto> EndAsync(int runId, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var run = await db.HigherOrLowerRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
                ?? throw new HigherOrLowerRunNotFoundException(runId);

            db.HigherOrLowerRuns.Remove(run);
            try
            {
                await db.SaveChangesAsync(ct);
                return new HigherOrLowerEndDto(run.Streak);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                // A pick moved the round on between the read and the delete. Ending
                // the game is still wanted: read what's there now and end that.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Saves a pick's changes. If the round has moved since it was read, a
    /// double submit or a tab behind the game, the concurrency token refuses the
    /// save and the pick is stale like any other.</summary>
    private async Task SaveAsync(int round, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new HigherOrLowerStaleRoundException(round);
        }
    }

    /// <summary>How many cards ago each anime of the cooldown window was shown, 0
    /// for the latest. The window is a quarter of the pool, so the pool's size
    /// is needed first; an anime shown more than once counts from its latest
    /// showing.</summary>
    private async Task<Dictionary<int, int>> RecentAsync(int poolSize, CancellationToken ct)
    {
        var recentIds = await db.HigherOrLowerAppearances.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Take(HigherOrLowerWeights.Cooldown(poolSize))
            .Select(a => a.AnimeId)
            .ToListAsync(ct);

        var roundsAgo = new Dictionary<int, int>();
        for (var i = 0; i < recentIds.Count; i++)
            roundsAgo.TryAdd(recentIds[i], i);
        return roundsAgo;
    }

    private async Task<List<string>> PopularPostersAsync(bool hideHentai, CancellationToken ct)
    {
        var popular = db.AnimeMetadata.AsNoTracking().Where(a => a.PictureUrl != null && a.PopularityRank != null);
        if (hideHentai)
            popular = popular.Where(a => a.Rating != HigherOrLowerPool.HentaiRating);

        var candidates = await popular
            .OrderBy(a => a.PopularityRank)
            .Take(PopularPosterPool)
            .Select(a => a.PictureUrl!)
            .ToListAsync(ct);

        // With fewer than two, the tile is still drawn with what there is.
        var chosen = new List<string>();
        while (chosen.Count < PostersOnTheTile && candidates.Count > 0)
        {
            var i = random.Next(candidates.Count);
            chosen.Add(candidates[i]);
            candidates.RemoveAt(i);
        }

        return chosen;
    }

    private async Task<HigherOrLowerGameDto> BuildGameAsync(HigherOrLowerRun run, CancellationToken ct)
    {
        var rows = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == run.LeftAnimeId || a.Id == run.RightAnimeId)
            .Select(a => new CardRow(
                a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.MediaType, a.AiredFrom, a.MalScore, a.UserEntry != null))
            .ToListAsync(ct);

        // The left anime's score was revealed in the round before, so it is shown
        // from round 2 on. The right anime's is never in a game's read: it is only
        // ever sent by the pick that reveals it.
        return new HigherOrLowerGameDto(
            run.Id,
            run.Round,
            run.Streak,
            ToCard(rows.Single(r => r.Id == run.LeftAnimeId), scoreRevealed: run.Round >= 2),
            ToCard(rows.Single(r => r.Id == run.RightAnimeId), scoreRevealed: false));
    }

    private static HigherOrLowerCardDto ToCard(CardRow row, bool scoreRevealed) =>
        new(row.Id, row.Title, row.EnglishTitle, row.PictureUrl, row.MediaType, row.AiredFrom?.Year, row.InMyList,
            scoreRevealed ? row.MalScore : null);

    private record CardRow(
        int Id, string Title, string? EnglishTitle, string? PictureUrl, string? MediaType,
        DateOnly? AiredFrom, double? MalScore, bool InMyList);
}
