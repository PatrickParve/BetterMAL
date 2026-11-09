using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Profile;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>Runs This or That on stored data (add-this-or-that-ranking design.md
/// D2-D6). A ranking in progress is one <see cref="ThisOrThatRun"/> row per score
/// holding only facts; every read replays it against the score's hand-orderable
/// anime as they stand now (<see cref="ThisOrThatReplay"/>). The two reads write
/// nothing, so a change to my list is folded into the stored queue only by the
/// next write. My ranking itself changes only in <see cref="SaveAsync"/>, through
/// the ranking editor's own write.</summary>
public class ThisOrThatService(IAnimeRankingService ranking, AnimeTrackerDbContext db, Random random)
    : IThisOrThatService
{
    private static readonly ThisOrThatOutcome[] AllOutcomes =
        [ThisOrThatOutcome.Above, ThisOrThatOutcome.Below, ThisOrThatOutcome.CantDecide];

    public async Task<ThisOrThatScoresDto> GetScoresAsync(CancellationToken ct = default)
    {
        var counts = await ranking.GetScoreCountsAsync(TopAnimeMediaTypeScope.All, ct);
        var runs = await db.ThisOrThatRuns.AsNoTracking().ToListAsync(ct);
        var inProgress = runs.Where(IsReadable).Select(r => r.Score).ToHashSet();

        return new ThisOrThatScoresDto(counts
            .Where(c => c.Count >= 2)
            .OrderByDescending(c => c.Score)
            .Select(c => new ThisOrThatScoreSummary(
                c.Score, c.Count, ThisOrThatReplay.MaxPicks(c.Count), inProgress.Contains(c.Score)))
            .ToList());
    }

    public async Task<ThisOrThatScoreState?> GetScoreAsync(int score, CancellationToken ct = default)
    {
        var tier = await OfferedTierAsync(score, ct);
        if (tier is null)
            return null;

        var run = await db.ThisOrThatRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Score == score, ct);
        return await BuildScoreStateAsync(score, tier, run is not null && IsReadable(run) ? run : null, ct);
    }

    public async Task<ThisOrThatScoreState> StartAsync(int score, int? replacingVersion, CancellationToken ct = default)
    {
        var tier = await OfferedTierAsync(score, ct) ?? throw new ThisOrThatNotEnoughAnimeException(score);

        // Whatever refuses comes before anything is replaced. A row that can't be
        // read counts as no ranking, so it is replaced whatever version is named.
        var existing = await db.ThisOrThatRuns.FirstOrDefaultAsync(r => r.Score == score, ct);
        if (existing is not null && IsReadable(existing) && existing.Version != replacingVersion)
            throw new ThisOrThatStaleException($"The ranking of score {score} has moved on.");

        var now = DateTimeOffset.UtcNow;
        var queue = Shuffled(tier.Members.Select(m => m.AnimeId).ToList());
        var seed = random.Next();

        ThisOrThatRun run;
        if (existing is null)
        {
            run = new ThisOrThatRun
            {
                Score = score,
                QueueIds = queue,
                Seed = seed,
                Version = 1,
                StartedAt = now,
                UpdatedAt = now,
            };
            db.ThisOrThatRuns.Add(run);
        }
        else
        {
            // Replaced in place with the version carried on, so a tab still
            // showing the old ranking can never match the new one.
            run = existing;
            run.QueueIds = queue;
            run.AnswerChallengerIds = [];
            run.AnswerOtherIds = [];
            run.AnswerOutcomes = [];
            run.Seed = seed;
            run.Version = existing.Version + 1;
            run.StartedAt = now;
            run.UpdatedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another tab started this score between the read and the write, or a
            // pick moved the ranking on under a Start over.
            throw new ThisOrThatStaleException($"The ranking of score {score} has moved on.");
        }

        return await BuildScoreStateAsync(score, tier, run, ct);
    }

    public async Task<ThisOrThatScoreState> PickAsync(
        int score, int leftId, int rightId, int? winnerId, CancellationToken ct = default)
    {
        var (run, tier) = await LoadForWriteAsync(score, ct);
        var liveOrder = tier.Members.Select(m => m.AnimeId).ToList();
        var queue = FoldQueue(run.QueueIds, liveOrder);
        var answers = ToAnswers(run);

        // A finished ranking has no pair, so a pick on it is behind like any other.
        var replay = ThisOrThatReplay.Replay(queue, answers, liveOrder.ToHashSet());
        if (replay.Pair is not { } pair)
            throw new ThisOrThatStaleException($"The ranking of score {score} has no pair to pick on.");

        // The pair before the winner: a tab behind the ranking names cards that
        // have since left the table, and should be told it is behind, not wrong.
        var (expectedLeft, expectedRight) = Sides(run.Seed, pair);
        if (leftId != expectedLeft || rightId != expectedRight)
            throw new ThisOrThatStaleException("That is no longer the current pair.");
        if (winnerId is { } winner && winner != leftId && winner != rightId)
            throw new ThisOrThatWinnerNotInPairException(winner);

        var outcome = winnerId is null ? ThisOrThatOutcome.CantDecide
            : winnerId == pair.ChallengerId ? ThisOrThatOutcome.Above
            : ThisOrThatOutcome.Below;

        run.QueueIds = queue;
        run.AnswerChallengerIds = [.. run.AnswerChallengerIds, pair.ChallengerId];
        run.AnswerOtherIds = [.. run.AnswerOtherIds, pair.OtherId];
        run.AnswerOutcomes = [.. run.AnswerOutcomes, (int)outcome];
        Touch(run);
        await CommitAsync(score, ct);

        return await BuildScoreStateAsync(score, tier, run, ct);
    }

    public async Task<ThisOrThatScoreState> UndoAsync(int score, int version, CancellationToken ct = default)
    {
        var (run, tier) = await LoadForWriteAsync(score, ct);
        RequireVersion(run, version);
        if (run.AnswerChallengerIds.Count == 0)
            throw new ThisOrThatStaleException($"The ranking of score {score} has no pick to take back.");

        run.QueueIds = FoldQueue(run.QueueIds, tier.Members.Select(m => m.AnimeId).ToList());
        run.AnswerChallengerIds = [.. run.AnswerChallengerIds.SkipLast(1)];
        run.AnswerOtherIds = [.. run.AnswerOtherIds.SkipLast(1)];
        run.AnswerOutcomes = [.. run.AnswerOutcomes.SkipLast(1)];
        Touch(run);
        await CommitAsync(score, ct);

        return await BuildScoreStateAsync(score, tier, run, ct);
    }

    public async Task SaveAsync(int score, int version, CancellationToken ct = default)
    {
        var (run, tier) = await LoadForWriteAsync(score, ct);
        RequireVersion(run, version);

        var liveOrder = tier.Members.Select(m => m.AnimeId).ToList();
        var replay = ThisOrThatReplay.Replay(
            FoldQueue(run.QueueIds, liveOrder), ToAnswers(run), liveOrder.ToHashSet());
        if (replay.Order is not { } order)
            throw new ThisOrThatNotFinishedException(score);

        // The order holds exactly the score's hand-orderable anime, so the
        // slot-preserving merge writes it as given and places every one by hand. A
        // score that changed since is refused rather than half-written.
        try
        {
            await ranking.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(score, [.. order])], ct);
        }
        catch (AnimeRankingTierScoreMismatchException)
        {
            throw new ThisOrThatStaleException($"The ranking of score {score} has moved on.");
        }

        // Written, then deleted, in two steps. If the delete fails the ranking still
        // reads as finished with the same result, and saving again is harmless.
        db.ThisOrThatRuns.Remove(run);
        await CommitAsync(score, ct);
    }

    public async Task DiscardAsync(int score, int version, CancellationToken ct = default)
    {
        var run = await db.ThisOrThatRuns.FirstOrDefaultAsync(r => r.Score == score, ct);
        if (run is null || !IsReadable(run))
            throw new ThisOrThatRunNotFoundException(score);
        RequireVersion(run, version);

        db.ThisOrThatRuns.Remove(run);
        await CommitAsync(score, ct);
    }

    /// <summary>Which side an anime is placed from: 0 left, 1 right. A fixed integer
    /// mix of the ranking's seed and the anime's id (the murmur3 finalizer), and
    /// deliberately not <c>HashCode.Combine</c>, which is randomised per process, so
    /// a backend restart would flip every side (design.md D6).</summary>
    internal static int Side(int seed, int animeId)
    {
        unchecked
        {
            var x = (uint)seed ^ ((uint)animeId * 0x9E3779B1u);
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return (int)(x & 1u);
        }
    }

    private static (int Left, int Right) Sides(int seed, ThisOrThatPair pair) =>
        Side(seed, pair.ChallengerId) == 0
            ? (pair.ChallengerId, pair.OtherId)
            : (pair.OtherId, pair.ChallengerId);

    /// <summary>The score's hand-orderable anime in ranking order, or null when it
    /// holds fewer than two, which is a score that isn't offered.</summary>
    private async Task<AnimeRankingTierDto?> OfferedTierAsync(int score, CancellationToken ct)
    {
        var tier = await ranking.GetTierAsync(score, TopAnimeMediaTypeScope.All, ct);
        return tier is { Members.Count: >= 2 } ? tier : null;
    }

    private async Task<(ThisOrThatRun Run, AnimeRankingTierDto Tier)> LoadForWriteAsync(int score, CancellationToken ct)
    {
        var run = await db.ThisOrThatRuns.FirstOrDefaultAsync(r => r.Score == score, ct);
        if (run is null || !IsReadable(run))
            throw new ThisOrThatRunNotFoundException(score);

        // A score left with fewer than two anime isn't shown, so nothing can be
        // done to its ranking either.
        var tier = await OfferedTierAsync(score, ct) ?? throw new ThisOrThatRunNotFoundException(score);
        return (run, tier);
    }

    private static void RequireVersion(ThisOrThatRun run, int version)
    {
        if (run.Version != version)
            throw new ThisOrThatStaleException($"The ranking of score {run.Score} has moved on.");
    }

    private static void Touch(ThisOrThatRun run)
    {
        run.Version++;
        run.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Saves a write to a ranking. If the version has moved since the row
    /// was read, a double submit or a tab behind the ranking, the concurrency token
    /// refuses the save and the request is stale like any other.</summary>
    private async Task CommitAsync(int score, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ThisOrThatStaleException($"The ranking of score {score} has moved on.");
        }
    }

    /// <summary>A row whose parallel answer lists differ in length reads as no
    /// ranking, rather than failing the page.</summary>
    private static bool IsReadable(ThisOrThatRun run) =>
        run.AnswerChallengerIds.Count == run.AnswerOtherIds.Count
        && run.AnswerChallengerIds.Count == run.AnswerOutcomes.Count;

    private static List<ThisOrThatAnswer> ToAnswers(ThisOrThatRun run)
    {
        var answers = new List<ThisOrThatAnswer>(run.AnswerChallengerIds.Count);
        for (var i = 0; i < run.AnswerChallengerIds.Count; i++)
        {
            answers.Add(new ThisOrThatAnswer(
                run.AnswerChallengerIds[i], run.AnswerOtherIds[i], (ThisOrThatOutcome)run.AnswerOutcomes[i]));
        }

        return answers;
    }

    /// <summary>The stored queue with every live anime it doesn't hold appended, in
    /// its current ranking order when several join at once. An anime that left stays
    /// in the queue, so it takes back its place if it rejoins. Memory only: the
    /// stored row changes when the next write saves this.</summary>
    private static List<int> FoldQueue(IReadOnlyList<int> stored, IReadOnlyList<int> liveOrder)
    {
        var queued = stored.ToHashSet();
        return [.. stored, .. liveOrder.Where(id => !queued.Contains(id))];
    }

    private List<int> Shuffled(IReadOnlyList<int> ids)
    {
        var shuffled = ids.ToList();
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    private async Task<ThisOrThatScoreState> BuildScoreStateAsync(
        int score, AnimeRankingTierDto tier, ThisOrThatRun? run, CancellationToken ct)
    {
        var count = tier.Members.Count;
        return new ThisOrThatScoreState(
            score, count, ThisOrThatReplay.MaxPicks(count),
            run is null ? null : await BuildRunStateAsync(run, tier, ct));
    }

    private async Task<ThisOrThatRunState> BuildRunStateAsync(ThisOrThatRun run, AnimeRankingTierDto tier, CancellationToken ct)
    {
        var liveOrder = tier.Members.Select(m => m.AnimeId).ToList();
        var live = liveOrder.ToHashSet();
        var membersById = tier.Members.ToDictionary(m => m.AnimeId);
        var queue = FoldQueue(run.QueueIds, liveOrder);
        var answers = ToAnswers(run);
        var replay = ThisOrThatReplay.Replay(queue, answers, live);
        var canUndo = answers.Count > 0;

        if (replay.Order is { } order)
        {
            var previous = liveOrder.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index + 1);
            var rows = order
                .Select((id, index) =>
                {
                    var member = membersById[id];
                    return new ThisOrThatResultRow(
                        id, member.Title, member.EnglishTitle, member.PictureUrl, index + 1, previous[id]);
                })
                .ToList();

            return new ThisOrThatRunState(
                run.Version, order.Count, order.Count, answers.Count, 0, canUndo, null, rows, []);
        }

        var pair = replay.Pair!;
        var (leftId, rightId) = Sides(run.Seed, pair);

        var details = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == leftId || a.Id == rightId)
            .Select(a => new { a.Id, a.MediaType, a.AiredFrom })
            .ToListAsync(ct);

        ThisOrThatCard ToCard(int animeId)
        {
            var member = membersById[animeId];
            var detail = details.FirstOrDefault(d => d.Id == animeId);
            return new ThisOrThatCard(
                animeId, member.Title, member.EnglishTitle, member.PictureUrl, detail?.MediaType, detail?.AiredFrom?.Year);
        }

        // Every anime the next pair could hold, whichever way the pick goes, minus
        // the two already on screen, so the page can fetch their pictures now.
        var upcoming = new List<string>();
        foreach (var outcome in AllOutcomes)
        {
            var next = ThisOrThatReplay.UpcomingPair(queue, answers, live, pair, outcome);
            if (next is null)
                continue;

            foreach (var id in new[] { next.ChallengerId, next.OtherId })
            {
                if (id != pair.ChallengerId && id != pair.OtherId
                    && membersById[id].PictureUrl is { } url && !upcoming.Contains(url))
                    upcoming.Add(url);
            }
        }

        return new ThisOrThatRunState(
            run.Version, pair.Placed, pair.Total, answers.Count, pair.PicksLeftAtMost, canUndo,
            new ThisOrThatPairDto(ToCard(leftId), ToCard(rightId)), null, upcoming);
    }
}
