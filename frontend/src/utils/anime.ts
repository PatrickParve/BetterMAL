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
