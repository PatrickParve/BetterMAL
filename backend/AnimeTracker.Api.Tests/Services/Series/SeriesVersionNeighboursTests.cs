using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesVersionNeighbours (rebuild-series-by-story-component design.md
// decision D2, task 3.2): classifies a story component's version neighbours —
// FoldedVersion when the neighbour carries no story relation of its own,
// NeighbourTelling when it does, excluded entirely when it has no cached
// metadata row at all.
public class SeriesVersionNeighboursTests
{
    private static AnimeMetadata Anime(int id, string mediaType, DateOnly? airedFrom = null) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
    };

    private static void Relate(AnimeMetadata from, AnimeMetadata to, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = to.Id,
            RelationType = relationType,
            Title = to.Title,
        });

    // Clannad <-sequel-> After Story is the story component; the Movie hangs
    // off Clannad by alternative_version alone, with no story relation of its
    // own, so its story component is itself alone — an ordinary extra.
    [Fact]
    public void ALoneAlternativeVersionWithNoStoryRelationsFoldsIn()
    {
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        var movie = Anime(3, "movie");

        Relate(clannad, afterStory, "sequel");
        Relate(clannad, movie, "alternative_version");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [clannad, afterStory],
            cachedNeighbours: [movie]);

        var neighbour = Assert.Single(neighbours);
        Assert.Equal(movie.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.FoldedVersion, neighbour.MembershipKind);
    }

    // Both of Clannad's alternative_setting specials carry no story relation
    // of their own, so both fold in alongside the Movie.
    [Fact]
    public void BothAlternativeSettingSpecialsFoldIn()
    {
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        var settingSpecial1 = Anime(3, "special");
        var settingSpecial2 = Anime(4, "special");

        Relate(clannad, afterStory, "sequel");
        Relate(clannad, settingSpecial1, "alternative_setting");
        Relate(afterStory, settingSpecial2, "alternative_setting");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [clannad, afterStory],
            cachedNeighbours: [settingSpecial1, settingSpecial2]);

        Assert.Equal(2, neighbours.Count);
        Assert.All(neighbours, n => Assert.Equal(MembershipKind.FoldedVersion, n.MembershipKind));
    }

    // Brotherhood is a version neighbour of the 2003 series, but carries a
    // side_story of its own — it heads a component of its own, so its tile
    // must open its own series rather than folding in as an extra.
    [Fact]
    public void ARetellingWithItsOwnStoryRelationsOpensItsOwnSeries()
    {
        var anime2003 = Anime(1, "tv");
        var brotherhood = Anime(2, "tv");
        var brotherhoodSpecial = Anime(3, "special");

        Relate(anime2003, brotherhood, "alternative_version");
        Relate(brotherhood, brotherhoodSpecial, "side_story");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [anime2003],
            cachedNeighbours: [brotherhood]);

        var neighbour = Assert.Single(neighbours);
        Assert.Equal(brotherhood.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.NeighbourTelling, neighbour.MembershipKind);
    }

    // Prisma☆Illya is an alternative_setting of Fate/stay night with its
    // own sequels — same shape as Brotherhood, opens its own series.
    [Fact]
    public void AnAlternativeSettingWithItsOwnSequelsOpensItsOwnSeries()
    {
        var fateStayNight = Anime(1, "tv");
        var prismaIllya = Anime(2, "tv");
        var prismaIllya2Wei = Anime(3, "tv");

        Relate(fateStayNight, prismaIllya, "alternative_setting");
        Relate(prismaIllya, prismaIllya2Wei, "sequel");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [fateStayNight],
            cachedNeighbours: [prismaIllya]);

        var neighbour = Assert.Single(neighbours);
        Assert.Equal(prismaIllya.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.NeighbourTelling, neighbour.MembershipKind);
    }

    // A version neighbour with no cached metadata row at all can't be a
    // SeriesMember — it's excluded entirely, left for the caller's
    // related-entry projection instead of being misclassified.
    [Fact]
    public void AnUncachedNeighbourIsExcluded()
    {
        var clannad = Anime(1, "tv");
        var uncachedSpecial = Anime(2, "special"); // has a row in memory here only to declare the edge's far end

        Relate(clannad, uncachedSpecial, "alternative_setting");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [clannad],
            cachedNeighbours: []); // the far end's row was never actually cached

        Assert.Empty(neighbours);
    }

    [Fact]
    public void AComponentMemberItselfIsNeverReturnedAsANeighbour()
    {
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        Relate(clannad, afterStory, "sequel");
        Relate(afterStory, clannad, "alternative_setting"); // an unusual edge, but still between two component members

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [clannad, afterStory],
            cachedNeighbours: [clannad, afterStory]);

        Assert.Empty(neighbours);
    }

    // AnimeRelatedAnime can be one-sided the same way story relations
    // already are (rebuild-series-by-story-component design.md D3: "in both
    // directions") — here neither component member declares a version
    // relation at all, only the Movie does, pointing back at Clannad. The
    // Movie must still be found and classified from its own side.
    [Fact]
    public void ACandidateDeclaringTheEdgeBackAtAMemberIsStillFound()
    {
        var clannad = Anime(1, "tv");
        var afterStory = Anime(2, "tv");
        var movie = Anime(3, "movie");

        Relate(clannad, afterStory, "sequel");
        Relate(movie, clannad, "alternative_version");

        var neighbours = SeriesVersionNeighbours.Classify(
            componentMembers: [clannad, afterStory],
            cachedNeighbours: [movie]);

        var neighbour = Assert.Single(neighbours);
        Assert.Equal(movie.Id, neighbour.AnimeId);
        Assert.Equal(MembershipKind.FoldedVersion, neighbour.MembershipKind);
    }
}
