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
  pictureUrl: string | null
  episodesWatched: number
  totalEpisodes: number | null
  nextEpisode: NextEpisodeEtaDto | null
}

export type AiringTodayItemDto = {
  animeId: number
  title: string
  pictureUrl: string | null
  localTime: string
}

export type CurrentSeasonItemDto = {
  animeId: number
  title: string
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

export type SeasonAnimeItemDto = {
  animeId: number
  title: string
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
  items: SeasonAnimeItemDto[]
  offset: number
  limit: number
  totalCount: number
}
