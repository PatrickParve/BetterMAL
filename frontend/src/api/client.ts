import type {
  ActivityFeedItemDto,
  AiringWeekDto,
  AnimeDetailDto,
  AnimeSearchResult,
  MainDashboardDto,
  MalAuthStatus,
  MyListItemDto,
  ProfileDto,
  SeasonPageDto,
  TopAnimeItemDto,
  UserAnimeEntryDto,
  UserAnimeEntryEditRequest,
} from './types.ts'

async function fetchJson<T>(input: string, init?: RequestInit): Promise<T> {
  const res = await fetch(input, init)
  if (!res.ok) throw new Error(`${input} responded with ${res.status}`)
  return res.json() as Promise<T>
}

async function fetchVoid(input: string, init?: RequestInit): Promise<void> {
  const res = await fetch(input, init)
  if (!res.ok) throw new Error(`${input} responded with ${res.status}`)
}

export function getMalAuthStatus(): Promise<MalAuthStatus> {
  return fetchJson<MalAuthStatus>('/api/mal-auth/status')
}

export function searchAnime(query: string, signal?: AbortSignal): Promise<AnimeSearchResult[]> {
  return fetchJson<AnimeSearchResult[]>(`/api/anime/search?q=${encodeURIComponent(query)}`, { signal })
}

export function updateEntry(animeId: number, request: UserAnimeEntryEditRequest): Promise<UserAnimeEntryDto> {
  return fetchJson<UserAnimeEntryDto>(`/api/anime/${animeId}/entry`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export function getDashboard(): Promise<MainDashboardDto> {
  return fetchJson<MainDashboardDto>('/api/dashboard')
}

// `week` is any ISO date (yyyy-MM-dd) inside the desired week; omit for the
// current week.
export function getAiringWeek(week?: string): Promise<AiringWeekDto> {
  const query = week ? `?week=${encodeURIComponent(week)}` : ''
  return fetchJson<AiringWeekDto>(`/api/airing${query}`)
}

export function getSeasonPage(
  year: number,
  season: string,
  params: { sort: string; offset: number; limit: number },
): Promise<SeasonPageDto> {
  const query = new URLSearchParams({
    sort: params.sort,
    offset: String(params.offset),
    limit: String(params.limit),
  })
  return fetchJson<SeasonPageDto>(`/api/season/${year}/${season}?${query.toString()}`)
}

export function getMyList(): Promise<MyListItemDto[]> {
  return fetchJson<MyListItemDto[]>('/api/my-list')
}

export function getTopAnime(): Promise<TopAnimeItemDto[]> {
  return fetchJson<TopAnimeItemDto[]>('/api/top-anime')
}

export function getProfile(): Promise<ProfileDto> {
  return fetchJson<ProfileDto>('/api/profile')
}

export function getActivityHistory(): Promise<ActivityFeedItemDto[]> {
  return fetchJson<ActivityFeedItemDto[]>('/api/profile/activity')
}

export function putTopAnimeSelection(animeIds: number[]): Promise<void> {
  return fetchVoid('/api/top-anime/selection', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ animeIds }),
  })
}

export function getAnimeDetail(animeId: number): Promise<AnimeDetailDto> {
  return fetchJson<AnimeDetailDto>(`/api/anime/${animeId}`)
}

export function refreshAnime(animeId: number): Promise<void> {
  return fetchVoid(`/api/anime/${animeId}/refresh`, { method: 'POST' })
}
