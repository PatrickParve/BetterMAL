using System.Linq.Expressions;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

/// <summary>What Guess the Anime knows about one anime: its picture and title and
/// the seven stats a guess is compared on. It is the input to
/// <see cref="GuessComparison.Compare"/>, so the rules are a pure function of two
/// of these and the client only draws (add-guess-the-anime design.md D8). A stat
/// is null when it isn't stored; <see cref="Studios"/> null means studios were
/// never given and empty means MyAnimeList credits none.
///
/// <see cref="Episodes"/> is the total MyAnimeList gives, or, for a show it gives
/// none, how many episodes have aired (One Piece): the service fills that in after
/// the read, since the aired count is not a column.</summary>
public record AnimeStats(
    int Id,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    DateOnly? AiredFrom,
    List<string>? Genres,
    int? Episodes,
    List<string>? Studios,
    string? MediaType,
    double? MalScore)
{
    /// <summary>The columns above, for a query to project to.</summary>
    public static readonly Expression<Func<AnimeMetadata, AnimeStats>> Select = a => new AnimeStats(
        a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.AiredFrom, a.Genres, a.TotalEpisodes, a.Studios,
        a.MediaType, a.MalScore);

    private static readonly Func<AnimeMetadata, AnimeStats> Project = Select.Compile();

    public static AnimeStats Of(AnimeMetadata anime) => Project(anime);

    /// <summary>An anime a game can't use as it is stored: it has no start date,
    /// episode count, genres, studio list, type or MAL score (add-guess-the-anime
    /// design.md D6). Genres count as missing when empty, since a stored anime
    /// with none has most likely just never had a full fetch, and a studio list
    /// does not: <c>[]</c> is MyAnimeList saying it credits nobody, while null is
    /// nobody having asked. A show still airing with no total is not missing its
    /// episode count: the game shows how many have aired, and a fetch couldn't give
    /// it more. It is the one definition, behind both the draw's query
    /// and a guess's check, so the two can't disagree. An anime that isn't stored
    /// at all is missing everything, and is the caller's to tell apart.</summary>
    public static readonly Expression<Func<AnimeMetadata, bool>> IsMissing = a =>
        a.AiredFrom == null
        || (a.TotalEpisodes == null && a.AiringStatus != "currently_airing")
        || a.MediaType == null
        || a.MalScore == null
        || a.Genres == null
        || a.Genres.Count == 0
        || a.Studios == null;

    /// <summary>The opposite of <see cref="IsMissing"/>, built from it rather than
    /// written again.</summary>
    public static readonly Expression<Func<AnimeMetadata, bool>> IsComplete =
        Expression.Lambda<Func<AnimeMetadata, bool>>(Expression.Not(IsMissing.Body), IsMissing.Parameters);

    private static readonly Func<AnimeMetadata, bool> MissingCheck = IsMissing.Compile();

    /// <summary>Whether this stored anime is missing a stat, by the same
    /// expression a query uses.</summary>
    public static bool IsMissingAny(AnimeMetadata anime) => MissingCheck(anime);
}
