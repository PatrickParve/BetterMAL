using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using Microsoft.AspNetCore.Builder;
using Npgsql;

namespace AnimeTracker.Api.Tests.Services.Infrastructure;

// fix-native-dev-setup design.md D8: Parse, ToConfiguration and Apply, each
// testable alone.
public class DevDotEnvOverlayTests
{
    // Parse

    [Fact]
    public void SkipsBlankAndCommentLines()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "", "   ", "# a comment", "FOO=bar" });

        Assert.Equal(new Dictionary<string, string> { ["FOO"] = "bar" }, result);
    }

    [Fact]
    public void StripsATrailingCommentAfterWhitespace()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "BACKEND_PORT=5050         # must match the redirect URI…" });

        Assert.Equal("5050", result["BACKEND_PORT"]);
    }

    [Fact]
    public void KeepsAHashWithNoPrecedingWhitespace()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "FOO=pa#ss" });

        Assert.Equal("pa#ss", result["FOO"]);
    }

    [Fact]
    public void StripsMatchingQuotesAndKeepsAHashInsideThem()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "DOUBLE=\"a # b\"", "SINGLE='x'" });

        Assert.Equal("a # b", result["DOUBLE"]);
        Assert.Equal("x", result["SINGLE"]);
    }

    [Fact]
    public void SplitsOnlyOnTheFirstEquals()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "a=b=c" });

        Assert.Equal("b=c", result["a"]);
    }

    [Fact]
    public void SkipsALineWithNoEquals()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "NOEQUALS", "FOO=bar" });

        Assert.Equal(new Dictionary<string, string> { ["FOO"] = "bar" }, result);
    }

    [Fact]
    public void TrimsWhitespaceAroundKeyAndValue()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "  FOO  =  bar  " });

        Assert.Equal("bar", result["FOO"]);
    }

    [Fact]
    public void TheLaterDuplicateWins()
    {
        var result = DevDotEnvOverlay.Parse(new[] { "FOO=first", "FOO=second" });

        Assert.Equal("second", result["FOO"]);
    }

    // ToConfiguration

    private static Dictionary<string, string> FullEnvExample() => new()
    {
        ["MAL_CLIENT_ID"] = "client-id",
        ["MAL_CLIENT_SECRET"] = "client-secret",
        ["BACKEND_PORT"] = "5050",
        ["FRONTEND_PORT"] = "5173",
        ["POSTGRES_DB"] = "animetracker",
        ["POSTGRES_USER"] = "animetracker",
        ["POSTGRES_PASSWORD"] = "devlocalpassword",
        ["POSTGRES_PORT"] = "5434",
    };

    [Fact]
    public void TheFullEnvExampleKeySetGivesExactlyTheMappedKeys()
    {
        var result = DevDotEnvOverlay.ToConfiguration(FullEnvExample());

        Assert.Equal(
            new[] { "ConnectionStrings:Default", "Mal:CallbackPort", "Mal:ClientId", "Mal:ClientSecret", "Mal:FrontendPort", "urls" },
            result.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("http://localhost:5050", result["urls"]);
    }

    [Fact]
    public void TheConnectionStringRoundTripsHostPortDatabaseUsernameAndPassword()
    {
        var result = DevDotEnvOverlay.ToConfiguration(FullEnvExample());

        var connectionString = new NpgsqlConnectionStringBuilder(result["ConnectionStrings:Default"]);
        Assert.Equal("localhost", connectionString.Host);
        Assert.Equal(5434, connectionString.Port);
        Assert.Equal("animetracker", connectionString.Database);
        Assert.Equal("animetracker", connectionString.Username);
        Assert.Equal("devlocalpassword", connectionString.Password);
    }

    [Fact]
    public void NoPostgresPortGivesPort5432()
    {
        var env = FullEnvExample();
        env.Remove("POSTGRES_PORT");

        var result = DevDotEnvOverlay.ToConfiguration(env);

        var connectionString = new NpgsqlConnectionStringBuilder(result["ConnectionStrings:Default"]);
        Assert.Equal(5432, connectionString.Port);
    }

    [Fact]
    public void APasswordContainingSemicolonAndEqualsRoundTrips()
    {
        var env = FullEnvExample();
        env["POSTGRES_PASSWORD"] = "pa;ss=word";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        var connectionString = new NpgsqlConnectionStringBuilder(result["ConnectionStrings:Default"]);
        Assert.Equal("pa;ss=word", connectionString.Password);
    }

    [Theory]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("POSTGRES_USER")]
    [InlineData("POSTGRES_DB")]
    public void AMissingPostgresKeyGivesNoConnectionString(string key)
    {
        var env = FullEnvExample();
        env.Remove(key);

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("ConnectionStrings:Default"));
    }

    [Theory]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("POSTGRES_USER")]
    [InlineData("POSTGRES_DB")]
    public void AnEmptyPostgresKeyGivesNoConnectionString(string key)
    {
        var env = FullEnvExample();
        env[key] = "";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("ConnectionStrings:Default"));
    }

    [Fact]
    public void AnEmptyMalClientIdGivesNoKey()
    {
        var env = FullEnvExample();
        env["MAL_CLIENT_ID"] = "";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Mal:ClientId"));
    }

    [Fact]
    public void ATmdbApiKeyMapsToTheTmdbOption()
    {
        var env = FullEnvExample();
        env["TMDB_API_KEY"] = "tmdb-key";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.Equal("tmdb-key", result["Tmdb:ApiKey"]);
    }

    [Fact]
    public void AnEmptyTmdbApiKeyGivesNoKey()
    {
        var env = FullEnvExample();
        env["TMDB_API_KEY"] = "";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Tmdb:ApiKey"));
    }

    [Fact]
    public void AnAbsentTmdbApiKeyGivesNoKey()
    {
        var env = FullEnvExample();
        Assert.False(env.ContainsKey("TMDB_API_KEY"));

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Tmdb:ApiKey"));
    }

    [Fact]
    public void AnEmptyBackendPortGivesNoKey()
    {
        var env = FullEnvExample();
        env["BACKEND_PORT"] = "";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Mal:CallbackPort"));
        Assert.False(result.ContainsKey("urls"));
    }

    [Fact]
    public void ANonIntegerBackendPortThrowsNamingTheKeyButNotTheValue()
    {
        var env = FullEnvExample();
        env["BACKEND_PORT"] = "abc";

        var ex = Assert.Throws<InvalidOperationException>(() => DevDotEnvOverlay.ToConfiguration(env));

        Assert.Contains("BACKEND_PORT", ex.Message);
        Assert.DoesNotContain("abc", ex.Message);
    }

    // first-run-setup: FRONTEND_PORT is where the OAuth callback sends the
    // browser back to (deployment, "The backend is told the frontend's port").

    [Fact]
    public void AFrontendPortIsMappedToTheMalFrontendPortKey()
    {
        var env = FullEnvExample();
        env["FRONTEND_PORT"] = "5200";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.Equal("5200", result["Mal:FrontendPort"]);
    }

    [Fact]
    public void NoFrontendPortGivesNoKeySoTheDefault5173Applies()
    {
        var env = FullEnvExample();
        env.Remove("FRONTEND_PORT");

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Mal:FrontendPort"));
        Assert.Equal(5173, new MalOptions().FrontendPort);
    }

    [Fact]
    public void AnEmptyFrontendPortGivesNoKey()
    {
        var env = FullEnvExample();
        env["FRONTEND_PORT"] = "";

        var result = DevDotEnvOverlay.ToConfiguration(env);

        Assert.False(result.ContainsKey("Mal:FrontendPort"));
    }

    [Fact]
    public void ANonIntegerFrontendPortThrowsNamingTheKeyButNotTheValue()
    {
        var env = FullEnvExample();
        env["FRONTEND_PORT"] = "abc";

        var ex = Assert.Throws<InvalidOperationException>(() => DevDotEnvOverlay.ToConfiguration(env));

        Assert.Contains("FRONTEND_PORT", ex.Message);
        Assert.DoesNotContain("abc", ex.Message);
    }

    [Fact]
    public void ANonIntegerPostgresPortThrowsNamingTheKeyButNotTheValue()
    {
        var env = FullEnvExample();
        env["POSTGRES_PORT"] = "abc";

        var ex = Assert.Throws<InvalidOperationException>(() => DevDotEnvOverlay.ToConfiguration(env));

        Assert.Contains("POSTGRES_PORT", ex.Message);
        Assert.DoesNotContain("abc", ex.Message);
    }

    // Apply, against a real builder

    private const string SampleDotEnv =
        "MAL_CLIENT_ID=client-id\nPOSTGRES_DB=animetracker\nPOSTGRES_USER=animetracker\nPOSTGRES_PASSWORD=devlocalpassword\n";

    private static (WebApplicationBuilder Builder, string TempRoot) CreateBuilder(string environmentName, string? dotEnvContents)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "DevDotEnvOverlayTests_" + Guid.NewGuid());
        var contentRoot = Path.Combine(tempRoot, "backend", "AnimeTracker.Api");
        Directory.CreateDirectory(contentRoot);

        if (dotEnvContents is not null)
            File.WriteAllText(Path.Combine(tempRoot, ".env"), dotEnvContents);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ContentRootPath = contentRoot,
            Args = ["--hostBuilder:reloadConfigOnChange=false"],
        });

        return (builder, tempRoot);
    }

    private static void Cleanup(WebApplicationBuilder builder, string tempRoot)
    {
        (builder.Configuration as IDisposable)?.Dispose();
        Directory.Delete(tempRoot, recursive: true);
    }

    [Fact]
    public void DevelopmentWithADotEnvAddsASourceAndItsValues()
    {
        var (builder, tempRoot) = CreateBuilder("Development", SampleDotEnv);
        try
        {
            var sourceCountBefore = builder.Configuration.Sources.Count;

            DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);

            Assert.Equal(sourceCountBefore + 1, builder.Configuration.Sources.Count);
            Assert.Equal("client-id", builder.Configuration["Mal:ClientId"]);
            Assert.NotNull(builder.Configuration["ConnectionStrings:Default"]);
        }
        finally
        {
            Cleanup(builder, tempRoot);
        }
    }

    [Fact]
    public void ProductionWithTheSameDotEnvAddsNothing()
    {
        var (builder, tempRoot) = CreateBuilder("Production", SampleDotEnv);
        try
        {
            var sourceCountBefore = builder.Configuration.Sources.Count;

            DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);

            Assert.Equal(sourceCountBefore, builder.Configuration.Sources.Count);
            Assert.Null(builder.Configuration["Mal:ClientId"]);
            Assert.Null(builder.Configuration["ConnectionStrings:Default"]);
        }
        finally
        {
            Cleanup(builder, tempRoot);
        }
    }

    [Fact]
    public void DevelopmentWithNoDotEnvAddsNothingAndThrowsNothing()
    {
        var (builder, tempRoot) = CreateBuilder("Development", dotEnvContents: null);
        try
        {
            var sourceCountBefore = builder.Configuration.Sources.Count;

            DevDotEnvOverlay.Apply(builder.Configuration, builder.Environment);

            Assert.Equal(sourceCountBefore, builder.Configuration.Sources.Count);
        }
        finally
        {
            Cleanup(builder, tempRoot);
        }
    }
}
