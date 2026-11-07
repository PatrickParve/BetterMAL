using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Search;

// theme-search spec, "Nothing is asked of AnimeThemes or MyAnimeList" (improve-search-page task 3.8):
// the search reads what the app has stored, makes no request, and starts no background work.
public class ThemeSearchNoOutsideCallTests
{
    // The search path as Program.cs wires it, with a MyAnimeList client and an AnimeThemes client that
    // record any call made to them and refuse it. Were the search ever to ask either for anything (a
    // picture, a name, a song), the container would hand it the refusing one.
    private static ServiceProvider BuildProvider(string databaseName, out Tripwire mal, out Tripwire animeThemes)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IAnimeMetadataRepository, AnimeMetadataRepository>();
        services.AddSingleton<IAnimeSearchIndex, AnimeSearchIndexCache>();
        services.AddSingleton<IThemeSearchIndex, ThemeSearchIndex>();
        services.AddScoped<IThemeSearchService, ThemeSearchService>();
        services.AddSingleton(Tripwire.Create<IMalClient>(out mal));
        services.AddSingleton(Tripwire.Create<IAnimeThemesClient>(out animeThemes));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheSearchAnswersWithNeitherClientCalled()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var provider = BuildProvider(databaseName, out var mal, out var animeThemes);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            StoredThemes.MarkSynced(db);
            db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Kimetsu no Yaiba", EnglishTitle = "Demon Slayer", PictureUrl = "https://mal/1.jpg", PopularityRank = 3 });
            db.AnimeThemes.AddRange(
                StoredThemes.Row(1, 10, songTitle: "Gurenge", artists: ["LiSA"], animeName: "Kimetsu no Yaiba"),
                StoredThemes.Row(2, 20, songTitle: "Blue Bird", artists: ["Ikimonogakari"], animeName: "Naruto Shippuuden"),
                StoredThemes.Row(3, 30, songTitle: "Nameless", animeName: null));
            await db.SaveChangesAsync();
        }

        // A cold start, a warm one, every kind of query, and a quoted one.
        foreach (var query in new[] { "gurenge", "lisa", "demon slayer", "naruto", "\"blue bird\"", "nothing matches this", "!!!", "" })
        {
            using var scope = provider.CreateScope();
            var response = await scope.ServiceProvider.GetRequiredService<IThemeSearchService>().SearchAsync(query, 5);
            Assert.True(response.Ready);
        }

        using (var scope = provider.CreateScope())
        {
            var found = await scope.ServiceProvider.GetRequiredService<IThemeSearchService>().SearchAsync("gurenge", 5);
            var result = Assert.Single(found.Results);
            Assert.Equal("Kimetsu no Yaiba", result.Title); // it answered, from what is stored
            Assert.Equal("https://mal/1.jpg", result.PictureUrl);
        }

        Assert.Empty(mal.Calls);
        Assert.Empty(animeThemes.Calls);
    }

    [Fact]
    public async Task TheTripwiresReallyRecordAndRefuseACall()
    {
        // The control for the test above: a call to either would be seen, and would fail the search.
        using var provider = BuildProvider(Guid.NewGuid().ToString(), out var mal, out var animeThemes);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetRequiredService<IAnimeThemesClient>().GetPageAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetRequiredService<IMalClient>().SearchAnimeAsync("naruto"));

        Assert.Equal(["IAnimeThemesClient.GetPageAsync"], animeThemes.Calls);
        Assert.Equal(["IMalClient.SearchAnimeAsync"], mal.Calls);
    }

    [Theory]
    [InlineData(typeof(ThemeSearchService))]
    [InlineData(typeof(ThemeSearchIndex))]
    [InlineData(typeof(ThemeSearchController))]
    public void TheSearchIsGivenNoClientAndNoWayToStartBackgroundWork(Type type)
    {
        var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();

        Assert.DoesNotContain(dependencies, t =>
            t == typeof(IMalClient)
            || t == typeof(IAnimeThemesClient)
            || t == typeof(IAnimeThemesMediaRelay)
            || t == typeof(IAnimeThemesCatalogueSyncService)
            || t == typeof(ISeriesBuildTrigger)
            || t == typeof(HttpClient)
            || t == typeof(IHttpClientFactory));
    }
}
