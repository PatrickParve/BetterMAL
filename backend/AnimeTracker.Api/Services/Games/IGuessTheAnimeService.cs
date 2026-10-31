namespace AnimeTracker.Api.Services.Games;

/// <summary>The Guess the Anime game, run on the server so its rules are tested and
/// the answer never reaches the browser before the game ends
/// (add-guess-the-anime design.md D1). Unlike Higher or Lower it can call
/// MyAnimeList, to fetch an anime's missing stats when the game needs them
/// (design D6): starting a game, and making a guess.</summary>
public interface IGuessTheAnimeService
{
    /// <summary>The game in progress if there is one, with its guesses compared
    /// afresh, the release years the eligible anime cover, and two posters of
    /// popular anime. Stored data only. The answer is nowhere in it. The Hide NSFW
    /// setting shapes the years and posters; a game in progress keeps the setting
    /// it started with.</summary>
    Task<GuessTheAnimeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default);

    /// <summary>How many anime these settings leave, and whether that is enough to
    /// play, which is any at all. Throws <see cref="GameSettingsException"/> for
    /// settings that can't describe a game.</summary>
    Task<GamePoolDto> GetPoolAsync(GameSettings settings, CancellationToken ct = default);

    /// <summary>Draws an answer, fetching what it is missing, and starts a game,
    /// replacing any in progress. Throws <see cref="GameSettingsException"/> for
    /// settings that can't describe a game,
    /// <see cref="GuessTheAnimeNotPlayableException"/> for ones that leave no anime
    /// and <see cref="GuessTheAnimeNoAnswerException"/> when no answer could be
    /// settled. The whole draw comes before anything is changed, so a refused start
    /// leaves a game in progress as it was.</summary>
    Task<GuessTheAnimeGameDto> StartAsync(GameSettings settings, CancellationToken ct = default);

    /// <summary>Compares one guess with the answer and moves the game on. Throws
    /// <see cref="GuessTheAnimeRunNotFoundException"/> for a game no longer in
    /// progress, <see cref="GuessTheAnimeStaleGuessException"/> for a number the
    /// game has passed, <see cref="GuessTheAnimeAlreadyGuessedException"/> for an
    /// anime already guessed and <see cref="GuessTheAnimeDetailsUnavailableException"/>
    /// when the anime's details couldn't be fetched, which counts nothing.</summary>
    Task<GuessTheAnimeGuessDto> GuessAsync(int runId, int number, int animeId, CancellationToken ct = default);

    /// <summary>Ends a game early and answers with the answer. Throws
    /// <see cref="GuessTheAnimeRunNotFoundException"/> for a game no longer in
    /// progress.</summary>
    Task<GuessTheAnimeEndDto> EndAsync(int runId, CancellationToken ct = default);
}
