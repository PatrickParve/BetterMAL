using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using static AnimeTracker.Api.Tests.Services.IdMapping.FakeCustomIdMappings;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// external-id-mapping spec "A custom mapping file supplements and corrects the
// synced mapping" (design.md D19): how an entry applies on top of the synced
// mapping. Pure, so no file and no database.
public class AnimeIdMappingMergeTests
{
    private static AnimeIdMapping Synced(
        int animeId, int? tv = null, int? season = null, int[]? movies = null, string[]? imdb = null) => new()
    {
        AnimeId = animeId,
        TmdbTvId = tv,
        TmdbSeasonNumber = season,
        TmdbMovieIds = [.. movies ?? []],
        ImdbIds = [.. imdb ?? []],
    };

    // --- Whether anything applies ---

    [Fact]
    public void WithNoEntryTheSyncedMappingIsUnchanged()
    {
        var synced = Synced(1, tv: 10, season: 1, imdb: ["tt0000001"]);

        Assert.Same(synced, AnimeIdMappingMerge.Apply(synced, null));
    }

    [Fact]
    public void WithNeitherThereIsNoMapping()
    {
        Assert.Null(AnimeIdMappingMerge.Apply(null, null));
    }

    [Fact]
    public void AnAnimeTheSourceDoesNotKnowTakesItsWholeMappingFromTheEntry()
    {
        var merged = AnimeIdMappingMerge.Apply(null, Fill(60568, tv: 280564, season: 1, imdb: ["tt38754770"]));

        Assert.NotNull(merged);
        Assert.Equal(60568, merged.AnimeId);
        Assert.Equal(280564, merged.TmdbTvId);
        Assert.Equal(1, merged.TmdbSeasonNumber);
        Assert.Empty(merged.TmdbMovieIds);
        Assert.Equal(["tt38754770"], merged.ImdbIds);
    }

    // --- A default entry fills, and yields to the source ---

    [Fact]
    public void ADefaultEntryYieldsToTmdbIdsTheSourceHas()
    {
        var synced = Synced(1, tv: 10, season: 1);

        var merged = AnimeIdMappingMerge.Apply(synced, Fill(1, tv: 99, season: 5));

        Assert.Same(synced, merged); // nothing applied, so not even a copy
        Assert.NotNull(merged);
        Assert.Equal(10, merged.TmdbTvId);
        Assert.Equal(1, merged.TmdbSeasonNumber);
    }

    [Fact]
    public void ADefaultEntryYieldsToASourceMovieIdToo()
    {
        // "Has TMDB ids" means a TV id or movie ids: a movie the source knows is not a gap.
        var synced = Synced(1, movies: [635302]);

        var merged = AnimeIdMappingMerge.Apply(synced, Fill(1, tv: 99, season: 5));

        Assert.Same(synced, merged);
    }

    [Fact]
    public void ADefaultEntryFillsATmdbGroupTheSourceLacksAndKeepsTheSourcesImdbIds()
    {
        // The source row is IMDb-only: the entry's TV id and season fill the
        // TMDB group, and the source's IMDb id stays.
        var merged = AnimeIdMappingMerge.Apply(Synced(1, imdb: ["tt0000001"]), Fill(1, tv: 99, season: 2, imdb: ["tt0000002"]));

        Assert.Equal(99, merged!.TmdbTvId);
        Assert.Equal(2, merged.TmdbSeasonNumber);
        Assert.Equal(["tt0000001"], merged.ImdbIds);
    }

    [Fact]
    public void TmdbAndImdbAreDecidedSeparately()
    {
        // The source has a TV id but no IMDb id: only the IMDb group applies.
        var merged = AnimeIdMappingMerge.Apply(Synced(1, tv: 10, season: 1), Fill(1, tv: 99, season: 5, imdb: ["tt0000002"]));

        Assert.Equal(10, merged!.TmdbTvId);
        Assert.Equal(1, merged.TmdbSeasonNumber);
        Assert.Equal(["tt0000002"], merged.ImdbIds);
    }

    [Fact]
    public void AGroupTheEntryDoesNotGiveIsReadFromTheSource()
    {
        // The entry names only IMDb ids; the source has neither group.
        var merged = AnimeIdMappingMerge.Apply(Synced(1, tv: 10, season: 1), Fill(1, imdb: ["tt0000002"]));

        Assert.Equal(10, merged!.TmdbTvId);
        Assert.Equal(["tt0000002"], merged.ImdbIds);
    }

    // --- An override wins ---

    [Fact]
    public void AnOverrideCorrectsASeasonTheSourceHasWrong()
    {
        // Fate/Zero Season 2: the source says TMDB season 1, TMDB now has a season 2.
        var synced = Synced(11741, tv: 45845, season: 1, imdb: ["tt2051178"]);

        var merged = AnimeIdMappingMerge.Apply(synced, Override(11741, tv: 45845, season: 2));

        Assert.Equal(45845, merged!.TmdbTvId);
        Assert.Equal(2, merged.TmdbSeasonNumber);
        Assert.Equal(["tt2051178"], merged.ImdbIds); // not named by the override: the source's stay
    }

    [Fact]
    public void AnOverrideReplacesTheWholeTmdbGroupNotFieldByField()
    {
        // The source says movie 5; the override says TV 9. The result is TV 9
        // alone: a movie id left over from the source would draw a set the
        // override never meant.
        var merged = AnimeIdMappingMerge.Apply(Synced(1, movies: [5]), Override(1, tv: 9, season: 1));

        Assert.Equal(9, merged!.TmdbTvId);
        Assert.Empty(merged.TmdbMovieIds);
    }

    [Fact]
    public void AnOverrideThatNamesOnlyImdbIdsLeavesTheTmdbGroupToTheSource()
    {
        var merged = AnimeIdMappingMerge.Apply(Synced(1, tv: 10, season: 1, imdb: ["tt0000001"]), Override(1, imdb: ["tt0000009"]));

        Assert.Equal(10, merged!.TmdbTvId);
        Assert.Equal(1, merged.TmdbSeasonNumber);
        Assert.Equal(["tt0000009"], merged.ImdbIds);
    }

    // --- The synced mapping is never modified ---

    [Fact]
    public void ApplyReturnsANewMappingAndLeavesTheSyncedOneAlone()
    {
        var synced = Synced(1, tv: 10, season: 1, imdb: ["tt0000001"]);

        var merged = AnimeIdMappingMerge.Apply(synced, Override(1, tv: 10, season: 2, imdb: ["tt0000009"]));

        Assert.NotSame(synced, merged);
        Assert.Equal(1, synced.TmdbSeasonNumber);
        Assert.Equal(["tt0000001"], synced.ImdbIds);
        Assert.Equal(2, merged!.TmdbSeasonNumber);
    }

    // --- "No longer needed": the entry would change nothing ---

    [Fact]
    public void ADefaultEntryTheSourceHasFilledInIsUnnecessary()
    {
        Assert.True(AnimeIdMappingMerge.IsUnnecessary(Synced(1, tv: 10, season: 1, imdb: ["tt0000001"]), Fill(1, tv: 99, imdb: ["tt0000002"])));
    }

    [Fact]
    public void ADefaultEntryForAnAnimeTheSourceLacksIsNecessary()
    {
        Assert.False(AnimeIdMappingMerge.IsUnnecessary(null, Fill(1, tv: 10, season: 1)));
    }

    [Fact]
    public void ADefaultEntryThatStillFillsOneGroupIsNecessary()
    {
        // The source has the TMDB ids now but still no IMDb id.
        Assert.False(AnimeIdMappingMerge.IsUnnecessary(Synced(1, tv: 10, season: 1), Fill(1, tv: 10, season: 1, imdb: ["tt0000002"])));
    }

    [Fact]
    public void AnOverrideTheSourceNowAgreesWithIsUnnecessary()
    {
        Assert.True(AnimeIdMappingMerge.IsUnnecessary(Synced(1, tv: 10, season: 2), Override(1, tv: 10, season: 2)));
    }

    [Fact]
    public void AnOverrideThatStillDiffersFromTheSourceIsNecessary()
    {
        Assert.False(AnimeIdMappingMerge.IsUnnecessary(Synced(1, tv: 10, season: 1), Override(1, tv: 10, season: 2)));
    }

    [Fact]
    public void MovieIdOrderMattersWhenComparing()
    {
        // The mapping's movie ids are an ordered list (the picker shows them in this order).
        Assert.False(AnimeIdMappingMerge.IsUnnecessary(Synced(1, movies: [1, 2]), Override(1, movies: [2, 1])));
        Assert.True(AnimeIdMappingMerge.IsUnnecessary(Synced(1, movies: [1, 2]), Override(1, movies: [1, 2])));
    }
}
