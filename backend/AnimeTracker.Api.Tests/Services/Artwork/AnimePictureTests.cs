using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Artwork;

// AnimePicture.Options — the anime picker's option set, deduplicated by
// PictureIdentity rather than raw URL equality (a real MAL quirk: the same
// photo can come back as main_picture's .webp and as a .jpg entry in
// `pictures`).
public class AnimePictureTests
{
    private static AnimeMetadata Anime(string? pictureUrl, string? malPictureUrl, List<string>? pictureUrls, string? selectedPictureUrl = null) => new()
    {
        Id = 1,
        Title = "T",
        SelectedPictureUrl = selectedPictureUrl,
        PictureUrl = pictureUrl,
        MalPictureUrl = malPictureUrl,
        PictureUrls = pictureUrls,
    };

    [Fact]
    public void Options_SameIdentityUnderTwoExtensionsCollapsesToOneTile()
    {
        // main_picture came back as .webp; the identical photo also appears
        // in `pictures` as .jpg (MAL's own duplication of one photo).
        var anime = Anime(
            pictureUrl: "https://cdn.myanimelist.net/images/anime/1028/117777l.webp",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/1028/117777l.webp",
            pictureUrls:
            [
                "https://cdn.myanimelist.net/images/anime/1687/110933l.jpg",
                "https://cdn.myanimelist.net/images/anime/1028/117777l.jpg",
                "https://cdn.myanimelist.net/images/anime/1737/119392l.jpg",
            ]);

        var options = AnimePicture.Options(anime);

        Assert.Equal(3, options.Count);
    }

    [Fact]
    public void Options_CollapsedSlotKeepsTheAuthoritativeLiteralForCurrentMatching()
    {
        var anime = Anime(
            pictureUrl: "https://cdn.myanimelist.net/images/anime/1028/117777l.webp",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/1028/117777l.webp",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/1028/117777l.jpg"]);

        var options = AnimePicture.Options(anime);

        // The collapsed slot must be the exact PictureUrl string, not the
        // .jpg variant pictures[] happened to list — otherwise nothing in
        // the option set matches the current selection by exact string,
        // which is how the picker's "Current" badge and IsOverridden work.
        Assert.Contains(anime.PictureUrl, options);
        Assert.DoesNotContain("https://cdn.myanimelist.net/images/anime/1028/117777l.jpg", options);
    }

    [Fact]
    public void Options_DifferentPhotosWithDifferentExtensionsStayDistinct()
    {
        var anime = Anime(
            pictureUrl: "https://cdn.myanimelist.net/images/anime/1/1a.jpg",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/1/1a.jpg",
            pictureUrls:
            [
                "https://cdn.myanimelist.net/images/anime/1/1a.jpg",
                "https://cdn.myanimelist.net/images/anime/2/2b.webp",
            ]);

        var options = AnimePicture.Options(anime);

        Assert.Equal(2, options.Count);
    }

    [Fact]
    public void Options_AChoiceMalNoLongerListsStaysInTheOptions()
    {
        // MAL has since dropped the chosen picture from `pictures` entirely
        // — it is never re-validated away (design.md D9 / spec "A stored
        // choice is never re-validated away").
        var anime = Anime(
            pictureUrl: "https://cdn.myanimelist.net/images/anime/9/9-chosen.jpg",
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/9/9-main.jpg",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/9/9-main.jpg"],
            selectedPictureUrl: "https://cdn.myanimelist.net/images/anime/9/9-chosen.jpg");

        var options = AnimePicture.Options(anime);

        Assert.Contains(anime.SelectedPictureUrl, options);
    }

    [Fact]
    public void Options_ATmdbChoiceNeverLandsInTheMalOptions()
    {
        // The choice is a TMDB image: the picker's TMDB sections offer it, so it
        // must not also surface under the MyAnimeList heading (design.md D13).
        var tmdbChoice = TmdbImageUrl.Original("/abc123.jpg");
        var anime = Anime(
            pictureUrl: tmdbChoice,
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/9/9-main.jpg",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/9/9-main.jpg", "https://cdn.myanimelist.net/images/anime/9/9-alt.jpg"],
            selectedPictureUrl: tmdbChoice);

        var options = AnimePicture.Options(anime);

        Assert.DoesNotContain(tmdbChoice, options);
        Assert.Equal(
            ["https://cdn.myanimelist.net/images/anime/9/9-main.jpg", "https://cdn.myanimelist.net/images/anime/9/9-alt.jpg"],
            options); // MAL's own set, in MAL's order, untouched by the choice
    }

    [Fact]
    public void Options_AMalChoiceIsStillUpsertedWhileATmdbChoiceIsSkipped()
    {
        // The guard is on the source of the choice, not on choosing at all: a MAL
        // choice keeps its identity-authoritative upsert.
        var malChoice = "https://cdn.myanimelist.net/images/anime/9/9-chosen.webp";
        var anime = Anime(
            pictureUrl: malChoice,
            malPictureUrl: "https://cdn.myanimelist.net/images/anime/9/9-main.jpg",
            pictureUrls: ["https://cdn.myanimelist.net/images/anime/9/9-chosen.jpg"],
            selectedPictureUrl: malChoice);

        var options = AnimePicture.Options(anime);

        Assert.Contains(malChoice, options);
        Assert.DoesNotContain("https://cdn.myanimelist.net/images/anime/9/9-chosen.jpg", options); // the .webp literal took the slot
    }
}
