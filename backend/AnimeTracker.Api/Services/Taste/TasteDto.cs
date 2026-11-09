using AnimeTracker.Api.Services.Recap;

namespace AnimeTracker.Api.Services.Taste;

/// <summary>One genre of the counted set (taste-stats design.md D9).
/// <c>CountRank</c> is its 1-based place in the most-watched order, which holds
/// every genre; <c>ScoreRank</c> its place in the highest-rated order, which holds
/// only genres with a scored anime, so it is null for the rest. Both lists are
/// shipped once, carrying both ranks, and the client orders a tab by sorting on
/// the rank alone, never by re-deriving a tie-break. <c>WeightedScore</c> is the
/// Bayesian score rounded for display (the order is computed from the full-
/// precision value) and is null when nothing in the genre is scored. Posters are
/// up to three of its anime in my ranking's order.</summary>
public record TasteGenreDto(
    string Name,
    int AnimeCount,
    int ScoredCount,
    double? WeightedScore,
    int CountRank,
    int? ScoreRank,
    List<RecapRankingPosterDto> Posters);

/// <summary>One studio of the counted set — the same shape and rules as <see
/// cref="TasteGenreDto"/>, keyed by the name MyAnimeList credits.</summary>
public record TasteStudioDto(
    string Name,
    int AnimeCount,
    int ScoredCount,
    double? WeightedScore,
    int CountRank,
    int? ScoreRank,
    List<RecapRankingPosterDto> Posters);

/// <summary>A pair or trio of genres carried together by at least two anime,
/// named alphabetically, with how many carry every one of them.</summary>
public record TasteComboDto(List<string> Genres, int AnimeCount);

/// <summary>A hidden gem: an anime I scored 8 or higher outside MyAnimeList's
/// 1,000 most popular, other than a music video, PV or CM. <c>PopularityRank</c> is MyAnimeList's own rank for it,
/// shown in full — it is not derived from the community score.</summary>
public record TasteGemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore,
    int PopularityRank);

/// <summary>One category of a split. <c>Label</c> is a demographic's name, a
/// source key as MyAnimeList stores it, a decade such as "2010s", or "other" for
/// the categories merged after the fourth. <c>Values</c> are the raw keys it covers,
/// in the form the matching my-list filter takes: a named category its own key, and
/// "other" every key it merged (plus MyAnimeList's own "other" source when the
/// Source split counted one), so following any category reaches exactly the anime it
/// counted.</summary>
public record TasteSplitDto(string Label, int Count, List<string> Values);

/// <summary>The profile's taste read (<c>GET /api/profile/taste</c>): everything
/// the Hidden gems, Top genres, Top studios, Favourite combos and the three split
/// boxes draw. <c>Genres</c> and <c>Studios</c> hold every one of the counted
/// set in most-watched order, each carrying both ranks; pairs and trios are
/// capped at five, hidden gems at ten, and each split at four categories plus
/// Other.</summary>
public record TasteSectionDto(
    List<TasteGenreDto> Genres,
    List<TasteStudioDto> Studios,
    List<TasteComboDto> Pairs,
    List<TasteComboDto> Trios,
    List<TasteSplitDto> Demographics,
    List<TasteSplitDto> Sources,
    List<TasteSplitDto> Eras,
    List<TasteGemDto> HiddenGems);
