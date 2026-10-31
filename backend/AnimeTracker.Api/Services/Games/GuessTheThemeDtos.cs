using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

// What Guess the Opening/Ending sends and receives (add-anime-themes-and-guess-the-theme
// design.md D7). Enums are serialised by name, like every enum on the wire.
//
// The rule behind every shape here: until a song is revealed, nothing in a response names
// it or its anime, not even an id. The anime, the title, the label, the picture and the
// video exist only in a reveal.

/// <summary>Where a move left the game. A wrong guess or a skip below the last length is
/// <see cref="Wrong"/> or <see cref="Skipped"/> and the game goes on; a right guess is
/// <see cref="Right"/> and the game goes on to a reveal; <see cref="Lost"/> is a wrong guess
/// or a skip at the last length; <see cref="Cleared"/> is a right guess, or a replacement
/// that found none, with no song left to draw.</summary>
public enum GuessTheThemeOutcome
{
    Wrong,
    Skipped,
    Right,
    Lost,
    Cleared,
}

/// <summary>An anime I guessed wrong this round, for the round's list. Only a stored anime
/// has one; a guess of one the app hasn't stored keeps just its id.</summary>
public record GuessTheThemeCardDto(int AnimeId, string Title, string? EnglishTitle, string? PictureUrl);

/// <summary>What a song was. <see cref="HeardSeconds"/> is the length at which it was
/// guessed, and null when it wasn't: a loss, or giving up. It is in a response only from a
/// right guess, a loss, End game or a game read during a reveal. <see cref="PlayUrl"/> is the app's own
/// address for the video, which starts at once from what has been read ahead; <see cref="VideoUrl"/> is
/// the file itself, for when that can't be played.</summary>
public record GuessTheThemeRevealDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    AnimeThemeKind Kind,
    string Label,
    string? SongTitle,
    List<string> Artists,
    string VideoUrl,
    bool InMyList,
    double? HeardSeconds,
    string PlayUrl);

/// <summary>A game in progress. <see cref="Move"/> is the number every move sends back
/// (the concurrency token); <see cref="Lengths"/> is the ladder, sent so it lives in one
/// place; <see cref="Round"/> names the song in the clip's address and is shown nowhere.
/// <see cref="Reveal"/> is set only in the <see cref="GuessTheThemePhase.Revealed"/> phase,
/// so a reload during a reveal shows it again, which gives nothing away.
/// <see cref="NextRound"/> is the song already drawn for after the one being played, as a
/// number and nothing more, so the board can load its clip while the round is listened to
/// and not wait for it at Next song. It is null when no further song could be drawn.
/// <see cref="GuessedIds"/> are the anime
/// guessed wrong this round, which stay out of the dropdown; <see cref="Guessed"/> is
/// the ones of them that are stored.</summary>
public record GuessTheThemeGameDto(
    int Id,
    int Move,
    IReadOnlyList<double> Lengths,
    int Round,
    int Step,
    GuessTheThemePhase Phase,
    GuessTheThemeRevealDto? Reveal,
    int? NextRound,
    List<int> GuessedIds,
    List<GuessTheThemeCardDto> Guessed);

/// <summary>What opening either page needs: the game in progress if any, the years to
/// offer, two posters of popular anime for the Games tile and the setup screen, and
/// whether a catalogue refresh has ever finished, so the setup screen can say the songs
/// are on their way rather than that there are too few anime.</summary>
public record GuessTheThemeStateDto(
    GuessTheThemeGameDto? Game, GameYearsDto? Years, List<string> Pictures, bool CatalogueReady);

/// <summary>A guess. <see cref="Move"/> does the job a round number does in the other games:
/// a double press, or a tab behind the game, fails on it.</summary>
public record GuessTheThemeGuessRequest(int Move, int AnimeId);

/// <summary>A skip, which is a guess of nothing.</summary>
public record GuessTheThemeSkipRequest(int Move);

/// <summary>Next song, from a reveal.</summary>
public record GuessTheThemeNextRequest(int Move);

/// <summary>Replace the song of this round, whose file has gone.</summary>
public record GuessTheThemeReplaceRequest(int Round);

/// <summary>What a guess or a skip did. <see cref="Game"/> is the game after it where it
/// goes on (<c>Wrong</c>, <c>Skipped</c>, <c>Right</c>) and null where it has ended.
/// <see cref="Reveal"/> is set where a song is shown: after a right guess it is also in the
/// game, so it is set here only when the game has ended (<c>Lost</c>, <c>Cleared</c>).</summary>
public record GuessTheThemeMoveDto(
    GuessTheThemeOutcome Outcome, GuessTheThemeGameDto? Game, GuessTheThemeRevealDto? Reveal);

/// <summary>A game ended with End game. <see cref="Reveal"/> is the answer when it was
/// ended during a round, and null during a reveal, where it is already on screen.</summary>
public record GuessTheThemeEndDto(GuessTheThemeRevealDto? Reveal);

/// <summary>A replaced song: the game on the new song, or <see cref="GuessTheThemeOutcome.Cleared"/>
/// with no game where no other song could be drawn.</summary>
public record GuessTheThemeReplaceDto(GuessTheThemeOutcome? Outcome, GuessTheThemeGameDto? Game);
