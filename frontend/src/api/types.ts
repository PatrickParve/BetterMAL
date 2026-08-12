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

export type AnimeSearchResult = {
  id: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
}

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
}

export type SeasonRefreshResultDto = {
  refreshed: boolean
}

export type SearchPageDto = {
  query: string
  items: AnimeBrowseItemDto[]
  offset: number
  limit: number
  totalCount: number
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
}
