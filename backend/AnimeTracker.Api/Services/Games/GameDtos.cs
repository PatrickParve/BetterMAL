namespace AnimeTracker.Api.Services.Games;

/// <summary>The years the eligible anime cover, for the setup screen's From and
/// To choices.</summary>
public record GameYearsDto(int Min, int Max);

/// <summary>How many anime some settings leave, and whether that is enough to
/// play. What is enough is each game's own rule: Higher or Lower needs two
/// different scores, Guess the Anime a single anime.</summary>
public record GamePoolDto(int Count, bool Playable);
