namespace AnimeTracker.Api.Services.Recap;

/// <summary>One "hot take": an included, both-scored anime ranked by how far
/// my score diverges from MAL's, largest first (design.md decision 4).
/// <c>Divergence</c> is the shared <see cref="ScoreDivergence"/> value —
/// positive means MAL sits further above its mean than I sit above mine (MAL
/// liked it more than I did), negative is the reverse — so the client can
/// render the two directions distinctly from its sign alone. <c>MalRevealed</c>
/// is the same "always show for completed shows" flag as <see
/// cref="RecapRowDto.MalRevealed"/>, so a hidden MAL score stays hidden in
/// hot takes exactly as it does everywhere else in the recap.</summary>
public record RecapHotTakeDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int MyScore,
    double MalScore,
    double Divergence,
    bool MalRevealed);

/// <summary>The recap's stat block (design.md "Recap stats" requirement),
/// computed over the period+filter's included set alone. <c>Dropped</c>
/// counts included entries with status Dropped, so the gap between
/// <c>AnimeCounted</c> and <c>Completed</c> is accounted for rather than
/// left unexplained (design.md decision 10).</summary>
public record RecapStatsDto(
    double? MeanScore,
    int AnimeCounted,
    int Completed,
    int Dropped,
    int EpisodesWatched,
    int MoviesWatched,
    long TimeSpentSeconds,
    List<RecapHotTakeDto> HotTakes);

/// <summary>One of a ranking's top-three posters (design.md "Top three
/// posters for every ranked season and year") — attached to every row of the
/// season and year rankings alike (polish-recap-page design.md decision
/// 3).</summary>
public record RecapRankingPosterDto(int AnimeId, string Title, string? PictureUrl);

/// <summary>One ranked season, best first. <c>WeightedScore</c> is the
/// Bayesian average (design.md decision "Bayesian ranking of seasons and
/// years"), rounded for display; <c>ScoredCount</c> is <c>v</c> in that
/// formula. <c>TopPosters</c> carries every row, not only the leader's
/// (polish-recap-page design.md decision 3).</summary>
public record RecapSeasonRankingDto(
    int Year,
    string Season,
    int ScoredCount,
    double WeightedScore,
    List<RecapRankingPosterDto> TopPosters);

/// <summary>One ranked year, best first — same shape and rules as <see
/// cref="RecapSeasonRankingDto"/> one level up the calendar.</summary>
public record RecapYearRankingDto(
    int Year,
    int ScoredCount,
    double WeightedScore,
    List<RecapRankingPosterDto> TopPosters);

/// <summary>One ranked season or year, largest time watched first (design.md
/// decision 7). One DTO serves both levels: <c>Season</c> is null at the
/// year level, non-null at the season level — the two differ only in
/// grouping key. <c>TimeSpentSeconds</c> is computed on the same basis as
/// <see cref="RecapStatsDto.TimeSpentSeconds"/>, so a group's rows always sum
/// to the stat block's total. Unlike the score rankings, a group need not
/// hold any scored anime to be ranked here — only watched ones.
/// <c>TopPosters</c> is empty for every row at the season level; at the year
/// level it carries the leader's posters (three highest-scored anime, picked
/// by episodes watched rather than score) and is empty below rank
/// one.</summary>
public record RecapTimeRankingDto(
    int Year,
    string? Season,
    long TimeSpentSeconds,
    int EpisodesWatched,
    List<RecapRankingPosterDto> TopPosters);

/// <summary>One row of the recap's full included set (design.md decision 1:
/// the server returns the whole set, not just a top 10 — the client does the
/// top-10 slice, the basis switch, and the media-type narrowing locally).
/// <c>MalRevealed</c> is the server-computed "may this be shown under
/// 'always show completed scores'" boolean, same rule as
/// <c>TopSeriesItemDto.MalRevealed</c> — pass it straight into
/// <c>ScoreValue</c>'s <c>completed</c> prop.</summary>
public record RecapRowDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    int? MyScore,
    double? MalScore,
    bool MalRevealed);

/// <summary>Full recap payload for one period + time filter (design.md
/// decision 1). <c>Filter</c> echoes what was actually used — always
/// <c>RecapTimeFilter.Aired</c> for a season recap, regardless of what (if
/// anything) the caller sent, since a season recap has no toggle.
/// <c>WatchedCount</c>/<c>AiredCount</c> are this same period's entry counts
/// under *both* filters (design.md decision 2), so the recap page can
/// render the filter toggle's disabled/enabled state, and fall back off an
/// option that turned out empty, without a second call to learn the other
/// option's count. <c>SeasonRanking</c>/<c>YearRanking</c>/
/// <c>SeasonTimeRanking</c>/<c>YearTimeRanking</c> are empty wherever design.md
/// decision "Bayesian ranking of seasons and years"'s gate excludes them for
/// this mode/filter combination — the time rankings share that same gate
/// (design.md decision 8) even though they don't share the score rankings'
/// scored-anime requirement.</summary>
public record RecapDto(
    string Mode,
    int StartYear,
    int EndYear,
    string? Season,
    string Filter,
    int WatchedCount,
    int AiredCount,
    RecapStatsDto Stats,
    List<RecapRowDto> Items,
    List<RecapSeasonRankingDto> SeasonRanking,
    List<RecapYearRankingDto> YearRanking,
    List<RecapTimeRankingDto> SeasonTimeRanking,
    List<RecapTimeRankingDto> YearTimeRanking);

/// <summary>One calendar year's entry counts under both time filters, for
/// the whole list — not scoped to any particular recap period. The recap
/// picker sums these across a candidate range itself (design.md decision 2)
/// rather than asking the server to.</summary>
public record RecapYearAvailabilityDto(int Year, int WatchedCount, int AiredCount);

/// <summary>One (year, season) point's entry count under the "aired"
/// selection, for the whole list — a season recap has no watch-history
/// option, so only one count is meaningful.</summary>
public record RecapSeasonAvailabilityDto(int Year, string Season, int Count);

/// <summary>The whole-list availability summary backing the recap picker
/// (design.md decision 2) — loaded once when the picker opens.</summary>
public record RecapAvailabilityDto(List<RecapYearAvailabilityDto> Years, List<RecapSeasonAvailabilityDto> Seasons);
