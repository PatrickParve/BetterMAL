using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Artwork;

// SeriesPicturePool.Build — the series picker's option set, deduplicated by
// PictureIdentity rather than raw URL equality (design D6 / the same MAL
// .webp-vs-.jpg duplication AnimePictureTests covers for a single anime).
public class SeriesPicturePoolTests
{
    private static SeriesMember Member(int animeId, string? pictureUrl, string? malPictureUrl, List<string>? pictureUrls, string? selectedPictureUrl = null) => new()
    {
        AnimeId = animeId,
        SeriesId = 1,
        IsMainLine = true,
        Order = animeId,
        Anime = new AnimeMetadata
        {
            Id = animeId,
            Title = $"Anime {animeId}",
            SelectedPictureUrl = selectedPictureUrl,
            PictureUrl = pictureUrl,
            MalPictureUrl = malPictureUrl,
            PictureUrls = pictureUrls,
        },
    };

    [Fact]
    public void Build_SameIdentityUnderTwoExtensionsWithinOneMemberCollapses()
    {
        var member = Member(
            animeId: 1,
            pictureUrl: "https://cdn.myanimelist.net/images/anime/1627/136934l.webp",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/1627/136934l.webp",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/1627/136934l.jpg"]);

        var pool = SeriesPicturePool.Build([member]);

        var single = Assert.Single(pool);
        // Kept as the member's own authoritative literal, not the .jpg
        // variant pictures[] listed for the same photo.
        Assert.Equal("https://cdn.myanimelist.net/images/anime/1627/136934l.webp", single);
    }

    [Fact]
    public void Build_DistinctMembersContributeDistinctPictures()
    {
        var a = Member(1, "https://cdn.myanimelist.net/images/anime/1/1a.jpg", "https://cdn.myanimelist.net/images/anime/1/1a.jpg", null);
        var b = Member(2, "https://cdn.myanimelist.net/images/anime/2/2b.jpg", "https://cdn.myanimelist.net/images/anime/2/2b.jpg", null);

        var pool = SeriesPicturePool.Build([a, b]);

        Assert.Equal(2, pool.Count);
    }

    [Fact]
    public void Build_AMembersChosenPictureMalNoLongerListsStaysInThePool()
    {
        // The member's own displayed picture (its chosen picture) is
        // upserted before its PictureUrls, so it stays in the pool even
        // after MAL drops it from that member's picture set (design.md D9).
        var member = Member(
            animeId: 5,
            pictureUrl: "https://cdn.myanimelist.net/images/anime/5/5-chosen.jpg",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/5/5-main.jpg",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/5/5-main.jpg"],
            selectedPictureUrl: "https://cdn.myanimelist.net/images/anime/5/5-chosen.jpg");

        var pool = SeriesPicturePool.Build([member]);

        Assert.Contains("https://cdn.myanimelist.net/images/anime/5/5-chosen.jpg", pool);
    }

    [Fact]
    public void Build_AMembersTmdbChoiceNeverEntersThePool()
    {
        // A member's displayed picture is its chosen one, and here that is a
        // TMDB image. The pool is the MyAnimeList list, so it must stay out
        // (design.md D13), while the member's own MAL pictures still count.
        var tmdbChoice = TmdbImageUrl.Original("/abc123.jpg");
        var member = Member(
            animeId: 7,
            pictureUrl: tmdbChoice,
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/7/7-main.jpg",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/7/7-main.jpg", "https://cdn.myanimelist.net/images/anime/7/7-alt.jpg"],
            selectedPictureUrl: tmdbChoice);

        var pool = SeriesPicturePool.Build([member]);

        Assert.DoesNotContain(tmdbChoice, pool);
        Assert.Equal(
            ["https://cdn.myanimelist.net/images/anime/7/7-main.jpg", "https://cdn.myanimelist.net/images/anime/7/7-alt.jpg"],
            pool);
    }

    [Fact]
    public void Build_ATmdbChoiceOnOneMemberDoesNotDisturbAnotherMembersPictures()
    {
        var chosen = Member(1, TmdbImageUrl.Original("/abc123.jpg"), "https://cdn.myanimelist.net/images/anime/1/1a.jpg", null,
            selectedPictureUrl: TmdbImageUrl.Original("/abc123.jpg"));
        var other = Member(2, "https://cdn.myanimelist.net/images/anime/2/2b.jpg", "https://cdn.myanimelist.net/images/anime/2/2b.jpg", null);

        var pool = SeriesPicturePool.Build([chosen, other]);

        Assert.Equal(
            ["https://cdn.myanimelist.net/images/anime/1/1a.jpg", "https://cdn.myanimelist.net/images/anime/2/2b.jpg"],
            pool);
    }
}
