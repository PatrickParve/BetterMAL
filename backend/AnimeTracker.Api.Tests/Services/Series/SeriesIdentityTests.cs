using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// series-identity spec "resolved in one place" (tasks.md 5.1/5.5): the
// resolution table every surface that names or pictures a series routes
// through instead of projecting its root member directly.
public class SeriesIdentityTests
{
    [Fact]
    public void UnchosenSeriesFallsBackToItsRoot()
    {
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            selectedTitle: null, selectedPictureUrl: null,
            rootTitle: "Attack on Titan", rootEnglishTitle: "Shingeki no Kyojin", rootMalPictureUrl: "root.jpg");

        Assert.Equal("Attack on Titan", title);
        Assert.Equal("Shingeki no Kyojin", englishTitle);
        Assert.Equal("root.jpg", pictureUrl);
    }

    [Fact]
    public void ChosenTitleReplacesTitleAndSuppressesEnglishTitle()
    {
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            selectedTitle: "Beyblade", selectedPictureUrl: null,
            rootTitle: "Beyblade: Metal Fusion", rootEnglishTitle: "Beyblade: Metal Fusion", rootMalPictureUrl: "root.jpg");

        Assert.Equal("Beyblade", title);
        Assert.Null(englishTitle);
        Assert.Equal("root.jpg", pictureUrl); // no chosen picture -> falls back to root's MAL picture
    }

    [Fact]
    public void ChosenPictureReplacesRootPictureIndependentlyOfTitle()
    {
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            selectedTitle: null, selectedPictureUrl: "chosen.jpg",
            rootTitle: "Attack on Titan", rootEnglishTitle: "Shingeki no Kyojin", rootMalPictureUrl: "root.jpg");

        Assert.Equal("Attack on Titan", title);
        Assert.Equal("Shingeki no Kyojin", englishTitle);
        Assert.Equal("chosen.jpg", pictureUrl);
    }

    [Fact]
    public void ChosenTitleAndPictureBothApply()
    {
        var (title, englishTitle, pictureUrl) = SeriesIdentity.Resolve(
            selectedTitle: "Beyblade", selectedPictureUrl: "chosen.jpg",
            rootTitle: "Beyblade: Metal Fusion", rootEnglishTitle: "Beyblade: Metal Fusion", rootMalPictureUrl: "root.jpg");

        Assert.Equal("Beyblade", title);
        Assert.Null(englishTitle);
        Assert.Equal("chosen.jpg", pictureUrl);
    }
}
