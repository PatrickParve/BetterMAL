using AnimeTracker.Api.Services.Artwork;

namespace AnimeTracker.Api.Tests.Services.Artwork;

public class PictureIdentityTests
{
    [Theory]
    [InlineData("https://cdn.myanimelist.net/images/anime/1/1a.jpg", "https://cdn.myanimelist.net/images/anime/1/1a.webp")]
    [InlineData("https://cdn.myanimelist.net/images/anime/1/1a.jpeg", "https://cdn.myanimelist.net/images/anime/1/1a.png")]
    [InlineData("https://cdn.myanimelist.net/images/anime/1/1a.JPG", "https://cdn.myanimelist.net/images/anime/1/1a.jpg")]
    public void KeyFor_SamePathDifferentExtensionMatches(string a, string b)
    {
        Assert.Equal(PictureIdentity.KeyFor(a), PictureIdentity.KeyFor(b));
    }

    [Fact]
    public void KeyFor_DifferentPathsDoNotMatch()
    {
        Assert.NotEqual(
            PictureIdentity.KeyFor("https://cdn.myanimelist.net/images/anime/1/1a.jpg"),
            PictureIdentity.KeyFor("https://cdn.myanimelist.net/images/anime/2/2b.jpg"));
    }
}
