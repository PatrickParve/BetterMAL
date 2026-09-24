using AnimeTracker.Api.Services.IdMapping;

namespace AnimeTracker.Api.Tests.Services.IdMapping;

// external-id-mapping spec "A custom mapping file supplements and corrects the
// synced mapping" (design.md D19): the file reader. Real files in a temporary
// folder, since reading, tolerating and re-reading a file is the behaviour under
// test. Every access re-checks the file (a zero interval) unless a test is about
// the interval, and each write stamps a distinct modification time, so a test
// never depends on the file system's timestamp granularity or on sleeping.
public sealed class CustomIdMappingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "bettermal-custom-" + Guid.NewGuid().ToString("N"));
    private readonly CapturingLogger<CustomIdMappings> logger = new();
    private int writes;

    public CustomIdMappingsTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string FilePath => Path.Combine(folder, "id-mapping.json");

    private void Write(string json)
    {
        File.WriteAllText(FilePath, json);
        File.SetLastWriteTimeUtc(FilePath, new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc).AddSeconds(++writes));
    }

    private CustomIdMappings Create(TimeSpan? interval = null) =>
        new([FilePath], logger, interval ?? TimeSpan.Zero);

    private bool Said(string level, string text) =>
        logger.Lines.Any(line => line.StartsWith(level + ":", StringComparison.Ordinal) && line.Contains(text, StringComparison.Ordinal));

    private const string FalseMemory = """
        { "mal_id": 60568, "themoviedb_id": { "tv": 280564 }, "season": { "tmdb": 1 }, "imdb_id": ["tt38754770"],
          "note": "False Memory (2026)" }
        """;

    private const string FateZeroTwo = """
        { "mal_id": 11741, "themoviedb_id": { "tv": 45845 }, "season": { "tmdb": 2 }, "override": true }
        """;

    // --- Reading ---

    [Fact]
    public void AMissingFileMeansNoEntriesAndNoComplaint()
    {
        var mappings = Create();

        Assert.Empty(mappings.Current);
        Assert.Empty(logger.Lines);
    }

    [Fact]
    public void EntriesAreReadInTheSourceFilesShapeWithTheirTwoExtraFields()
    {
        Write($"[{FalseMemory}, {FateZeroTwo}]");

        var entries = Create().Current;

        Assert.Equal(2, entries.Count);
        var fill = entries[60568];
        Assert.False(fill.Override);
        Assert.Equal("False Memory (2026)", fill.Note);
        Assert.Equal(280564, fill.Mapping.TmdbTvId);
        Assert.Equal(1, fill.Mapping.TmdbSeasonNumber);
        Assert.Equal(["tt38754770"], fill.Mapping.ImdbIds);
        var correction = entries[11741];
        Assert.True(correction.Override);
        Assert.Null(correction.Note);
        Assert.Equal(2, correction.Mapping.TmdbSeasonNumber);
        Assert.True(Said("Information", "2 entries (1 fill, 1 override)"));
    }

    [Fact]
    public void MovieIdsAreReadAsAList()
    {
        Write("""[{ "mal_id": 40456, "themoviedb_id": { "movie": [635302, 635303] } }]""");

        Assert.Equal([635302, 635303], Create().Current[40456].Mapping.TmdbMovieIds);
    }

    [Fact]
    public void CommentsAndTrailingCommasAreAllowedBecauseAPersonEditsTheFile()
    {
        Write("""
            [
              // False Memory (2026): the source lists it with no ids.
              { "mal_id": 60568, "themoviedb_id": { "tv": 280564, }, "imdb_id": ["tt38754770",], },
            ]
            """);

        Assert.Equal(280564, Create().Current[60568].Mapping.TmdbTvId);
    }

    // --- An entry that is wrong loses only itself ---

    [Fact]
    public void AnEntryWithoutAMalIdIsSkippedAndTheOthersApply()
    {
        Write($$"""[{ "themoviedb_id": { "tv": 1 } }, {{FalseMemory}}]""");

        var entries = Create().Current;

        Assert.Equal([60568], entries.Keys);
        Assert.True(Said("Warning", "entry #1"));
        Assert.True(Said("Warning", "no positive mal_id"));
    }

    [Fact]
    public void AnEntryThatGivesNothingIsSkippedAndSaid()
    {
        Write($$"""[{ "mal_id": 5 }, {{FalseMemory}}]""");

        Assert.Equal([60568], Create().Current.Keys);
        Assert.True(Said("Warning", "MAL id 5 gives no TMDB id and no valid IMDb id"));
    }

    [Fact]
    public void AnEntryOfTheWrongShapeIsSkippedAndSaid()
    {
        // themoviedb_id was once a bare number upstream: it is refused here as it is there.
        Write($$"""[{ "mal_id": 5, "themoviedb_id": 123 }, {{FalseMemory}}]""");

        Assert.Equal([60568], Create().Current.Keys);
        Assert.True(Said("Warning", "entry #1"));
    }

    [Fact]
    public void AnImdbIdThatIsNotTtAndDigitsIsDroppedAndSaid()
    {
        Write("""[{ "mal_id": 5, "themoviedb_id": { "tv": 1 }, "imdb_id": ["tt123", "12345"] }]""");

        Assert.Equal(["tt123"], Create().Current[5].Mapping.ImdbIds);
        Assert.True(Said("Warning", "'12345' is not an IMDb id"));
    }

    [Fact]
    public void ASeasonWithoutATvIdIsIgnoredAndSaid()
    {
        Write("""[{ "mal_id": 5, "themoviedb_id": { "movie": [9] }, "season": { "tmdb": 2 } }]""");

        Assert.Null(Create().Current[5].Mapping.TmdbSeasonNumber);
        Assert.True(Said("Warning", "the season is ignored because it has no TMDB TV id"));
    }

    [Fact]
    public void ARepeatedMalIdKeepsTheFirstEntryAndSaysSo()
    {
        Write("""
            [
              { "mal_id": 5, "themoviedb_id": { "tv": 1 } },
              { "mal_id": 5, "themoviedb_id": { "tv": 2 } }
            ]
            """);

        Assert.Equal(1, Create().Current[5].Mapping.TmdbTvId);
        Assert.True(Said("Warning", "MAL id 5 already has an entry"));
    }

    // --- A file that cannot be read keeps the last good entries ---

    [Fact]
    public void ASyntaxErrorKeepsTheLastGoodEntriesAndNamesTheProblem()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();
        Assert.Single(mappings.Current);

        Write($"[{FalseMemory}, {{ \"mal_id\": 5, ");   // saved half-typed

        Assert.Equal([60568], mappings.Current.Keys);   // the typo did not switch anything off
        Assert.True(Said("Error", FilePath));
        Assert.True(Said("Error", "1 previously read entries stay in force"));
    }

    [Fact]
    public void ARootThatIsNotAnArrayKeepsTheLastGoodEntries()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();
        Assert.Single(mappings.Current);

        Write("""{ "mal_id": 60568 }""");

        Assert.Equal([60568], mappings.Current.Keys);
        Assert.True(Said("Error", "must hold a JSON array"));
    }

    [Fact]
    public void FixingTheFileAfterATypoAppliesTheNewEntries()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();
        Assert.Single(mappings.Current);
        Write("[ oops");
        Assert.Single(mappings.Current);

        Write($"[{FalseMemory}, {FateZeroTwo}]");

        Assert.Equal(2, mappings.Current.Count);
    }

    [Fact]
    public void AFileBrokenFromTheStartHasNoEntriesAndSaysSo()
    {
        Write("not json at all");

        Assert.Empty(Create().Current);
        Assert.True(Said("Error", "0 previously read entries"));
    }

    [Fact]
    public void ABrokenFileIsReportedOnceNotOnEveryLook()
    {
        Write("[ oops");
        var mappings = Create();

        _ = mappings.Current;
        _ = mappings.Current;
        _ = mappings.Current;

        Assert.Single(logger.Lines, line => line.StartsWith("Error:", StringComparison.Ordinal));
    }

    // --- Changes on disk ---

    [Fact]
    public void AnEditAppliesWithoutARestart()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();
        Assert.Single(mappings.Current);

        Write($"[{FalseMemory}, {FateZeroTwo}]");

        Assert.Equal(2, mappings.Current.Count);
    }

    [Fact]
    public void AnUnchangedFileIsNotReadAgain()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();

        _ = mappings.Current;
        _ = mappings.Current;
        _ = mappings.Current;

        Assert.Single(logger.Lines, line => line.Contains("loaded from", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFileIsNotLookedAtAgainWithinTheInterval()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create(interval: TimeSpan.FromHours(1));
        Assert.Single(mappings.Current);

        Write($"[{FalseMemory}, {FateZeroTwo}]");

        Assert.Single(mappings.Current); // an edit within the interval is seen after it, not before
    }

    [Fact]
    public void AFileThatAppearsLaterIsPickedUp()
    {
        var mappings = Create();
        Assert.Empty(mappings.Current);

        Write($"[{FalseMemory}]");

        Assert.Single(mappings.Current);
    }

    [Fact]
    public void AFileThatDisappearsTakesItsEntriesWithIt()
    {
        Write($"[{FalseMemory}]");
        var mappings = Create();
        Assert.Single(mappings.Current);

        File.Delete(FilePath);

        Assert.Empty(mappings.Current);
        Assert.True(Said("Information", "is gone"));
    }

    // --- Where the file is looked for ---

    [Fact]
    public void AConfiguredFileIsTheOnlyCandidate()
    {
        var candidates = CustomIdMappings.CandidatePaths("elsewhere/mapping.json", "/work/dir");

        Assert.Equal([Path.GetFullPath("elsewhere/mapping.json", "/work/dir")], candidates);
    }

    [Fact]
    public void WithNothingConfiguredTheWorkingDirectoryAndTheTwoAboveAreSearched()
    {
        var work = Path.Combine(Path.GetTempPath(), "repo", "backend", "AnimeTracker.Api");

        var candidates = CustomIdMappings.CandidatePaths(null, work);

        var expected = new[]
        {
            Path.Combine(work, "custom", "id-mapping.json"),
            Path.Combine(Path.GetTempPath(), "repo", "backend", "custom", "id-mapping.json"),
            Path.Combine(Path.GetTempPath(), "repo", "custom", "id-mapping.json"),
        };
        Assert.Equal(expected.Select(path => Path.GetFullPath(path)).ToList(), candidates.Select(path => Path.GetFullPath(path)).ToList());
    }

    [Fact]
    public void TheFirstExistingCandidateIsTheFileAndTheFirstOneIsTheFileToBeCreated()
    {
        var first = Path.Combine(folder, "a", "id-mapping.json");
        var second = Path.Combine(folder, "b", "id-mapping.json");
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);

        Assert.Equal(first, new CustomIdMappings([first, second], logger).FilePath); // none exists yet

        File.WriteAllText(second, "[]");
        Assert.Equal(second, new CustomIdMappings([first, second], logger).FilePath);
    }
}
