using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Controllers;

// improve-search-page task 3.7: GET api/themes/search. Query binding only happens in a real
// pipeline, so these go over HTTP to the controller alone, on Kestrel in the process.
public class ThemeSearchControllerTests
{
    // --- Binding and the clamp, over a fake service ---

    [Fact]
    public async Task WithNoLimitTheTypeAheadsFiveAreAsked()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(services => services.AddSingleton<IThemeSearchService>(fake));

        var response = await host.Client.GetAsync("api/themes/search?q=gurenge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("gurenge", 5), fake.Call);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(5000, 200)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    public async Task TheLimitIsClampedToOneThroughTwoHundred(int asked, int used)
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(services => services.AddSingleton<IThemeSearchService>(fake));

        await host.Client.GetAsync($"api/themes/search?q=love&limit={asked}");

        Assert.Equal(used, fake.Call?.Limit);
    }

    [Fact]
    public async Task NoQueryAsksTheServiceForAnEmptyOne()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(services => services.AddSingleton<IThemeSearchService>(fake));

        await host.Client.GetAsync("api/themes/search");

        Assert.Equal(("", 5), fake.Call);
    }

    [Fact]
    public async Task AQuotedQueryReachesTheServiceWithItsQuotes()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(services => services.AddSingleton<IThemeSearchService>(fake));

        await host.Client.GetAsync("api/themes/search?q=%22blue%20bird%22");

        Assert.Equal(("\"blue bird\"", 5), fake.Call);
    }

    // --- Over the real service and an in-memory catalogue ---

    [Fact]
    public async Task AnEmptyQueryAnswersReadyWithNoResults()
    {
        await using var host = await StartRealAsync(seed: db =>
        {
            StoredThemes.MarkSynced(db);
            db.AnimeThemes.Add(StoredThemes.Row(1, 10, songTitle: "Blue Bird", animeName: "Show"));
        });

        foreach (var url in new[] { "api/themes/search", "api/themes/search?q=", "api/themes/search?q=%20%20", "api/themes/search?q=!!!" })
        {
            using var body = await GetJsonAsync(host, url);

            Assert.True(body.RootElement.GetProperty("ready").GetBoolean(), url);
            Assert.Equal(0, body.RootElement.GetProperty("results").GetArrayLength());
        }
    }

    [Fact]
    public async Task BeforeTheCatalogueHasLoadedTheAnswerSaysNotReady()
    {
        await using var host = await StartRealAsync(seed: db =>
            db.AnimeThemes.Add(StoredThemes.Row(1, 10, songTitle: "Blue Bird", animeName: "Show")));

        using var body = await GetJsonAsync(host, "api/themes/search?q=blue");

        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Equal(0, body.RootElement.GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task AResultCarriesTheAnimeAndExactlyTheThemeThePagesShow()
    {
        await using var host = await StartRealAsync(seed: db =>
        {
            StoredThemes.MarkSynced(db);
            db.AnimeThemes.Add(StoredThemes.Row(
                5, 50, AnimeThemeKind.Ending, 2, songTitle: "Gurenge", artists: ["LiSA"], episodes: "1-12", nsfw: true, animeName: "Kimetsu"));
        });

        using var body = await GetJsonAsync(host, "api/themes/search?q=gurenge");

        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        var result = Assert.Single(body.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal(5, result.GetProperty("animeId").GetInt32());
        Assert.Equal("Kimetsu", result.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("englishTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("pictureUrl").ValueKind);
        var theme = result.GetProperty("theme");
        Assert.Equal(50, theme.GetProperty("id").GetInt32());
        Assert.Equal("Ending", theme.GetProperty("kind").GetString()); // as the pages' themes send it
        Assert.Equal("ED2", theme.GetProperty("label").GetString());
        Assert.Equal("Gurenge", theme.GetProperty("songTitle").GetString());
        Assert.Equal(["LiSA"], theme.GetProperty("artists").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal("1-12", theme.GetProperty("episodes").GetString());
        Assert.True(theme.GetProperty("nsfw").GetBoolean());
        Assert.Equal("https://v.animethemes.moe/T50.webm", theme.GetProperty("videoUrl").GetString());
    }

    private static async Task<JsonDocument> GetJsonAsync(TestHost host, string url)
    {
        var response = await host.Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<TestHost> StartRealAsync(Action<AnimeTrackerDbContext> seed)
    {
        var databaseName = Guid.NewGuid().ToString();
        var host = await TestHost.StartAsync(services =>
        {
            services.AddDbContext<AnimeTrackerDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<IAnimeSearchIndex>(new FakeAnimeSearchIndex());
            services.AddSingleton<IThemeSearchIndex, ThemeSearchIndex>();
            services.AddScoped<IThemeSearchService, ThemeSearchService>();
        });

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }

        return host;
    }

    private sealed class FakeSearch : IThemeSearchService
    {
        public (string Query, int Limit)? Call { get; private set; }

        public Task<ThemeSearchResponseDto> SearchAsync(string query, int limit, CancellationToken ct = default)
        {
            Call = (query, limit);
            return Task.FromResult(new ThemeSearchResponseDto(true, []));
        }
    }

    /// <summary>Only the one controller, so no other route or dependency is involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "ThemeSearchController";
        public IEnumerable<TypeInfo> Types => types.Select(t => t.GetTypeInfo());
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }
        public IServiceProvider Services => _app.Services;

        public static async Task<TestHost> StartAsync(Action<IServiceCollection> register)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            register(builder.Services);
            builder.Services.AddControllers()
                // As Program.cs sends them: an enum travels as its name.
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(ThemeSearchController)));
                });

            var app = builder.Build();
            app.MapControllers();
            await app.StartAsync();

            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First().TrimEnd('/') + "/") };
            return new TestHost(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
