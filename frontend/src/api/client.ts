import type {
  AiringWeekDto,
  AnimeSearchResult,
  MainDashboardDto,
  MalAuthStatus,
  SeasonPageDto,
  UserAnimeEntryDto,
  UserAnimeEntryEditRequest,
} from './types.ts'

async function fetchJson<T>(input: string, init?: RequestInit): Promise<T> {
  const res = await fetch(input, init)
  if (!res.ok) throw new Error(`${input} responded with ${res.status}`)
  return res.json() as Promise<T>
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
