export type WatchStatus = 'Watching' | 'OnHold' | 'PlanToWatch' | 'Completed' | 'Dropped' | 'Rewatching'

// The MyAnimeList connection's three states (report-jobs-and-lost-mal-connection
// design.md D14): Lost means MyAnimeList itself refused this app's login,
// distinct from an outage. App.tsx shows the first-run connect screen only
// for NotConnected — a lost connection keeps the app open.
export type MalConnectionState = 'Connected' | 'Lost' | 'NotConnected'

export type MalAuthStatus = {
  state: MalConnectionState
  lostAt: string | null
}

export type HealthStatus = {
  status: string
}

export type UserAnimeEntryDto = {
  animeId: number
  status: WatchStatus
  episodesWatched: number
  myScore: number | null
  startedAt: string | null
  completedAt: string | null
  rewatchCount: number
  pendingSync: boolean
  lastSyncedAt: string | null
}

export type UserAnimeEntryEditRequest = {
  status?: WatchStatus
  episodesWatched?: number
  myScore?: number | null
  rewatchCount?: number
  // Nullable-of-optional: omit entirely to leave the date untouched, or send
  // `null` to explicitly clear it — `undefined` already means "not being
  // edited" for every other field here, so it can't also mean "clear".
  startedAt?: string | null
  completedAt?: string | null
  // Only consulted when this same edit returns a Rewatching entry to
  // Completed by choosing Completed explicitly (design.md D2) — omitted or
  // false leaves the rewatch count unchanged, true increases it by one.
  countsAsRewatch?: boolean
}

// The dropdown's "series" row (design.md decision 5) — id is the root
// entry's MAL id and so is itself the navigation target (key-series-by-root-
// anime-id design.md D1/D7), entryCount covers every member. Backend
// AnimeSearchResultDto carries one shared `id` field for both kinds (see
// its own doc comment), so this is `id`, not `seriesId`, unlike the separate
// SeriesSearchResultDto below.
export type SeriesSearchResult = {
  kind: 'series'
  id: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  entryCount: number
}

// Type-ahead dropdown row: discriminated on `kind` so series rows (pinned
// first, capped at 2) and anime rows share one flat list — mirrors backend
// AnimeSearchResultDto.
export type AnimeSearchResult =
  | {
      kind: 'anime'
      id: number
      title: string
      englishTitle: string | null
      pictureUrl: string | null
    }
  | SeriesSearchResult

// Everything the reusable entry editor overlay needs to render for one anime.
// `entry` is null when the anime isn't in my list yet (add-to-list mode).
export type EntryEditorTarget = {
  animeId: number
  animeTitle: string
  totalEpisodes: number | null
  // The anime's raw airing status (`finished_airing`/`currently_airing`/
  // `not_yet_aired`, or null when unrecorded) — needed to decide whether
  // Rewatching is offered (design.md D0). null also covers callers that
  // don't have this data plumbed through yet (tasks.md 2.9).
  airingStatus: string | null
  // Aired-so-far episode count, null-means-unknown — paired with
  // airingStatus so the overlay can compute hasAiredEpisodes() itself
  // (gate-editing-on-aired-episodes design.md D7).
  episodesAired: number | null
  // The anime's raw media type — needed to decide whether the Rank action is
  // offered (anime-ranking capability: short-form entries are never
  // hand-orderable). null covers callers that don't have this plumbed
  // through (add-anime-ranking tasks.md 6.3), same convention as
  // airingStatus above.
  mediaType: string | null
  entry: UserAnimeEntryDto | null
  onSaved?: (entry: UserAnimeEntryDto) => void
  onDeleted?: () => void
}

// Everything the shared episode-increment hook needs to perform the edit and,
// on a completion transition, open the completion score prompt.
export type IncrementTarget = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  episodesWatched: number
  previousStatus: WatchStatus
  currentScore: number | null
  // The anime's raw media type — carried into the completion prompt so it
  // can decide whether save-and-rank is offered for the chosen score
  // (anime-ranking capability: short-form entries are never hand-orderable;
  // add-anime-ranking tasks.md 6.5).
  mediaType: string | null
  onSaved: (entry: UserAnimeEntryDto) => void
  // Carries the completion-score prompt's saved entry (null if the user
  // skipped without scoring), so a caller showing only that one anime can
  // patch its state instead of re-reading.
  onCompleted?: (entry: UserAnimeEntryDto | null) => void
}

export type NextEpisodeEtaDto = {
  days: number
  hours: number
}

export type CurrentlyWatchingItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  episodesWatched: number
  totalEpisodes: number | null
  episodesAired: number | null
  currentlyAiring: boolean
  nextEpisode: NextEpisodeEtaDto | null
  status: WatchStatus
  // gate-editing-on-aired-episodes: the raw airing status — currentlyAiring
  // alone can't distinguish finished_airing from not_yet_aired once
  // episodesAired is unknown, and the frontend gate needs that distinction.
  airingStatus: string | null
}

export type AiringTodayItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  localTime: string
  episodeNumber: number | null
}

export type CurrentSeasonItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  episodesWatched: number
  totalEpisodes: number | null
  malScore: number | null
  popularityRank: number | null
  episodesAired: number | null
  finishedAiring: boolean
}

export type AnimeUpdateKind =
  | 'Announced'
  | 'EpisodeCountReleased'
  | 'StartDateReleased'
  | 'StartDateChanged'
  | 'BroadcastSlotChanged'
  | 'EpisodesMoved'

// One card of news about an anime (anime-updates spec) — every kind noticed
// in a single detection pass merged into one row. totalEpisodes/airedFrom are
// the anime's *current* values, read live, so a later correction shows up
// here without the row itself changing; the previous* fields are the
// exception, reported exactly as recorded since for those the news is the
// movement itself.
export type AnimeUpdateDto = {
  id: number
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  kinds: AnimeUpdateKind[]
  detectedAt: string
  totalEpisodes: number | null
  airedFrom: string | null
  previousStartDate: string | null
  previousBroadcastDayOfWeek: string | null
  previousBroadcastTime: string | null
  currentBroadcastDayOfWeek: string | null
  currentBroadcastTime: string | null
  movedEpisode: number | null
  previousEpisodeDate: string | null
  newEpisodeDate: string | null
  reason: string
  seen: boolean
}

export type MainDashboardDto = {
  currentlyWatching: CurrentlyWatchingItemDto[]
  airingToday: AiringTodayItemDto[]
  currentSeason: CurrentSeasonItemDto[]
}

export type AiringSlotDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  localTime: string
  episodeNumber: number | null
  episodeNumberEnd: number | null
}

export type AiringDayDto = {
  localDate: string
  dayOfWeek: string
  slots: AiringSlotDto[]
}

export type AiringWeekDto = {
  weekStart: string
  weekEnd: string
  days: AiringDayDto[]
}

// An anime's index within each of the season browser's four orderings over
// the whole listing it came from (design D3) — only the season and year
// listing reads populate AnimeBrowseItemDto.sortOrder; search leaves it null.
export type BrowseSortOrderDto = {
  popularity: number
  malScore: number
  alphabetical: number
  myScore: number
}

// Shared shape for a browsable (not-yet-in-my-list-scoped) anime card, used by
// both the season page and the search page.
export type AnimeBrowseItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  totalEpisodes: number | null
  mediaType: string | null
  malScore: number | null
  popularityRank: number | null
  myScore: number | null
  inMyList: boolean
  sortOrder: BrowseSortOrderDto | null
}

export type SeasonPageDto = {
  year: number
  season: string
  items: AnimeBrowseItemDto[]
  totalCount: number
  lastFetchedAt: string | null
  hasListing: boolean
}

export type SeasonRefreshResultDto = {
  outcome: 'fetched' | 'notListed' | 'skipped' | 'failed'
}

export type SeasonBoundsDto = {
  latestYear: number
  latestSeason: string
}

export type YearPageDto = {
  year: number
  items: AnimeBrowseItemDto[]
  totalCount: number
  lastFetchedAt: string | null
  hasListing: boolean
}

export type YearRefreshResultDto = {
  outcome: 'fetched' | 'notListed' | 'skipped' | 'failed'
}

// The results page's series row — separate shape from SeriesSearchResult
// (no `kind`) since it rides in its own `series` array rather than a
// discriminated `items` union (design.md decision 5).
export type SeriesSearchResultDto = {
  seriesId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  entryCount: number
}

export type SearchPageDto = {
  query: string
  items: AnimeBrowseItemDto[]
  offset: number
  limit: number
  totalCount: number
  series: SeriesSearchResultDto[]
  malSearchFailed: boolean
}

export type MyListItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  mediaType: string | null
  totalEpisodes: number | null
  malScore: number | null
  airingStatus: string | null
  episodesAired: number | null
  entry: UserAnimeEntryDto
  // anime-ranking: this entry's overall rank, null when it isn't in the
  // ranking (unscored, Plan to watch, unaired) — feeds the my-score sort's
  // rank tiebreak (utils/anime.ts composeComparator).
  myRank: number | null
  popularityRank: number | null
}

export type TopAnimeItemDto = {
  rank: number
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  totalEpisodes: number | null
  malScore: number | null
  entry: UserAnimeEntryDto | null
  // gate-editing-on-aired-episodes: this page opens the entry editor, so it
  // needs the same aired-episode facts as CurrentlyWatchingItemDto to gate
  // the editor's own fields.
  airingStatus: string | null
  episodesAired: number | null
}

// The Top anime page's ranking-list selector. Mirrors the backend's
// TopAnimeRankingType allow-list — ona and music are absent because MAL API
// v2 rejects those ranking_type values with 400 (design.md D1).
export type TopAnimeRankingType = 'all' | 'tv' | 'movie' | 'ova' | 'special' | 'bypopularity' | 'favorite'

export const TOP_ANIME_RANKING_TYPES: { value: TopAnimeRankingType; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'tv', label: 'TV' },
  { value: 'movie', label: 'Movie' },
  { value: 'ova', label: 'OVA' },
  { value: 'special', label: 'Special' },
  { value: 'bypopularity', label: 'Popularity' },
  { value: 'favorite', label: 'Favourite' },
]

export type ActivityChangeType =
  | 'Added'
  | 'StatusChanged'
  | 'EpisodeIncremented'
  | 'ScoreChanged'
  | 'Completed'
  | 'RewatchCountChanged'
  | 'Removed'
  | 'StartDateChanged'
  | 'FinishDateChanged'

export type ActivityFeedItemDto = {
  id: number
  timestamp: string
  animeId: number
  animeTitle: string
  animeEnglishTitle: string | null
  pictureUrl: string | null
  changeType: ActivityChangeType
  changeDetail: string | null
  summary: string
}

export type AnimeStatsDto = {
  days: number
  watching: number
  completed: number
  onHold: number
  dropped: number
  planToWatch: number
  totalEntries: number
  rewatched: number
  rewatchedEpisodes: number
  episodes: number
  movies: number
  rewatching: number
}

export type TopAnimeEntryDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  myScore: number
  // anime-ranking: this anime's overall rank — always present, since every
  // member of a TopAnimeTierDto is in the ranking by construction.
  myRank: number
}

export type TopAnimeMediaType = 'all' | 'tv' | 'movie' | 'ova' | 'ona' | 'special'

// One score tier: every scored member for the current scope, in tier order,
// with includedCount of them making the resolved top list. A cut line
// belongs after includedCount whenever it's less than members.length.
export type TopAnimeTierDto = {
  score: number
  members: TopAnimeEntryDto[]
  includedCount: number
}

// items is the resolved "My top anime" list for mediaType's scope; tiers
// carries only the tiers that contribute to it, each in full — that's what
// the tier order overlay is built from.
export type TopAnimeSectionDto = {
  items: TopAnimeEntryDto[]
  tiers: TopAnimeTierDto[]
  mediaType: string
}

export type RewatchedEntryDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  rewatchCount: number
  myScore: number | null
}

// items is every rewatched entry (rewatchCount > 0) for mediaType's scope,
// ordered by rewatch count descending, with no tiers and no cap.
export type RewatchedSectionDto = {
  items: RewatchedEntryDto[]
  mediaType: string
}

export type ScoreDistributionBucketDto = {
  score: number
  count: number
}

export type ScoreDistributionDto = {
  buckets: ScoreDistributionBucketDto[]
  meanScore: number | null
}

export type OpinionDivergenceItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  myScore: number
  malScore: number
  isCompleted: boolean
}

export type UnresolvedEpisodeEntryDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  episodesWatched: number
}

// All-list episode progress (profile-stats "All-list episode progress").
// Dropped entries are excluded outright; every other entry's total resolves
// to its published total episode count, else its aired-so-far count when
// known and non-zero, else it's unresolved — counted in unresolvedEntries and
// excluded from episodesWatched/episodesTotal alike. unresolvedAnime lists
// those same entries in full, alphabetical by title.
export type EpisodeProgressDto = {
  episodesWatched: number
  episodesTotal: number
  unresolvedEntries: number
  totalEntries: number
  unresolvedAnime: UnresolvedEpisodeEntryDto[]
}

export type ProfileDto = {
  stats: AnimeStatsDto
  episodeProgress: EpisodeProgressDto
  recentActivity: ActivityFeedItemDto[]
  topAnime: TopAnimeSectionDto
  rewatched: RewatchedSectionDto
  scoreDistribution: ScoreDistributionDto
  theyLikedItIDidnt: OpinionDivergenceItemDto[]
  iLikedItTheyDidnt: OpinionDivergenceItemDto[]
  favouriteSeasons: RecapSeasonRankingDto[]
  favouriteYears: RecapYearRankingDto[]
}

// One franchise ranked by Top series: display fields from its root anime,
// its total member count, and its two main-series averages (SeriesAverageDto,
// defined below) — identical in shape and computation to the series page's
// `MAL · main series`/`Mine · main series` chips (design.md decision 1).
// malRevealed is the server-computed "may this be shown under 'always show
// completed scores'" boolean (design.md decision 5) — pass it straight into
// ScoreValue's `completed` prop.
// mainLineAiredEpisodes and mainLineAverageRank are the same figures
// SeriesListItemDto carries: aired-episode count over the main line's
// default combination of version alternatives, and the mean ranking
// position over main-line members my rankings cover (null when they cover
// none). Both are what the my-score basis's tie-break chain sorts on.
export type TopSeriesItemDto = {
  seriesId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  entryCount: number
  mainLineAiredCount: number
  mainLineAiredEpisodes: number
  mainLineAverageRank: number | null
  malMain: SeriesAverageDto
  mineMain: SeriesAverageDto
  malRevealed: boolean
}

// Every series with at least one member in my list (design.md decision 2),
// pre-ordered by the full my-score chain — my main-series average
// descending, then average ranking position ascending (nulls last), then
// main-line episodes aired descending, then raw title case-insensitively —
// the client re-sorts and re-filters this same array locally when the
// ranking basis is switched (design.md decision 4).
export type TopSeriesSectionDto = {
  items: TopSeriesItemDto[]
}

// One franchise ranked by "Most rewatched"'s Series scope: display fields
// from its root anime and its total rewatch time in seconds, summed across
// every member — main line and extras alike (design.md D9).
export type RewatchedSeriesItemDto = {
  seriesId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  rewatchSeconds: number
}

// Every series with above-zero total rewatch time (design.md D9), ordered by
// that total descending then title case-insensitively, with no cap.
export type RewatchedSeriesSectionDto = {
  items: RewatchedSeriesItemDto[]
}

export type SyncStatusDto = {
  pendingCount: number
  heldCount: number
  lastSyncedAt: string | null
}

// A change held for review since a previous process start — an unsent edit
// or a queued removal (design.md D1-D8a). Kind distinguishes the two;
// localValues is null for a removal, which has no field values to show.
export type HeldChangeKind = 'Entry' | 'Removal'

export type HeldChangeValuesDto = {
  status: WatchStatus
  episodesWatched: number
  myScore: number | null
  startedAt: string | null
  completedAt: string | null
  rewatchCount: number
}

export type HeldChangeRecentChangeDto = {
  changeType: ActivityChangeType
  changeDetail: string | null
  timestamp: string
}

export type HeldChangeDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  kind: HeldChangeKind
  heldAt: string
  localValues: HeldChangeValuesDto | null
  recentChanges: HeldChangeRecentChangeDto[]
  additionalChangeCount: number
  remoteValues: HeldChangeValuesDto | null
  remoteUnavailable: boolean
}

// Response of a per-item accept/decline. A 404 (nothing held for that anime)
// is surfaced by the client call throwing rather than via this shape.
export type HeldChangeDecisionDto = {
  applied: boolean
  error?: string
}

// Shared background-job lifecycle (background-jobs capability, design.md
// D1/D15) — every trigger POST and the combined api/app-status read serve
// this same shape, mirroring backend JobDto. total is null while the job
// doesn't yet know how much work there is — never zero, which means a run
// that really has nothing to do.
export type JobPhase = 'NotStarted' | 'Running' | 'Complete' | 'Failed'

export type JobStatusDto = {
  phase: JobPhase
  done: number
  total: number | null
  error: string | null
  startedAt: string | null
  finishedAt: string | null
  retryAt: string | null
}

// Accept all and decline all share one job (design.md D18): whichever is
// pressed second gets the running job's state back and starts nothing.
// action names which of the two is running or most recently ran.
export type HeldDecisionAction = 'Accept' | 'Decline'

export type HeldDecisionJobDto = JobStatusDto & {
  action: HeldDecisionAction | null
}

export type WeeklyCheckDto = {
  lastRunAt: string
  failed: boolean | null
  error: string | null
}

export type AppStatusJobsDto = {
  listImport: JobStatusDto
  syncNow: JobStatusDto
  reconcile: JobStatusDto
  heldDecision: HeldDecisionJobDto
  resync: JobStatusDto
  airingRefresh: JobStatusDto
  seriesBuild: JobStatusDto
  fileImport: JobStatusDto
}

// GET api/app-status: one cheap read of every job's state, the MyAnimeList
// connection state, and the weekly check's last outcome (design.md D15).
export type AppStatusDto = {
  malConnection: MalAuthStatus
  weeklyCheck: WeeklyCheckDto | null
  jobs: AppStatusJobsDto
}

export type ReconciliationDiffChangeType = 'Added' | 'Updated'

export type PendingReconciliationDiffEntryDto = {
  animeId: number
  title: string
  pictureUrl: string | null
  changeType: ReconciliationDiffChangeType
  status: WatchStatus
  episodesWatched: number
  myScore: number | null
  startedAt: string | null
  completedAt: string | null
  rewatchCount: number
}

export type PendingReconciliationDiffDto = {
  id: number
  computedAt: string
  entries: PendingReconciliationDiffEntryDto[]
}

export type RelatedAnimeDto = {
  animeId: number
  title: string
  pictureUrl: string | null
  mediaType: string | null
  relationType: string
  englishTitle: string | null
}

// Mirrors backend Services/Relations/RelationConfidence.cs.
export type RelationConfidence = 'Confirmed' | 'Corroborated' | 'Unconfirmed' | 'Contradicted' | 'Unknown'

// Mirrors backend Services/Detail/ResolvedRelationDto.cs — the server-ranked
// pick for a prequel/sequel/parent-story button, replacing the old
// first-by-array-order pick over relatedAnime.
export type ResolvedRelationDto = {
  animeId: number
  title: string
  pictureUrl: string | null
  mediaType: string | null
  confidence: RelationConfidence
  isReverseDerived: boolean
}

// Mirrors backend Services/Series/SeriesDto.cs (tasks 3.1-3.3, extended by
// split-series-by-version tasks 7.5/10.1 and rebuild-series-by-story-component
// task 8.1). airedEpisodes is null-means-unknown
// (design.md decision 1 of redesign-series-page): finished -> total, airing ->
// schedule reader clamped to total (null when unknown), not yet aired -> 0,
// unknown airing status -> null. relationGroup is this entry's relationship
// to the main line (split-series-by-version design.md decision 4) — the raw
// PascalCase enum name (e.g. "AlternativeVersion", "SideStory"), null for a
// main-line entry. isRelatedEntry is true for an entry shown from a relation
// the series traversal doesn't follow rather than stored as a member (design
// D5) — it's shown, not counted, and (for the Alternative version/setting
// groups specifically) never carries a stored series id of its own: its tile
// links by this entry's own animeId (design D10). opensOwnSeries decides a
// More tile's link target (rebuild-series-by-story-component design.md D7):
// true only for a version-neighbour member that carries story relations of
// its own, whose tile opens that anime's own series page; false — including
// every related entry — opens the anime's detail page instead. versionSlotKey/
// branchHeadAnimeId mirror the stored SeriesMember columns (design.md D4):
// both null outside the main line and for a trunk entry; a version slot's own
// alternatives carry both, the rest of that alternative's branch carries only
// branchHeadAnimeId. globalRank is this anime's 1-based rank in the
// anime-ranking capability's whole-library ranking — null when it doesn't
// carry one (unscored, plan-to-watch, not yet aired, or a related entry,
// which never enters that ranking).
export type SeriesEntryDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  mediaType: string | null
  airingStatus: string | null
  totalEpisodes: number | null
  averageEpisodeDurationSeconds: number | null
  airedFrom: string | null
  airedTo: string | null
  malScore: number | null
  relationType: string | null
  order: number
  airedEpisodes: number | null
  entry: UserAnimeEntryDto | null
  relationGroup: string | null
  isRelatedEntry: boolean
  opensOwnSeries: boolean
  versionSlotKey: number | null
  branchHeadAnimeId: number | null
  globalRank: number | null
}

// value is null exactly when scoredCount is 0; otherwise unrounded — render
// to two decimals at the point of display.
export type SeriesAverageDto = {
  value: number | null
  scoredCount: number
  totalCount: number
}

export type SeriesScoresDto = {
  malMain: SeriesAverageDto
  malAll: SeriesAverageDto
  mineMain: SeriesAverageDto
  mineAll: SeriesAverageDto
}

export type SeriesStatsDto = {
  mainLineEpisodeTotal: number
  mainLineRuntimeSeconds: number
  hasUnknownEpisodeCounts: boolean
  mainLineAiredEpisodes: number
  extrasEpisodeTotal: number
  extrasRuntimeSeconds: number
  myWatchedEpisodes: number
  myWatchedSeconds: number
  // Orthogonal to myWatchedSeconds (design D10, add-artwork-and-title-selection):
  // the completed-rewatch-runs-plus-in-progress-run figure. "Time watched"
  // sums the two for display; myWatchedSeconds alone still feeds "time left".
  myRewatchedSeconds: number
  entriesCompleted: number
  extrasCompleted: number
  mainLineCompletedByMe: boolean
  mainLineCount: number
  extrasCount: number
  longestGapDays: number | null
  longestGapFromAnimeId: number | null
  longestGapToAnimeId: number | null
  highestMalScoreAnimeIds: number[]
  myHighestScoreAnimeIds: number[]
  mostRewatchedAnimeIds: number[]
  studios: string[]
  genres: string[]
}

export type SeriesStatus = 'Airing' | 'Ongoing' | 'Upcoming' | 'Finished'

// One version slot on the main line (rebuild-series-by-story-component
// design.md D4): alternativeAnimeIds is every alternative the slot holds, in
// watch-order tie-break order; defaultBranchHeadAnimeId is the alternative
// the slot opens on absent a reader's own pick (design.md D5) — always one of
// alternativeAnimeIds. slotKey is the lowest MAL id among the alternatives,
// the same value carried on each alternative's own SeriesEntryDto.versionSlotKey.
export type SeriesSlotDto = {
  slotKey: number
  alternativeAnimeIds: number[]
  defaultBranchHeadAnimeId: number
}

// One admissible combination of slot picks and the SeriesStatsDto it produces
// (design.md D6, task 7.3). branchHeadAnimeIds names one alternative per slot,
// positionally aligned with SeriesDto.slots — index i here is the pick for
// slots[i]. Combinations are capped at 24; beyond the cap, every slot past the
// first keeps its default pick in every combination.
export type SeriesStatsByPickDto = {
  branchHeadAnimeIds: number[]
  stats: SeriesStatsDto
}

export type SeriesDto = {
  seriesId: number
  rootAniListId: number | null
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  status: SeriesStatus
  firstYear: number | null
  lastYear: number | null
  builtAt: string
  isPartial: boolean
  isTruncated: boolean
  scores: SeriesScoresDto
  // The default combination's stats — every alternative picked at its
  // default — so a first paint needs no lookup into statsByPick. Every other
  // admissible combination, this one included, is also in statsByPick.
  stats: SeriesStatsDto
  // Empty when the main line holds no version slot at all (design.md D4).
  slots: SeriesSlotDto[]
  statsByPick: SeriesStatsByPickDto[]
  mainLine: SeriesEntryDto[]
  extras: SeriesEntryDto[]
  // The series' own overrides (null when unset) and the picker inputs
  // derived from them (artwork-selection/series-identity capabilities).
  selectedTitle: string | null
  selectedPictureUrl: string | null
  pictureOptions: string[]
  titleOptions: string[]
  picturesPendingCount: number
}

// getSeries resolves to this rather than throwing on a 404 — "not part of a
// series" is a normal, renderable outcome, distinct from a transport failure
// (which still rejects the promise, letting the page fall back to its
// generic "couldn't load" treatment).
export type SeriesLookupResult = { found: true; series: SeriesDto } | { found: false }

// The Series page card's progress badge (design.md D5 of add-series-browser;
// six-state precedence widened by polish-series-badges-and-filters design.md
// D1) — mirrors SeriesPage.tsx's own completionBadge precedence; the two
// must be changed together. behindEpisodes is populated only when badge is
// 'Behind'.
export type SeriesProgressBadge = 'None' | 'Completed' | 'CaughtUp' | 'Behind' | 'Dropped' | 'Unwatched'

// Mirrors backend Services/Series/SeriesListDto.cs (add-series-browser
// design.md D2/D7). mainLineEpisodeTotal/hasUnknownEpisodeCounts are scoped
// to the main line only, matching the series page's own episode-total stat
// and this card's main-line averages, so every episode figure on the card
// describes the same member set; entryCount is the opposite scope on
// purpose — every member, extras included, matching SeriesBadge everywhere
// else in the app. mainLineWatchedEpisodes/mainLineAiredEpisodes are the two
// figures the My progress sort divides. mainLineAiredCount is main-line
// members that have started airing (polish-search-sort-and-titles design.md
// D7/D8) — the same figure the profile's Top series filter uses — and
// mainLineAverageRank is the mean ranking position of the main-line members
// my rankings cover, null when they cover none.
export type SeriesListItemDto = {
  seriesId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  status: SeriesStatus
  progressBadge: SeriesProgressBadge
  behindEpisodes: number | null
  malMain: SeriesAverageDto
  mineMain: SeriesAverageDto
  malRevealed: boolean
  firstYear: number | null
  lastYear: number | null
  mainLineEpisodeTotal: number
  hasUnknownEpisodeCounts: boolean
  entryCount: number
  mainLineWatchedEpisodes: number
  mainLineAiredEpisodes: number
  mainLineAiredCount: number
  mainLineAverageRank: number | null
}

// Every series eligible for the Series page (add-series-browser design.md
// D1), in the endpoint's deterministic default order — my main-line average
// descending, nulls last, then raw title case-insensitively. The client
// re-sorts this same array locally when the sort control changes.
export type SeriesListDto = {
  items: SeriesListItemDto[]
}

export type AnimeDetailDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  // MAL's own main picture (artwork-selection) — feeds the picker and is
  // what "Reset" restores; pictureUrl is the picture actually displayed.
  malPictureUrl: string | null
  // The stored choice, null when there is none — the client's only signal
  // that a clear control should render.
  selectedPictureUrl: string | null
  // Every picture MAL publishes for this anime, my-list only; null/empty
  // when never fetched or MAL reports none.
  pictureUrls: string[] | null
  malScore: number | null
  rank: number | null
  popularityRank: number | null
  mediaType: string | null
  airingStatus: string | null
  totalEpisodes: number | null
  episodesAired: number | null
  airedFrom: string | null
  airedTo: string | null
  studio: string | null
  source: string | null
  averageEpisodeDurationSeconds: number | null
  genres: string[] | null
  synopsis: string | null
  background: string | null
  rating: string | null
  seasonYear: number | null
  season: string | null
  nextEpisode: NextEpisodeEtaDto | null
  aniListId: number | null
  relatedAnime: RelatedAnimeDto[]
  entry: UserAnimeEntryDto | null
  inSeries: boolean
  // True when this is a my-list anime that has never had a picture set
  // fetched — the client's cue to call refreshAnimePictures (design D4b).
  picturesFetchPending: boolean
  // True when a visit-triggered live fetch was attempted and failed — the
  // rest of this record is served from cache, not confirmed fresh.
  refreshFailed: boolean
  // Server-resolved ranked pick for each button (relation-confidence spec) —
  // includes edges MAL stored only on the other side. relatedAnime above is
  // unaffected by this and still drives the More overlay.
  prequel: ResolvedRelationDto | null
  sequel: ResolvedRelationDto | null
  parentStory: ResolvedRelationDto | null
}

// Mirrors backend Services/Recap/RecapPeriod.cs / RecapEntrySelector.cs.
export type RecapMode = 'multiYear' | 'yearly' | 'season'
export type RecapTimeFilter = 'watched' | 'aired'

export const RECAP_SEASONS = ['winter', 'spring', 'summer', 'fall'] as const
export type RecapSeasonName = (typeof RECAP_SEASONS)[number]

// getRecap's request shape — mirrors RecapDto's own period fields (startYear
// doubling as "the year" for yearly/season) so a response can be echoed
// straight back into the next request. `season`/`filter` are read only for
// the modes that use them.
export type RecapQuery = {
  mode: RecapMode
  startYear: number
  endYear: number
  season?: RecapSeasonName | null
  filter?: RecapTimeFilter
}

export type RecapHotTakeDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  myScore: number
  malScore: number
  // > 0: MAL liked it more than I did. < 0: the reverse.
  divergence: number
  malRevealed: boolean
}

export type RecapStatsDto = {
  meanScore: number | null
  animeCounted: number
  completed: number
  dropped: number
  currentlyWatching: number
  episodesWatched: number
  moviesWatched: number
  timeSpentSeconds: number
  hotTakes: RecapHotTakeDto[]
}

// One row of the recap's full included set — the client does the top-10
// slice, the basis switch, and the media-type narrowing locally (design.md
// decision 1 of add-list-recaps).
export type RecapRowDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  mediaType: string | null
  myScore: number | null
  malScore: number | null
  malRevealed: boolean
  // anime-ranking: this entry's overall rank, null when it isn't in the
  // ranking — breaks ties in the recap's top 10 and score board when the
  // ranking basis is my score.
  myRank: number | null
}

// The ranking editor's one score's full hand-orderable membership, in
// ranking order — mirrors backend Services/Ranking/IAnimeRankingService.cs
// AnimeRankingMemberDto/AnimeRankingTierDto (add-anime-ranking design.md D7).
export type AnimeRankingMemberDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  rank: number
}

export type AnimeRankingTierDto = {
  score: number
  members: AnimeRankingMemberDto[]
}

// One entry of the ranking editor's score selector — every non-empty score
// under the current scope, highest first, with its hand-orderable count.
export type AnimeRankingScoreDto = {
  score: number
  count: number
}

// getRanking's response: the score selector's contents plus the selected
// tier's full membership (null when the scope has nothing hand-orderable at
// all, or — passing an explicit score — that score has nothing).
export type AnimeRankingResponseDto = {
  scores: AnimeRankingScoreDto[]
  tier: AnimeRankingTierDto | null
}

export type RecapRankingPosterDto = {
  animeId: number
  title: string
  pictureUrl: string | null
}

// One score's best three anime of a group, in my ranking's order.
export type RecapRankingScorePostersDto = {
  score: number
  posters: RecapRankingPosterDto[]
}

// postersByScore is sparse — one entry per score the group actually holds,
// ordered from 10 down, each carrying that score's best three anime in my
// ranking's order. The All posters are not shipped separately: they are the
// first three read from the top of this list, which is the group's best
// three overall because the buckets are already score-ordered and each
// holds its own score's best (refine-ranking-posters-and-score-filters
// design.md D2). scoreCounts is the ranking's own tie-break histogram, ten
// counts in ascending score order — index 0 is score 1, index 9 is score 10.
export type RecapSeasonRankingDto = {
  year: number
  season: string
  scoredCount: number
  weightedScore: number
  postersByScore: RecapRankingScorePostersDto[]
  scoreCounts: number[]
}

export type RecapYearRankingDto = {
  year: number
  scoredCount: number
  weightedScore: number
  postersByScore: RecapRankingScorePostersDto[]
  scoreCounts: number[]
}

// One ranked season or year, largest time watched first — `season` is null
// at the year level (design.md decision 7). Unlike the score rankings, a
// group need not hold any scored anime to appear here, only watched ones.
// topPosters is always empty at the season level; at the year level it
// carries the leader's posters and is empty below rank one.
export type RecapTimeRankingDto = {
  year: number
  season: string | null
  timeSpentSeconds: number
  episodesWatched: number
  topPosters: RecapRankingPosterDto[]
}

// filter echoes what the server actually used — always 'aired' for a season
// recap. watchedCount/airedCount are this period's counts under *both*
// filters, letting the recap page render/disable the toggle without a
// second call (design.md decision 2).
export type RecapDto = {
  mode: RecapMode
  startYear: number
  endYear: number
  season: string | null
  filter: RecapTimeFilter
  watchedCount: number
  airedCount: number
  stats: RecapStatsDto
  items: RecapRowDto[]
  seasonRanking: RecapSeasonRankingDto[]
  yearRanking: RecapYearRankingDto[]
  seasonTimeRanking: RecapTimeRankingDto[]
  yearTimeRanking: RecapTimeRankingDto[]
}

export type RecapYearAvailabilityDto = {
  year: number
  watchedCount: number
  airedCount: number
}

export type RecapSeasonAvailabilityDto = {
  year: number
  season: string
  count: number
}

// Whole-list summary backing the recap picker (design.md decision 2) — the
// picker sums Years across a candidate range itself rather than asking the
// server to.
export type RecapAvailabilityDto = {
  years: RecapYearAvailabilityDto[]
  seasons: RecapSeasonAvailabilityDto[]
}

// device-transfer import (04): mirrors backend TransferImportStatusSnapshot
// and TransferImportReport (design.md D1/D13). deviceName/exportedAt name the
// file the current or most recent run is for; report/error are that run's
// outcome, whichever phase it ended in.
export type TransferImportPhase = 'NotStarted' | 'Running' | 'Complete' | 'Failed'

export type TransferReportAnimeDto = {
  animeId: number
  title: string
  englishTitle: string | null
}

export type TransferImportFailureSubject = 'Anime' | 'Series'

// title is null when this device never learned it (design.md D13 "Neither
// stored: no title").
export type TransferImportFailureDto = {
  subject: TransferImportFailureSubject
  id: number
  title: string | null
  englishTitle: string | null
  what: string
  reason: string
}

export type TransferImportReportDto = {
  rankingAdded: TransferReportAnimeDto[]
  rankingRemoved: TransferReportAnimeDto[]
  fetched: TransferReportAnimeDto[]
  failures: TransferImportFailureDto[]
}

export type TransferImportStatusDto = {
  phase: TransferImportPhase
  done: number
  total: number
  deviceName: string | null
  exportedAt: string | null
  report: TransferImportReportDto | null
  error: string | null
}
