import type { WatchStatus } from '../api/types.ts'

// Prefer the English title wherever an anime title is displayed, falling
// back to the default (usually romaji/native) title when MAL has none.
export function pickDisplayTitle(title: string, englishTitle: string | null | undefined): string {
  return englishTitle && englishTitle.trim().length > 0 ? englishTitle : title
}

export const STATUS_LABELS: Record<WatchStatus, string> = {
  Watching: 'Watching',
  OnHold: 'On hold',
  PlanToWatch: 'Plan to watch',
  Completed: 'Completed',
  Dropped: 'Dropped',
}

// Modifier-class suffix per status, used to color-code list rows and filter
// tabs (watching = green, completed = blue, plan to watch = purple, on hold
// = yellow, dropped = red) via the --status-* variables in index.css.
export const STATUS_CLASS: Record<WatchStatus, string> = {
  Watching: 'watching',
  OnHold: 'onhold',
  PlanToWatch: 'plantowatch',
  Completed: 'completed',
  Dropped: 'dropped',
}

// Compact airing-status labels for the My list Plan-to-watch badge. The
// detail page uses its own longer phrasing (AIRING_STATUS_LABELS in
// AnimeDetailPage.tsx) — the list wants something terse enough to sit next
// to the media type.
const AIRING_STATUS_SHORT_LABELS: Record<string, string> = {
  not_yet_aired: 'Not aired',
  currently_airing: 'Airing',
  finished_airing: 'Aired',
}

export function airingStatusShortLabel(status: string | null | undefined): string | null {
  if (!status) return null
  return AIRING_STATUS_SHORT_LABELS[status] ?? null
}

export const CHANGE_TYPE_LABELS: Record<string, string> = {
  Added: 'Added',
  StatusChanged: 'Status changed',
  EpisodeIncremented: 'Episode watched',
  ScoreChanged: 'Score changed',
  Completed: 'Completed',
  RewatchCountChanged: 'Rewatch count changed',
}

// "Never" is settings-page phrasing for a field that's always present
// elsewhere, so callers that don't want it pass a null value instead.
export function formatTimestamp(value: string | null): string {
  if (!value) return 'Never'
  return new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

// Shared comparators for the "sort by X" dropdowns on My list and Current
// season — highest MAL score first, unranked (`null`) sorting last.
export function compareByMalScoreDesc<T extends { malScore: number | null }>(a: T, b: T): number {
  return (b.malScore ?? -1) - (a.malScore ?? -1)
}

export function compareByTitleAlphabetical<T extends { title: string }>(a: T, b: T): number {
  return a.title.localeCompare(b.title)
}

export type AiringStatus = 'finished_airing' | 'currently_airing' | 'not_yet_aired'

export const AIRING_STATUS_LABELS: Record<AiringStatus, string> = {
  finished_airing: 'Finished airing',
  currently_airing: 'Currently airing',
  not_yet_aired: 'Not yet aired',
}

// Base cycle the "show first" picker rotates through, e.g. choosing
// "currently_airing" first yields Currently airing -> Not yet aired ->
// Finished airing, keeping the other two statuses' relative order.
const AIRING_STATUS_CYCLE: AiringStatus[] = ['finished_airing', 'currently_airing', 'not_yet_aired']

export function compareByAiringStatus<T extends { airingStatus: string | null }>(
  first: AiringStatus,
): (a: T, b: T) => number {
  const startIndex = AIRING_STATUS_CYCLE.indexOf(first)
  const order = [...AIRING_STATUS_CYCLE.slice(startIndex), ...AIRING_STATUS_CYCLE.slice(0, startIndex)]
  return (a, b) => {
    const rankA = a.airingStatus ? order.indexOf(a.airingStatus as AiringStatus) : -1
    const rankB = b.airingStatus ? order.indexOf(b.airingStatus as AiringStatus) : -1
    return (rankA === -1 ? order.length : rankA) - (rankB === -1 ? order.length : rankB)
  }
}
