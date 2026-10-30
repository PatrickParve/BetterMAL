namespace AnimeTracker.Api.Services.Games;

/// <summary>The Higher or Lower game, run on the server so its rules are tested and
/// a hidden score never reaches the browser before the pick
/// (add-games-higher-or-lower design.md D1). Every operation reads stored data
/// only: none makes a MyAnimeList or AniList request.</summary>
public interface IHigherOrLowerService
{
    /// <summary>The game in progress if there is one, the release years the
    /// eligible anime cover, and two posters of popular anime. The Hide NSFW
    /// setting shapes the years and posters; a game in progress keeps the setting
    /// it started with.</summary>
    Task<HigherOrLowerStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default);

    /// <summary>How many anime these settings leave, and whether that is enough to
    /// play. Throws <see cref="HigherOrLowerSettingsException"/> for settings that
    /// can't describe a game.</summary>
    Task<HigherOrLowerPoolDto> GetPoolAsync(HigherOrLowerSettings settings, CancellationToken ct = default);

    /// <summary>Starts a game, replacing any in progress, and returns its first
    /// round. Throws <see cref="HigherOrLowerSettingsException"/> for settings that
    /// can't describe a game and <see cref="HigherOrLowerNotPlayableException"/>
    /// for ones that leave fewer than two different scores; a refused start leaves
    /// a game in progress as it was.</summary>
    Task<HigherOrLowerGameDto> StartAsync(HigherOrLowerSettings settings, CancellationToken ct = default);

    /// <summary>Judges one pick and moves the game on. Throws
    /// <see cref="HigherOrLowerRunNotFoundException"/> for a game no longer in
    /// progress, <see cref="HigherOrLowerStaleRoundException"/> for a round the
    /// game has passed, and <see cref="HigherOrLowerCardNotOnTableException"/> for
    /// an anime that isn't one of the two cards.</summary>
    Task<HigherOrLowerPickDto> PickAsync(int runId, int round, int animeId, CancellationToken ct = default);

    /// <summary>Ends a game early and answers with its streak. Throws
    /// <see cref="HigherOrLowerRunNotFoundException"/> for a game no longer in
    /// progress.</summary>
    Task<HigherOrLowerEndDto> EndAsync(int runId, CancellationToken ct = default);
}
