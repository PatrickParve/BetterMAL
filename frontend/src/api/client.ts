import type {
  ActivityFeedItemDto,
  AiringFullRefreshStatusDto,
  AiringWeekDto,
  AnimeDetailDto,
  AnimeSearchResult,
  HealthStatus,
  MainDashboardDto,
  MalAuthStatus,
  MyListItemDto,
  PendingReconciliationDiffDto,
  ProfileDto,
  ReconciliationResultDto,
  ResyncStatusDto,
  RewatchedSectionDto,
  SearchPageDto,
  SeasonPageDto,
  SeasonRefreshResultDto,
  SyncStatusDto,
  TopAnimeItemDto,
  TopAnimeMediaType,
  TopAnimeSectionDto,
  UserAnimeEntryDto,
  UserAnimeEntryEditRequest,
} from './types.ts'
import { reportReachable, reportUnreachable } from './connectionStatus.ts'

// GETs in flight, keyed by URL: a GET issued while an identical one is still
// outstanding joins it instead of opening a second request (kills React
// StrictMode's double-mount fetch, double-mounts, and rapid re-navigation
// duplicates in one place, for every page). Mutations are never collapsed —
// two identical in-flight PATCHes are two intended edits. The map stores the
// shared *Response* promise, not its parsed body, since fetchRaw is also used
// directly by callers that branch on res.status before parsing; every
// consumer (including the request that started it) reads its own res.clone(),
// since a Response body can only be consumed once. The entry is deleted as
// soon as the request settles, so nothing is cached: a read issued after the
// previous one completed is a fresh request.
const inFlightGets = new Map<string, Promise<Response>>()

async function fetchRaw(input: string, init?: RequestInit): Promise<Response> {
  const isGet = !init?.method || init.method.toUpperCase() === 'GET'
  if (!isGet) return performFetch(input, init)

  const existing = inFlightGets.get(input)
  if (existing) return (await existing).clone()

  const promise = performFetch(input, init)
  inFlightGets.set(input, promise)
  try {
    return (await promise).clone()
  } finally {
    inFlightGets.delete(input)
  }
}

// A response of any kind below 500 proves the backend answered, so it counts
// as reachable even when it's an error the caller will go on to throw for
// (404, 400, 401, ...) — those are ordinary application outcomes, not an outage.
// Runs at most once per de-duplicated group, so reachability is reported once
// per real network attempt, not once per joined caller.
async function performFetch(input: string, init?: RequestInit): Promise<Response> {
  let res: Response
  try {
    res = await fetch(input, init)
  } catch (err) {
    reportUnreachable()
    throw err
  }
  if (res.status >= 500) reportUnreachable()
  else reportReachable()
  return res
}

async function fetchJson<T>(input: string, init?: RequestInit): Promise<T> {
  const res = await fetchRaw(input, init)
  if (!res.ok) throw new Error(`${input} responded with ${res.status}`)
  return res.json() as Promise<T>
}

async function fetchVoid(input: string, init?: RequestInit): Promise<void> {
  const res = await fetchRaw(input, init)
  if (!res.ok) throw new Error(`${input} responded with ${res.status}`)
}

export function getMalAuthStatus(): Promise<MalAuthStatus> {
  return fetchJson<MalAuthStatus>('/api/mal-auth/status')
}

// Touches neither the database nor MAL — a true "is the process up" probe.
// Goes through fetchJson like every other call, so its own success/failure
// is what reports recovery to connectionStatus; no separate reporting needed.
export function getHealth(): Promise<HealthStatus> {
  return fetchJson<HealthStatus>('/api/health')
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

export function deleteEntry(animeId: number): Promise<void> {
  return fetchVoid(`/api/anime/${animeId}/entry`, { method: 'DELETE' })
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
  params: { sort: string; includeMyList: boolean; hideHentai: boolean; offset: number; limit: number },
): Promise<SeasonPageDto> {
  const query = new URLSearchParams({
    sort: params.sort,
    includeMyList: String(params.includeMyList),
    hideHentai: String(params.hideHentai),
    offset: String(params.offset),
    limit: String(params.limit),
  })
  return fetchJson<SeasonPageDto>(`/api/season/${year}/${season}?${query.toString()}`)
}

export function refreshSeason(year: number, season: string): Promise<SeasonRefreshResultDto> {
  return fetchJson<SeasonRefreshResultDto>(`/api/season/${year}/${season}/refresh`, { method: 'POST' })
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

export function getTopAnimeSection(mediaType: TopAnimeMediaType): Promise<TopAnimeSectionDto> {
  return fetchJson<TopAnimeSectionDto>(`/api/profile/top-anime?mediaType=${encodeURIComponent(mediaType)}`)
}

export function getRewatchedSection(mediaType: TopAnimeMediaType): Promise<RewatchedSectionDto> {
  return fetchJson<RewatchedSectionDto>(`/api/profile/rewatched?mediaType=${encodeURIComponent(mediaType)}`)
}

export function putTopAnimeOrder(
  mediaType: TopAnimeMediaType,
  tiers: { score: number; animeIds: number[] }[],
): Promise<void> {
  return fetchVoid('/api/top-anime/order', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ mediaType, tiers }),
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
  const res = await fetchRaw('/api/sync/reconcile/pending')
  if (res.status === 204) return null
  if (!res.ok) throw new Error(`/api/sync/reconcile/pending responded with ${res.status}`)
  return res.json() as Promise<PendingReconciliationDiffDto>
}

// 404 (nothing pending to accept/cancel) resolves to false rather than throwing.
export async function acceptReconciliationDiff(): Promise<boolean> {
  const res = await fetchRaw('/api/sync/reconcile/accept', { method: 'POST' })
  if (res.status === 404) return false
  if (!res.ok) throw new Error(`/api/sync/reconcile/accept responded with ${res.status}`)
  return true
}

export async function cancelReconciliationDiff(): Promise<boolean> {
  const res = await fetchRaw('/api/sync/reconcile/cancel', { method: 'POST' })
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

// Manual "refresh all airing data" (settings page): kicks off a background
// run, paced through AniList's rate limit and skipping finished shows already
// fetched before, rather than waiting on it; poll getAiringFullRefreshStatus
// for progress.
export function triggerAiringFullRefresh(): Promise<AiringFullRefreshStatusDto> {
  return fetchJson<AiringFullRefreshStatusDto>('/api/airing/refresh-all', { method: 'POST' })
}

export function getAiringFullRefreshStatus(): Promise<AiringFullRefreshStatusDto> {
  return fetchJson<AiringFullRefreshStatusDto>('/api/airing/refresh-all/status')
}
