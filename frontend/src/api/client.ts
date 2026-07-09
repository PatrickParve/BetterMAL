import type {
  ActivityFeedItemDto,
  AiringWeekDto,
  AnimeDetailDto,
  AnimeSearchResult,
  MainDashboardDto,
  MalAuthStatus,
  MyListItemDto,
  PendingReconciliationDiffDto,
  ProfileDto,
  ReconciliationResultDto,
  ResyncStatusDto,
  SearchPageDto,
  SeasonPageDto,
  SyncStatusDto,
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

export function getSearchPage(
  query: string,
  params: { sort: string; offset: number; limit: number },
): Promise<SearchPageDto> {
  const search = new URLSearchParams({
    q: query,
    sort: params.sort,
    offset: String(params.offset),
    limit: String(params.limit),
  })
  return fetchJson<SearchPageDto>(`/api/anime/search/page?${search.toString()}`)
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

export function getSyncStatus(): Promise<SyncStatusDto> {
  return fetchJson<SyncStatusDto>('/api/sync/status')
}

export function syncNow(): Promise<{ pushed: number }> {
  return fetchJson<{ pushed: number }>('/api/sync/now', { method: 'POST' })
}

export function runReconciliation(): Promise<ReconciliationResultDto> {
  return fetchJson<ReconciliationResultDto>('/api/sync/reconcile', { method: 'POST' })
}

// 204 (no pending diff) resolves to null rather than throwing.
export async function getPendingReconciliationDiff(): Promise<PendingReconciliationDiffDto | null> {
  const res = await fetch('/api/sync/reconcile/pending')
  if (res.status === 204) return null
  if (!res.ok) throw new Error(`/api/sync/reconcile/pending responded with ${res.status}`)
  return res.json() as Promise<PendingReconciliationDiffDto>
}

// 404 (nothing pending to accept/cancel) resolves to false rather than throwing.
export async function acceptReconciliationDiff(): Promise<boolean> {
  const res = await fetch('/api/sync/reconcile/accept', { method: 'POST' })
  if (res.status === 404) return false
  if (!res.ok) throw new Error(`/api/sync/reconcile/accept responded with ${res.status}`)
  return true
}

export async function cancelReconciliationDiff(): Promise<boolean> {
  const res = await fetch('/api/sync/reconcile/cancel', { method: 'POST' })
  if (res.status === 404) return false
  if (!res.ok) throw new Error(`/api/sync/reconcile/cancel responded with ${res.status}`)
  return true
}

// One-time corrective re-sync: kicks off a background run (~1 req/s per
// anime) rather than waiting on it; poll getResyncFromMalStatus for progress.
export function triggerResyncFromMal(): Promise<ResyncStatusDto> {
  return fetchJson<ResyncStatusDto>('/api/sync/resync-from-mal', { method: 'POST' })
}

export function getResyncFromMalStatus(): Promise<ResyncStatusDto> {
  return fetchJson<ResyncStatusDto>('/api/sync/resync-from-mal/status')
}
