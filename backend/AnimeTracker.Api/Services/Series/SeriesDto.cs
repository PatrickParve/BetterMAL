using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Series;

/// <summary>One anime's place in a series — a main-line entry, an extra, or a
/// related entry. <c>RelationType</c> is the traversal-set relation (design.md
/// decision 1) connecting this anime to another member, taken from this
/// anime's own outgoing relation rows; a member reached only by a reverse
/// edge (its own relations were never fetched) or the root itself has no
/// such row, so it's null. <c>AiredEpisodes</c> is null-means-unknown
/// (design.md decision 1): finished → total, currently airing → the
/// schedule reader's count clamped to total (null when the reader has
/// nothing stored), not yet aired → 0, unknown airing status → null.
/// <c>RelationGroup</c> is this entry's relationship to the main line
/// (split-series-by-version design.md decision 4) — the <see cref="RelationGroup"/>
/// enum's name, e.g. "AlternativeVersion", "SideStory" — null for a main-line entry,
/// which has no group of its own. <c>IsRelatedEntry</c> is true for an entry
/// shown from a relation the series traversal doesn't follow rather than
/// stored as a member (design.md D5) — it never counts toward any average,
/// stat, or the member cap. <c>OpensOwnSeries</c> is what a More tile's link
/// target is decided from (rebuild-series-by-story-component design.md D7):
/// true only for a version-neighbour member that carries story relations of
/// its own (<see cref="MembershipKind.NeighbourTelling"/>), whose tile opens
/// that anime's own series page; false for every other entry — a story
/// extra, a folded-in version neighbour, and every related entry alike —
/// whose tile opens the anime's detail page instead, so a tile can never
/// point back at the page it is rendered on. <c>VersionSlotKey</c>/
/// <c>BranchHeadAnimeId</c> mirror <see cref="Models.SeriesMember.VersionSlotKey"/>/
/// <see cref="Models.SeriesMember.BranchHeadAnimeId"/> (design.md D4): both
/// null outside the main line and for a trunk entry; a version slot's own
/// alternatives carry both, the rest of that alternative's branch carries
/// only <c>BranchHeadAnimeId</c>. <c>GlobalRank</c> is this anime's 1-based
/// rank in the anime-ranking capability's whole-library ranking — null when
/// the anime doesn't carry one (unscored, plan-to-watch, or not yet aired,
/// per <see cref="Services.Ranking.RankBand.Unranked"/>) — always null for a
/// related entry, which never enters that ranking's tie-break or any other
/// figure here.</summary>
public record SeriesEntryDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    string? AiringStatus,
    int? TotalEpisodes,
    int? AverageEpisodeDurationSeconds,
    DateOnly? AiredFrom,
    DateOnly? AiredTo,
    double? MalScore,
    string? RelationType,
    int Order,
    int? AiredEpisodes,
    UserAnimeEntryDto? Entry,
    string? RelationGroup,
    bool IsRelatedEntry,
    bool OpensOwnSeries,
    int? VersionSlotKey,
    int? BranchHeadAnimeId,
    int? GlobalRank);

/// <summary>One unweighted mean plus the count it was computed over, e.g.
/// "8.42 · 5 of 6 scored". <c>Value</c> is null when <c>ScoredCount</c> is
/// zero, and is otherwise unrounded (design.md decision 8) — the client
/// rounds for display.</summary>
public record SeriesAverageDto(double? Value, int ScoredCount, int TotalCount);

/// <summary>The four averages the series page shows (design.md decision 8):
/// MAL's score and mine, each across the main line and across every member.</summary>
public record SeriesScoresDto(
    SeriesAverageDto MalMain,
    SeriesAverageDto MalAll,
    SeriesAverageDto MineMain,
    SeriesAverageDto MineAll);

/// <summary>Series-wide stats (design.md decision 9). Where a main line holds
/// a version slot, every field described below as scoped to "the main line"
/// is in fact scoped to one combination of picks — trunk plus each slot's
/// picked branch (rebuild-series-by-story-component design.md D6,
/// <see cref="SeriesDto.StatsByPick"/>) — except <c>MainLineCount</c>, which
/// tracks the same picked scope so it stays a sensible denominator beside
/// <c>EntriesCompleted</c>. A series with no slot has only one such
/// combination, so every figure covers its whole main line exactly as
/// before this change. Episode/runtime totals
/// count every member: one with a known <c>TotalEpisodes</c> contributes it
/// in full, one without contributes its known aired-so-far count instead
/// (0 when that's unknown too) and sets <c>HasUnknownEpisodeCounts</c>, so
/// the total is always a true lower bound rather than a guess. My
/// progress (watched episodes/seconds, entries completed) is scoped to the
/// main line only, matching "time left" being main-line runtime minus
/// watched runtime. <c>MyWatchedSeconds</c> stays first-run-only — it is
/// what "time left" subtracts and what the progress bar fills — while
/// <c>MyRewatchedSeconds</c> is the orthogonal rewatch figure (design D10):
/// the client sums the two for "time watched" display, but each is read
/// separately, and only <c>MyWatchedSeconds</c> feeds anything else.
/// <c>MainLineAiredEpisodes</c> is summed over the same
/// known-total main-line set (design.md decision 4): finished entries
/// contribute their full total, a currently-airing entry contributes its
/// aired-so-far count, and an entry that hasn't aired yet contributes
/// nothing — so it can never exceed <c>MainLineEpisodeTotal</c>.
/// <c>MainLineCompletedByMe</c> mirrors the frontend's
/// <c>isGroupCompleted</c> convention over the main line (design.md decision
/// 7) — despite the field's name, it's true when every finished-airing
/// main-line member is Completed *or* Dropped
/// (profile-navbar-and-dropped-scores design.md decision 3); the name is
/// kept as the wire contract for the frontend's
/// <c>series.stats.mainLineCompletedByMe</c>. <c>ExtrasCompleted</c> counts extras marked Completed, the extras-side
/// twin of <c>EntriesCompleted</c>. The highest-scored/most-rewatched/favourite/studios/genres figures
/// span every member, main line and extras alike; the highest-scored and
/// most-rewatched lists carry every tied entry rather than one arbitrary
/// winner (design.md decision 8), and <c>MostRewatchedAnimeIds</c> is empty
/// when no member has been rewatched at all.</summary>
public record SeriesStatsDto(
    int MainLineEpisodeTotal,
    long MainLineRuntimeSeconds,
    bool HasUnknownEpisodeCounts,
    int MainLineAiredEpisodes,
    int ExtrasEpisodeTotal,
    long ExtrasRuntimeSeconds,
    int MyWatchedEpisodes,
    long MyWatchedSeconds,
    long MyRewatchedSeconds,
    int EntriesCompleted,
    int ExtrasCompleted,
    bool MainLineCompletedByMe,
    int MainLineCount,
    int ExtrasCount,
    int? LongestGapDays,
    int? LongestGapFromAnimeId,
    int? LongestGapToAnimeId,
    List<int> HighestMalScoreAnimeIds,
    List<int> MyHighestScoreAnimeIds,
    List<int> MostRewatchedAnimeIds,
    List<string> Studios,
    List<string> Genres);

/// <summary>One version slot on the main line (rebuild-series-by-story-component
/// design.md D4): <c>AlternativeAnimeIds</c> is every alternative the slot
/// holds, ordered the same way the watch order would break their tie
/// (<see cref="SeriesGraphBuilder.OrderKey"/>); <c>DefaultBranchHeadAnimeId</c>
/// is the alternative the slot opens on absent a reader's own pick (design.md
/// D5) — always one of <c>AlternativeAnimeIds</c>. <c>SlotKey</c> is the
/// lowest MAL id among the alternatives, the same value carried on each
/// alternative's own <see cref="SeriesEntryDto.VersionSlotKey"/>.</summary>
public record SeriesSlotDto(int SlotKey, List<int> AlternativeAnimeIds, int DefaultBranchHeadAnimeId);

/// <summary>One admissible combination of slot picks and the
/// <see cref="SeriesStatsDto"/> it produces (design.md D6, task 7.3).
/// <c>BranchHeadAnimeIds</c> names one alternative per slot, positionally
/// aligned with <see cref="SeriesDto.Slots"/> — index <c>i</c> here is the
/// pick for <c>Slots[i]</c>. The combinations delivered are capped at 24;
/// beyond the cap, every slot past the first keeps its default pick in
/// every combination rather than the full product of every slot's
/// alternatives being enumerated.</summary>
public record SeriesStatsByPickDto(List<int> BranchHeadAnimeIds, SeriesStatsDto Stats);

/// <summary>Full projection of a franchise for the series page. <c>Status</c>
/// is one of "Airing", "Ongoing", "Upcoming", or "Finished" (design.md/task
/// 3.4) — computed server-side since it depends on every member's airing
/// status, not just the root's. "Airing" is main-line-only: it is returned
/// only when a main-line member is currently airing. "Ongoing" now covers
/// everything else that isn't settled — an extra (not main-line) member
/// currently airing, or a member of any kind still unaired. <c>Title</c>,
/// <c>EnglishTitle</c> and <c>PictureUrl</c> come from the root entry
/// (design.md decision 4). <c>RootAniListId</c> is the root's AniList id
/// when a sync row exists for it, read the same way
/// <c>AnimeDetailService</c> reads it for a single anime (design.md decision
/// 6) — null falls back to an AniList title search client-side.
/// <c>Slots</c>/<c>StatsByPick</c> are the main line's version slots and the
/// per-combination stats they admit (rebuild-series-by-story-component
/// design.md D4/D6) — both empty-list/single-entry respectively when the
/// main line holds no slot at all, so a series untouched by this capability
/// projects exactly as it always has.</summary>
public record SeriesDto(
    int SeriesId,
    int RootAnimeId,
    int? RootAniListId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string Status,
    int? FirstYear,
    int? LastYear,
    DateTimeOffset BuiltAt,
    bool IsPartial,
    bool IsTruncated,
    SeriesScoresDto Scores,
    // The default combination's stats — every alternative picked at its
    // default — so a first paint needs no lookup into StatsByPick
    // (design.md D6). Every other admissible combination, this one
    // included, is also in StatsByPick.
    SeriesStatsDto Stats,
    // Empty when the main line holds no version slot at all (design.md D4).
    List<SeriesSlotDto> Slots,
    List<SeriesStatsByPickDto> StatsByPick,
    List<SeriesEntryDto> MainLine,
    List<SeriesEntryDto> Extras,
    // The series' own overrides (null when unset) and the picker inputs
    // derived from them (artwork-selection/series-identity): PictureOptions
    // is the main line's picture pool (Services/Artwork/SeriesPicturePool)
    // plus the current selection when the pool doesn't already carry it;
    // TitleOptions is every main-line member's title/English title
    // (Services/Series/SeriesTitleRule.OfferedTitles); PicturesPendingCount
    // is how many main-line members are in my list but have never had a
    // picture fetch, for the client's bounded-backfill note (design.md D6).
    string? SelectedTitle,
    string? SelectedPictureUrl,
    List<string> PictureOptions,
    List<string> TitleOptions,
    int PicturesPendingCount);
