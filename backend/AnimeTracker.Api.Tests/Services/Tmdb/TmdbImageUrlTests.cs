using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec: the image URL is built on read from TMDB's file path
// (design.md D6), and TmdbImageUrl.IsTmdbImage is the one test for "a TMDB
// picture".
public class TmdbImageUrlTests
{
    [Fact]
    public void Original_ATmdbPathThatBeginsWithASlashGivesNoDoubleSlash()
    {
        Assert.Equal("https://image.tmdb.org/t/p/original/abc123.jpg", TmdbImageUrl.Original("/abc123.jpg"));
    }

    [Fact]
    public void Original_APathWithNoSlashIsFixed()
    {
        Assert.Equal("https://image.tmdb.org/t/p/original/abc123.jpg", TmdbImageUrl.Original("abc123.jpg"));
    }

    [Fact]
    public void IsTmdbImage_AnUrlBuiltByOriginalIsATmdbImage()
    {
        Assert.True(TmdbImageUrl.IsTmdbImage(TmdbImageUrl.Original("/abc123.jpg")));
    }

    [Fact]
    public void IsTmdbImage_AMalCdnUrlIsNotATmdbImage()
    {
        Assert.False(TmdbImageUrl.IsTmdbImage("https://cdn.myanimelist.net/images/anime/1028/117777l.webp"));
    }

    [Fact]
    public void IsTmdbImage_NullAndLookalikeHostsAreNot()
    {
        Assert.False(TmdbImageUrl.IsTmdbImage(null));
        Assert.False(TmdbImageUrl.IsTmdbImage("https://image.tmdb.org.evil.example/t/p/original/a.jpg")); // prefix ends at the slash after the host
    }
}
