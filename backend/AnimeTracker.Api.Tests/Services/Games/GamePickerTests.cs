using AnimeTracker.Api.Services.Games;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower design D5-D7 and the higher-or-lower-anime-selection
// spec. The picker is pure apart from its Random, so each case hands it a seeded
// one and counts draws.
public class GamePickerTests
{
    private static readonly IReadOnlySet<int> NoneUsed = new HashSet<int>();
    private static readonly IReadOnlyDictionary<int, int> NoneRecent = new Dictionary<int, int>();

    private static PickCandidate Candidate(int id, double score = 7.0, int? rank = 1, bool onMyList = false) =>
        new(id, score, rank, onMyList);

    private static Dictionary<int, int> DrawMany(
        IReadOnlyList<PickCandidate> pool,
        int draws,
        IReadOnlySet<int>? used = null,
        double? avoid = null,
        IReadOnlyDictionary<int, int>? recent = null,
        int seed = 7)
    {
        var random = new Random(seed);
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < draws; i++)
        {
            var picked = GamePicker.Pick(pool, used ?? NoneUsed, avoid, recent ?? NoneRecent, GameWeights.Popularity, random);
            Assert.NotNull(picked);
            counts[picked.AnimeId] = counts.GetValueOrDefault(picked.AnimeId) + 1;
        }
        return counts;
    }

    // ---- what can never be drawn ----

    [Fact]
    public void AnAnimeAlreadyUsedInTheGameIsNeverDrawn()
    {
        var pool = Enumerable.Range(1, 10).Select(id => Candidate(id, score: 5 + id / 10.0)).ToList();
        var used = new HashSet<int> { 2, 5, 9 };

        var counts = DrawMany(pool, 2_000, used);

        Assert.DoesNotContain(counts.Keys, id => used.Contains(id));
        Assert.Equal(7, counts.Count);
    }

    [Fact]
    public void AnAnimeWithTheScoreToAvoidIsNeverDrawn()
    {
        var pool = new List<PickCandidate>
        {
            Candidate(1, score: 7.85), Candidate(2, score: 7.85), Candidate(3, score: 7.84), Candidate(4, score: 7.86),
        };

        var counts = DrawMany(pool, 2_000, avoid: 7.85);

        Assert.DoesNotContain(1, counts.Keys);
        Assert.DoesNotContain(2, counts.Keys);
        Assert.Contains(3, counts.Keys);
        Assert.Contains(4, counts.Keys);
    }

    [Fact]
    public void ScoresAreComparedAtTwoDecimals()
    {
        // Floating-point noise below the second decimal must not let a match through.
        var pool = new List<PickCandidate>
        {
            Candidate(1, score: 7.85 + 1e-12), Candidate(2, score: 7.85 - 1e-12), Candidate(3, score: 7.8500000001), Candidate(4, score: 7.86),
        };

        var counts = DrawMany(pool, 1_000, avoid: 7.85);

        Assert.Equal([4], counts.Keys);
    }

    [Theory]
    [InlineData(7.85, 7.85, true)]
    [InlineData(7.85, 7.8500000001, true)]
    [InlineData(7.85, 7.854, true)]
    [InlineData(7.85, 7.86, false)]
    [InlineData(7.85, 7.84, false)]
    [InlineData(9.24, 9.24, true)]
    [InlineData(2.72, 2.73, false)]
    public void SameScoreMeansTheSameTwoDecimals(double a, double b, bool same)
    {
        Assert.Equal(same, GamePicker.SameScore(a, b));
        Assert.Equal(same, GamePicker.SameScore(b, a));
    }

    [Fact]
    public void NoScoreIsAvoidedForTheFirstDraw()
    {
        var pool = new List<PickCandidate> { Candidate(1, score: 7.85) };

        var picked = GamePicker.Pick(pool, NoneUsed, avoidScore: null, NoneRecent, GameWeights.Popularity, new Random(1));

        Assert.Equal(1, picked?.AnimeId);
    }

    // ---- nothing left ----

    [Fact]
    public void AnEmptyPoolReturnsNull()
    {
        Assert.Null(GamePicker.Pick([], NoneUsed, null, NoneRecent, GameWeights.Popularity, new Random(1)));
    }

    [Fact]
    public void AFullyUsedPoolReturnsNull()
    {
        var pool = new List<PickCandidate> { Candidate(1), Candidate(2, score: 8) };

        Assert.Null(GamePicker.Pick(pool, new HashSet<int> { 1, 2 }, null, NoneRecent, GameWeights.Popularity, new Random(1)));
    }

    [Fact]
    public void APoolOfOnlyTheScoreToAvoidReturnsNull()
    {
        var pool = new List<PickCandidate> { Candidate(1, score: 7.85), Candidate(2, score: 7.85) };

        Assert.Null(GamePicker.Pick(pool, NoneUsed, 7.85, NoneRecent, GameWeights.Popularity, new Random(1)));
    }

    [Fact]
    public void APoolLeftOnlyWithUsedAndMatchingScoresReturnsNull()
    {
        var pool = new List<PickCandidate> { Candidate(1, score: 7.0), Candidate(2, score: 8.0) };

        Assert.Null(GamePicker.Pick(pool, new HashSet<int> { 1 }, 8.0, NoneRecent, GameWeights.Popularity, new Random(1)));
    }

    [Fact]
    public void TheLastAnimeLeftIsDrawnEveryTime()
    {
        var pool = new List<PickCandidate> { Candidate(1), Candidate(2, score: 8), Candidate(3, score: 9) };

        var counts = DrawMany(pool, 100, new HashSet<int> { 1, 3 });

        Assert.Equal([2], counts.Keys);
    }

    // ---- weighting ----

    [Fact]
    public void ThePopularAnimeIsDrawnFarMoreOftenThanTheUnpopularOne()
    {
        var pool = new List<PickCandidate> { Candidate(1, rank: 500), Candidate(2, rank: 20_000) };

        var counts = DrawMany(pool, 5_000);

        // Weights 1 against the 0.03 floor: about 97% to 3%.
        Assert.True(counts[1] > counts[2] * 10, $"popular {counts[1]}, unpopular {counts[2]}");
    }

    [Fact]
    public void TheHeaviestCandidateWinsMostOften()
    {
        var pool = new List<PickCandidate>
        {
            // All above the floor, which starts near rank 8,660: weights about 0.88, 0.36, 0.14, 0.06.
            Candidate(1, rank: 1_600), Candidate(2, rank: 2_500), Candidate(3, rank: 4_000), Candidate(4, rank: 6_000),
        };

        var counts = DrawMany(pool, 10_000);

        var order = counts.OrderByDescending(c => c.Value).Select(c => c.Key).ToList();
        Assert.Equal([1, 2, 3, 4], order);
    }

    [Fact]
    public void AVeryLightCandidateStillGetsDrawn()
    {
        var pool = Enumerable.Range(1, 20).Select(id => Candidate(id, score: 5 + id / 10.0, rank: 100)).ToList();
        pool.Add(Candidate(99, score: 9.9, rank: 24_943));

        var counts = DrawMany(pool, 20_000);

        // Weight 0.03 among twenty at 1: about 0.15% of draws, so about 30 of 20,000.
        Assert.True(counts.GetValueOrDefault(99) > 0);
        Assert.True(counts.GetValueOrDefault(99) < counts[1]);
    }

    [Fact]
    public void AnAnimeOnMyListIsNotPenalisedForBeingUnpopular()
    {
        var pool = new List<PickCandidate>
        {
            Candidate(1, rank: 20_000, onMyList: true), Candidate(2, rank: 500, onMyList: false),
        };

        var counts = DrawMany(pool, 10_000);

        // Both at full weight: each about half.
        Assert.InRange(counts[1], 4_700, 5_300);
        Assert.InRange(counts[2], 4_700, 5_300);
    }

    [Fact]
    public void APoolOfOnlyTheLeastPopularAnimeStillDrawsWithoutFailure()
    {
        var pool = new List<PickCandidate>
        {
            Candidate(1, score: 6, rank: 15_000), Candidate(2, score: 7, rank: 20_000), Candidate(3, score: 8, rank: 24_000),
        };

        var counts = DrawMany(pool, 3_000);

        // Every one sits on the floor, so none is shut out.
        Assert.Equal(3, counts.Count);
    }

    [Fact]
    public void APoolWithNothingVeryPopularStillFavoursTheMostPopularOfIt()
    {
        var pool = new List<PickCandidate>
        {
            Candidate(1, score: 6, rank: 2_000), Candidate(2, score: 7, rank: 3_500), Candidate(3, score: 8, rank: 6_000),
        };

        var counts = DrawMany(pool, 5_000);

        Assert.True(counts[1] > counts[2]);
        Assert.True(counts[2] > counts[3]);
    }

    [Fact]
    public void ANullRankIsDrawnLessOftenThanAKnownPopularOne()
    {
        var pool = new List<PickCandidate> { Candidate(1, rank: 100), Candidate(2, rank: null) };

        var counts = DrawMany(pool, 5_000);

        Assert.True(counts[1] > counts[2] * 10);
        Assert.True(counts.GetValueOrDefault(2) > 0);
    }

    // ---- holding back what was shown recently ----

    [Fact]
    public void AnAnimeShownJustNowIsRarelyDrawnAgain()
    {
        var pool = Enumerable.Range(1, 100).Select(id => Candidate(id, score: 5 + id / 100.0)).ToList();
        var recent = new Dictionary<int, int> { [1] = 0 };

        var counts = DrawMany(pool, 20_000, recent: recent);

        // The pool is 100, so the cooldown is 25: freshness at 0 is (1/26)², about 0.0015.
        Assert.True(counts.GetValueOrDefault(1) < 20, $"drawn {counts.GetValueOrDefault(1)} times");
    }

    [Fact]
    public void AnAnimeComesBackAsItsShowingFallsFurtherBack()
    {
        var pool = Enumerable.Range(1, 100).Select(id => Candidate(id, score: 5 + id / 100.0)).ToList();
        // Cooldown 25: anime 1 was shown 0 ago, 2 was 12 ago, 3 is outside the window.
        var recent = new Dictionary<int, int> { [1] = 0, [2] = 12 };

        var counts = DrawMany(pool, 40_000, recent: recent);

        Assert.True(counts.GetValueOrDefault(1) < counts.GetValueOrDefault(2));
        Assert.True(counts.GetValueOrDefault(2) < counts[3]);
    }

    [Fact]
    public void AnAnimeBeyondTheWindowIsAsLikelyAsOneNeverShown()
    {
        var pool = Enumerable.Range(1, 2).Select(id => Candidate(id, score: 5 + id)).ToList();
        // Cooldown 1: anime 1 was shown 5 cards ago, which is out of the window.
        var recent = new Dictionary<int, int> { [1] = 5 };

        var counts = DrawMany(pool, 10_000, recent: recent);

        Assert.InRange(counts[1], 4_700, 5_300);
    }

    [Fact]
    public void HoldingBackNeverStopsAGameThatHasAnimeLeft()
    {
        // Everything in the pool was shown a moment ago; a draw still lands.
        var pool = Enumerable.Range(1, 8).Select(id => Candidate(id, score: 5 + id / 10.0)).ToList();
        var recent = Enumerable.Range(1, 8).ToDictionary(id => id, id => id - 1);

        var counts = DrawMany(pool, 1_000, recent: recent);

        Assert.NotEmpty(counts);
    }

    // ---- randomness ----

    [Fact]
    public void TheSameSeedDrawsTheSameAnimeAndAnotherSeedDoesNot()
    {
        var pool = Enumerable.Range(1, 500).Select(id => Candidate(id, score: 5 + id / 1000.0, rank: id)).ToList();

        int[] Sequence(int seed)
        {
            var random = new Random(seed);
            return Enumerable.Range(0, 20).Select(_ =>
                GamePicker.Pick(pool, NoneUsed, null, NoneRecent, GameWeights.Popularity, random)!.AnimeId).ToArray();
        }

        Assert.Equal(Sequence(3), Sequence(3));
        Assert.NotEqual(Sequence(3), Sequence(4));
    }
}
