export type WatchStatus = 'Watching' | 'OnHold' | 'PlanToWatch' | 'Completed' | 'Dropped'

export type MalAuthStatus = {
  connected: boolean
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
}

// The dropdown's "series" row (design.md decision 5) — id is the series id,
// rootAnimeId is the navigation target, entryCount covers every member.
export type SeriesSearchResult = {
  kind: 'series'
  id: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  rootAnimeId: number
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
}

export type SeasonPageDto = {
  year: number
  season: string
  items: AnimeBrowseItemDto[]
  offset: number
  limit: number
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

// The results page's series row — separate shape from SeriesSearchResult
// (no `kind`) since it rides in its own `series` array rather than a
// discriminated `items` union (design.md decision 5).
export type SeriesSearchResultDto = {
  seriesId: number
  rootAnimeId: number
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
  meanScore: number | null
  watching: number
  completed: number
  onHold: number
  dropped: number
  planToWatch: number
  totalEntries: number
  rewatched: number
  episodes: number
}

export type TopAnimeEntryDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  myScore: number
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

export type ProfileDto = {
  stats: AnimeStatsDto
  recentActivity: ActivityFeedItemDto[]
  topAnime: TopAnimeSectionDto
  rewatched: RewatchedSectionDto
  scoreDistribution: ScoreDistributionDto
  theyLikedItIDidnt: OpinionDivergenceItemDto[]
  iLikedItTheyDidnt: OpinionDivergenceItemDto[]
}

// One franchise ranked by Top series: display fields from its root anime,
// its total member count, and its two main-series averages (SeriesAverageDto,
// defined below) — identical in shape and computation to the series page's
// `MAL · main series`/`Mine · main series` chips (design.md decision 1).
// malRevealed is the server-computed "may this be shown under 'always show
// completed scores'" boolean (design.md decision 5) — pass it straight into
// ScoreValue's `completed` prop.
export type TopSeriesItemDto = {
  seriesId: number
  rootAnimeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  entryCount: number
  mainLineAiredCount: number
  malMain: SeriesAverageDto
  mineMain: SeriesAverageDto
  malRevealed: boolean
}

// Every series with at least one member in my list (design.md decision 2),
// pre-ordered by my main-series average descending, then scored main-line
// count descending, then raw title case-insensitively — the client re-sorts
// and re-filters this same array locally when the ranking basis is switched
// (design.md decision 4).
export type TopSeriesSectionDto = {
  items: TopSeriesItemDto[]
}

export type SyncStatusDto = {
  pendingCount: number
  lastSyncedAt: string | null
}

export type ResyncPhase = 'NotStarted' | 'Running' | 'Complete'

export type ResyncStatusDto = {
  phase: ResyncPhase
  synced: number
  total: number
}

export type AiringFullRefreshPhase = 'NotStarted' | 'Running' | 'Complete'

export type AiringFullRefreshStatusDto = {
  phase: AiringFullRefreshPhase
  synced: number
  total: number
}

export type SeriesBulkBuildPhase = 'NotStarted' | 'Running' | 'Complete' | 'Failed'

// Progress of the settings page's "Build all series from my list" action.
// Built counts targets processed, not builds run — one build can cover
// several other targets' membership at once (design.md decision 6).
export type SeriesBulkBuildStatusDto = {
  phase: SeriesBulkBuildPhase
  built: number
  total: number
}

export type ReconciliationResultDto = {
  added: number
  updated: number
  unchanged: number
  skippedPending: number
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
}

// Mirrors backend Services/Series/SeriesDto.cs (tasks 3.1-3.3). airedEpisodes
// is null-means-unknown (design.md decision 1 of redesign-series-page):
// finished -> total, airing -> schedule reader clamped to total (null when
// unknown), not yet aired -> 0, unknown airing status -> null.
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

export type SeriesStatus = 'Ongoing' | 'Upcoming' | 'Finished'

export type SeriesDto = {
  seriesId: number
  rootAnimeId: number
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
  stats: SeriesStatsDto
  mainLine: SeriesEntryDto[]
  extras: SeriesEntryDto[]
}

// getSeries resolves to this rather than throwing on a 404 — "not part of a
// series" is a normal, renderable outcome, distinct from a transport failure
// (which still rejects the promise, letting the page fall back to its
// generic "couldn't load" treatment).
export type SeriesLookupResult = { found: true; series: SeriesDto } | { found: false }

export type AnimeDetailDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
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
}

export type RecapRankingPosterDto = {
  animeId: number
  title: string
  pictureUrl: string | null
}

// topPosters is empty for every row but the leader (rank 1).
export type RecapSeasonRankingDto = {
  year: number
  season: string
  scoredCount: number
  weightedScore: number
  topPosters: RecapRankingPosterDto[]
}

export type RecapYearRankingDto = {
  year: number
  scoredCount: number
  weightedScore: number
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
