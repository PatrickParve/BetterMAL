using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.IdMapping;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// Shared by every test that builds a service which reads the id mapping, and by
// the custom-mapping tests themselves (design.md D19).

/// <summary>A custom mapping held in memory: no file, no clock. The entries can
/// be replaced between calls, as an edit of the real file would.</summary>
internal sealed class FakeCustomIdMappings : ICustomIdMappings
{
    public FakeCustomIdMappings(params CustomIdMapping[] entries) =>
        Current = entries.ToDictionary(entry => entry.Mapping.AnimeId);

    public IReadOnlyDictionary<int, CustomIdMapping> Current { get; set; }

    public string FilePath => "custom/id-mapping.json";

    /// <summary>A default entry: it only fills a group the synced mapping lacks.</summary>
    public static CustomIdMapping Fill(
        int animeId, int? tv = null, int? season = null, int[]? movies = null, string[]? imdb = null, string? note = null) =>
        Entry(animeId, false, tv, season, movies, imdb, note);

    /// <summary>An override: it applies whatever the synced mapping holds.</summary>
    public static CustomIdMapping Override(
        int animeId, int? tv = null, int? season = null, int[]? movies = null, string[]? imdb = null, string? note = null) =>
        Entry(animeId, true, tv, season, movies, imdb, note);

    private static CustomIdMapping Entry(
        int animeId, bool isOverride, int? tv, int? season, int[]? movies, string[]? imdb, string? note) =>
        new(new AnimeIdMapping
        {
            AnimeId = animeId,
            TmdbTvId = tv,
            TmdbSeasonNumber = tv is null ? null : season,
            TmdbMovieIds = [.. movies ?? []],
            ImdbIds = [.. imdb ?? []],
        }, isOverride, note);
}

internal static class TestIdMappings
{
    /// <summary>The resolver over <paramref name="db"/>, with no custom entries
    /// unless given, which is what every test that is not about the custom
    /// mapping wants.</summary>
    public static AnimeIdMappingResolver Resolver(AnimeTrackerDbContext db, ICustomIdMappings? custom = null) =>
        new(db, custom ?? new FakeCustomIdMappings());
}

/// <summary>Records every log line, so a test can check what was said and at
/// which level.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Add($"{logLevel}: {formatter(state, exception)}");
}
