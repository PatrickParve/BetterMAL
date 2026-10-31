using System.Net;
using System.Reflection;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Controllers;

// add-guess-the-anime task 1.4: the type-ahead route's two optional parameters.
// Query binding only happens in a real pipeline, so these go over HTTP to the
// controller alone, on Kestrel in the process, with a fake service behind it.
public class AnimeSearchControllerTests
{
    [Fact]
    public async Task WithNeitherParameterTheSearchIsWhatItWas()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/anime/search?q=gundam");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("gundam", 5, true, true), fake.Call);
    }

    [Fact]
    public async Task TheLocalStageStillSkipsTheLiveSearch()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/anime/search?q=gundam&stage=local");

        Assert.Equal(("gundam", 5, false, true), fake.Call);
    }

    [Fact]
    public async Task SeriesFalseLeavesSeriesOut()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/anime/search?q=demon&series=false");

        Assert.Equal(("demon", 5, true, false), fake.Call);
    }

    [Fact]
    public async Task SeriesTrueIsTheDefault()
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/anime/search?q=demon&series=true");

        Assert.Equal(("demon", 5, true, true), fake.Call);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(60, 60)]
    [InlineData(61, 60)]
    [InlineData(500, 60)]
    [InlineData(0, 1)]
    [InlineData(-4, 1)]
    public async Task TheLimitIsClampedToSixty(int asked, int used)
    {
        var fake = new FakeSearch();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync($"api/anime/search?q=demon&series=false&limit={asked}");

        Assert.Equal(used, fake.Call?.Limit);
    }

    private sealed class FakeSearch : IAnimeSearchService
    {
        public (string Query, int Limit, bool IncludeLive, bool IncludeSeries)? Call { get; private set; }

        public Task<List<AnimeSearchResultDto>> SearchAsync(
            string query, int limit, bool includeLive, bool includeSeries = true, CancellationToken ct = default)
        {
            Call = (query, limit, includeLive, includeSeries);
            return Task.FromResult(new List<AnimeSearchResultDto>());
        }

        public Task<SearchPageDto> SearchPageAsync(
            string query, string sortKey, int offset, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    /// <summary>Only the one controller, so no other route or dependency is
    /// involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "AnimeSearchController";
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

        public static async Task<TestHost> StartAsync(IAnimeSearchService service)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(service);
            builder.Services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(AnimeSearchController)));
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
