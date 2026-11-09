using System.Numerics;

namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>One answer given in a ranking: the anime being placed, the anime it
/// was shown with, and how the pick came out from the first one's side.</summary>
public readonly record struct ThisOrThatAnswer(int ChallengerId, int OtherId, ThisOrThatOutcome Outcome);

/// <summary>The pair a ranking asks for next, with where the ranking stands.
/// <see cref="Placed"/> counts the live anime already placed (not the
/// challenger), <see cref="Total"/> every live anime in the ranking, and
/// <see cref="PicksLeftAtMost"/> the most picks it can still take.</summary>
public sealed record ThisOrThatPair(int ChallengerId, int OtherId, int Placed, int Total, int PicksLeftAtMost);

/// <summary>What replaying a ranking gives: either the next pair to ask, or, once
/// every anime is placed, the finished order (best first).</summary>
public sealed record ThisOrThatReplayResult(ThisOrThatPair? Pair, IReadOnlyList<int>? Order)
{
    public bool IsFinished => Order is not null;
}

/// <summary>Rebuilds a This or That ranking from the facts a run stores
/// (add-this-or-that-ranking design.md D2/D3). Pure: no database, no clock, no
/// randomness, so every edge case is a plain test.
///
/// The ranking places the queue's anime one at a time by halving, each among the
/// anime already placed. Per challenger, ALL of its answers are applied as
/// constraints on the range it could still belong in, and only then is the live
/// middle of what's left asked. That order is what keeps a later answer from
/// rerouting an earlier step: a first draft that tried the plain middle first
/// let it, and widened the range again.
///
/// A ghost is a queued anime that is no longer live (it left the score). A ghost
/// keeps its slot when its answers fully determined it, so what was placed
/// against it still narrows; otherwise nothing depends on it and it drops out.</summary>
public static class ThisOrThatReplay
{
    /// <summary>Replays <paramref name="answers"/> over <paramref name="queue"/>.
    /// <paramref name="liveIds"/> are the score's anime now; a live anime missing
    /// from <paramref name="queue"/> is left out of the result, so the caller
    /// appends the joiners first. An id queued twice counts once, at its first
    /// place.</summary>
    public static ThisOrThatReplayResult Replay(
        IReadOnlyList<int> queue, IReadOnlyList<ThisOrThatAnswer> answers, IReadOnlySet<int> liveIds)
    {
        var byChallenger = new Dictionary<int, List<ThisOrThatAnswer>>();
        foreach (var answer in answers)
        {
            if (!byChallenger.TryGetValue(answer.ChallengerId, out var given))
                byChallenger[answer.ChallengerId] = given = [];
            given.Add(answer);
        }

        // May hold ghosts. Index 0 is the best.
        var placed = new List<int>();
        var seen = new HashSet<int>();

        for (var queueIndex = 0; queueIndex < queue.Count; queueIndex++)
        {
            var challenger = queue[queueIndex];
            if (!seen.Add(challenger))
                continue;

            // The range of slots (lo up to, not including, hi) the challenger
            // could still go in. Above narrows it from the top, Below from the
            // bottom, and Can't decide fixes it to the slot below that anime.
            var lo = 0;
            var hi = placed.Count;
            int? fixedAt = null;
            if (byChallenger.TryGetValue(challenger, out var given))
            {
                foreach (var answer in given)
                {
                    // An anime that was dropped as an undetermined ghost no longer
                    // constrains anything.
                    var index = placed.IndexOf(answer.OtherId);
                    if (index < 0)
                        continue;

                    switch (answer.Outcome)
                    {
                        case ThisOrThatOutcome.Above:
                            hi = Math.Min(hi, index);
                            break;
                        case ThisOrThatOutcome.Below:
                            lo = Math.Max(lo, index + 1);
                            break;
                        case ThisOrThatOutcome.CantDecide:
                            fixedAt = index + 1;
                            break;
                    }
                }
            }

            if (fixedAt is { } at)
                lo = hi = at;
            // Cannot come from answers given through this replay. Clamped rather
            // than thrown, so a hand-edited row can't break the page.
            else if (lo > hi)
                lo = hi = Math.Min(lo, placed.Count);

            if (!liveIds.Contains(challenger))
            {
                if (lo == hi)
                    placed.Insert(lo, challenger);
                continue;
            }

            // Ghosts in the range don't count: the pivot is the middle of the live
            // anime, the lower-placed of two middles, so every pick at least
            // halves what is live and the bound holds.
            var liveSlots = new List<int>();
            for (var i = lo; i < hi; i++)
            {
                if (liveIds.Contains(placed[i]))
                    liveSlots.Add(i);
            }

            if (liveSlots.Count == 0)
            {
                placed.Insert(lo, challenger);
                continue;
            }

            var pivot = placed[liveSlots[liveSlots.Count / 2]];

            var livePlaced = 0;
            foreach (var id in placed)
            {
                if (liveIds.Contains(id))
                    livePlaced++;
            }

            var waiting = new HashSet<int>();
            for (var i = queueIndex + 1; i < queue.Count; i++)
            {
                if (liveIds.Contains(queue[i]) && !seen.Contains(queue[i]))
                    waiting.Add(queue[i]);
            }

            var picksLeft = PicksToPlace(liveSlots.Count);
            for (var j = 0; j < waiting.Count; j++)
                picksLeft += PicksToPlace(livePlaced + 1 + j);

            return new ThisOrThatReplayResult(
                new ThisOrThatPair(challenger, pivot, livePlaced, livePlaced + 1 + waiting.Count, picksLeft), null);
        }

        return new ThisOrThatReplayResult(null, placed.Where(liveIds.Contains).ToList());
    }

    /// <summary>The pair the ranking would ask next if the current pair were
    /// answered with <paramref name="outcome"/>; null when that answer would
    /// finish it. Used to start the next pair's pictures downloading before the
    /// pick is made (design.md D5).</summary>
    public static ThisOrThatPair? UpcomingPair(
        IReadOnlyList<int> queue,
        IReadOnlyList<ThisOrThatAnswer> answers,
        IReadOnlySet<int> liveIds,
        ThisOrThatPair current,
        ThisOrThatOutcome outcome) =>
        Replay(queue, [.. answers, new ThisOrThatAnswer(current.ChallengerId, current.OtherId, outcome)], liveIds).Pair;

    /// <summary>The most picks a score of <paramref name="count"/> anime can take:
    /// the sum of ⌈log₂(k + 1)⌉ for k = 1 … count − 1, each later anime placed
    /// among the k already placed. 33 for 12, 412 for 77, 937 for 149, 0 for 1.</summary>
    public static int MaxPicks(int count)
    {
        var total = 0;
        for (var placed = 1; placed < count; placed++)
            total += PicksToPlace(placed);
        return total;
    }

    /// <summary>The most picks to place one anime among <paramref name="placed"/>
    /// others by halving: ⌈log₂(placed + 1)⌉, which is the bit length of the count.</summary>
    internal static int PicksToPlace(int placed) =>
        placed <= 0 ? 0 : BitOperations.Log2((uint)placed) + 1;
}
