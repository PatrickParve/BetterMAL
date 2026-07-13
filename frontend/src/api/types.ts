export type WatchStatus = 'Watching' | 'OnHold' | 'PlanToWatch' | 'Completed' | 'Dropped'

export type MalAuthStatus = {
  connected: boolean
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
  nextEpisode: NextEpisodeEtaDto | null
}

export type AiringTodayItemDto = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
  localTime: string
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
}

export type SeasonPageDto = {
  year: number
  season: string
  items: AnimeBrowseItemDto[]
  offset: number
  limit: number
  totalCount: number
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

// Items is the resolved "My top anime" list. Candidates/tieBreakSlots are
// only populated when the fill boundary lands mid-tier (an actual tie to
// break) — that's what the selection overlay (15.6) is built from.
export type TopAnimeSectionDto = {
  items: TopAnimeEntryDto[]
  tieBreakSlots: number
  candidates: TopAnimeEntryDto[]
  selectedAnimeIds: number[]
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
}

export type ProfileDto = {
  stats: AnimeStatsDto
  recentActivity: ActivityFeedItemDto[]
  topAnime: TopAnimeSectionDto
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
  airedFrom: string | null
  airedTo: string | null
  studio: string | null
  source: string | null
  averageEpisodeDurationSeconds: number | null
  genres: string[] | null
  synopsis: string | null
  background: string | null
  prequelMalId: number | null
  prequelTitle: string | null
  sequelMalId: number | null
  sequelTitle: string | null
  entry: UserAnimeEntryDto | null
}
