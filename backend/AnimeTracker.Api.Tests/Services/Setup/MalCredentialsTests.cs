using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// first-run-setup spec, "Setup checks both MyAnimeList credentials first" and
// design D5: each missing credential is named by its .env name.
public class MalCredentialsTests
{
    [Fact]
    public void BothSetMeansNothingIsMissing()
    {
        var missing = MalCredentials.Missing(new MalOptions { ClientId = "id", ClientSecret = "secret" });

        Assert.Empty(missing);
    }

    [Fact]
    public void ASecretThatIsNullNamesOnlyTheSecret()
    {
        var missing = MalCredentials.Missing(new MalOptions { ClientId = "id", ClientSecret = null });

        Assert.Equal(["MAL_CLIENT_SECRET"], missing);
    }

    [Fact]
    public void AClientIdThatIsEmptyNamesOnlyTheId()
    {
        var missing = MalCredentials.Missing(new MalOptions { ClientId = "", ClientSecret = "secret" });

        Assert.Equal(["MAL_CLIENT_ID"], missing);
    }

    [Fact]
    public void NeitherSetNamesBothWithTheIdFirst()
    {
        var missing = MalCredentials.Missing(new MalOptions());

        Assert.Equal(["MAL_CLIENT_ID", "MAL_CLIENT_SECRET"], missing);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("  \n ")]
    public void AWhitespaceValueCountsAsMissing(string blank)
    {
        var missing = MalCredentials.Missing(new MalOptions { ClientId = blank, ClientSecret = blank });

        Assert.Equal(["MAL_CLIENT_ID", "MAL_CLIENT_SECRET"], missing);
    }
}
