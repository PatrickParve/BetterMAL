using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// Seeding shared by every test that needs mapped anime, series and cached
// TMDB sets: the artwork service's own tests, and the detail, series,
// controller and device-transfer tests that sit on top of it. Each builder
// only adds to the context it is given; the caller saves. Time passing is
// simulated by seeding FetchedAt relative to UtcNow, as the rest of this
// suite does.
internal static class TmdbTestData
{
    public const string ImageBase = "https://image.tmdb.org/t/p/original";

    /// <summary>A set fetched yesterday: well inside the 30 days a set stays fresh.</summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromDays(1);

    // custom: the entries of the custom id mapping (design.md D19), none unless
    // a test is about them.
    public static TmdbArtworkService ArtworkService(
        AnimeTrackerDbContext db, FakeTmdbClient client, string apiKey = "test-key", RefreshGate? gate = null,
        ICustomIdMappings? custom = null) =>
        new(db, client, gate ?? new RefreshGate(), Microsoft.Extensions.Options.Options.Create(new TmdbOptions { ApiKey = apiKey }),
            TestIdMappings.Resolver(db, custom));

    public static async Task SeedAsync(DbContextOptions<AnimeTrackerDbContext> options, Action<AnimeTrackerDbContext> seed)
    {
        using var db = new AnimeTrackerDbContext(options);
        seed(db);
        await db.SaveChangesAsync();
    }

    /// <summary>An anime, in my list unless said otherwise, with a mapping row
    /// unless <paramref name="withMapping"/> is off. Returns the anime so a
    /// test can adjust it (a fresh <c>LastSyncedAt</c>, say, to keep a detail
    /// read from live-fetching it).</summary>
    public static AnimeMetadata AddAnime(
        AnimeTrackerDbContext db, int animeId, bool inMyList = true,
        int? tvId = null, int? season = null, int[]? movieIds = null, string[]? imdbIds = null, bool withMapping = true)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" };
        db.AnimeMetadata.Add(anime);
        if (inMyList)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId });
        if (withMapping)
        {
            db.AnimeIdMappings.Add(new AnimeIdMapping
            {
                AnimeId = animeId,
                TmdbTvId = tvId,
                TmdbSeasonNumber = season,
                TmdbMovieIds = movieIds?.ToList() ?? [],
                ImdbIds = imdbIds?.ToList() ?? [],
            });
        }
        return anime;
    }

    /// <summary>A freshly built series whose members are already added as
    /// anime: the main line in the order given, then the extras. Every member
    /// is that anime's primary membership, so <c>FindSeriesIdAsync</c>
    /// resolves each of them to this series. Returns the series so a test can
    /// adjust it (a stored picture choice, say).</summary>
    public static AnimeTracker.Api.Models.Series AddSeries(
        AnimeTrackerDbContext db, int seriesId, int[] mainLine, params (int AnimeId, string? Group, int Order)[] extras)
    {
        var series = new AnimeTracker.Api.Models.Series { Id = seriesId, BuiltAt = DateTimeOffset.UtcNow };
        db.Series.Add(series);
        for (var i = 0; i < mainLine.Length; i++)
            db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = mainLine[i], IsMainLine = true, IsPrimary = true, Order = i });
        foreach (var (animeId, group, order) in extras)
            db.SeriesMembers.Add(new SeriesMember { SeriesId = seriesId, AnimeId = animeId, IsMainLine = false, IsPrimary = true, Order = order, RelationGroup = group });
        return series;
    }

    public static TmdbTvImageSet TvSet(int tvId, TimeSpan age, params (string Path, TmdbImageKind Kind, string? Language, int Position)[] images) => new()
    {
        TvId = tvId,
        FetchedAt = DateTimeOffset.UtcNow - age,
        Images = images.Select(i => new TmdbTvImage { TvId = tvId, FilePath = i.Path, Kind = i.Kind, Language = i.Language, Width = 2000, Height = 3000, Position = i.Position }).ToList(),
    };

    public static TmdbSeasonImageSet SeasonSet(int tvId, int season, TimeSpan age, params (string Path, TmdbImageKind Kind, string? Language, int Position)[] images) => new()
    {
        TvId = tvId,
        SeasonNumber = season,
        FetchedAt = DateTimeOffset.UtcNow - age,
        Images = images.Select(i => new TmdbSeasonImage { TvId = tvId, SeasonNumber = season, FilePath = i.Path, Kind = i.Kind, Language = i.Language, Width = 2000, Height = 3000, Position = i.Position }).ToList(),
    };

    public static TmdbMovieImageSet MovieSet(int movieId, TimeSpan age, params (string Path, TmdbImageKind Kind, string? Language, int Position)[] images) => new()
    {
        MovieId = movieId,
        FetchedAt = DateTimeOffset.UtcNow - age,
        Images = images.Select(i => new TmdbMovieImage { MovieId = movieId, FilePath = i.Path, Kind = i.Kind, Language = i.Language, Width = 2000, Height = 3000, Position = i.Position }).ToList(),
    };

    public static (string, TmdbImageKind, string?, int) Poster(string path, string? language, int position = 0) =>
        (path, TmdbImageKind.Poster, language, position);

    public static (string, TmdbImageKind, string?, int) Backdrop(string path, string? language, int position = 0) =>
        (path, TmdbImageKind.Backdrop, language, position);
}
