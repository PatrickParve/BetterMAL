import type { AnimeUpdateDto } from '../../api/types.ts'

// Same `bettermal.*` per-browser preference namespace ScoreVisibilityContext
// and ContentFilterContext use. Holds the ISO `detectedAt` of the newest
// update the user has been shown — one high-water mark rather than a set of
// seen ids, since the feed is strictly ordered by detection and never
// back-fills (design.md D2).
const STORAGE_KEY = 'bettermal.updatesSeenAt'

export function readSeenMarker(): string | null {
  if (typeof window === 'undefined') return null
  return window.localStorage.getItem(STORAGE_KEY)
}

// No marker stored means everything in the window counts as unseen.
export function hasUnseen(items: AnimeUpdateDto[], marker: string | null): boolean {
  if (items.length === 0) return false
  if (marker === null) return true
  return items.some((item) => item.detectedAt > marker)
}

// Both sides of every comparison are server-issued `detectedAt` values, so
// they order lexically and browser clock skew never enters into it.
export function markSeen(items: AnimeUpdateDto[]): string | null {
  if (items.length === 0) return readSeenMarker()
  const newest = items.reduce((latest, item) => (item.detectedAt > latest ? item.detectedAt : latest), items[0].detectedAt)
  window.localStorage.setItem(STORAGE_KEY, newest)
  return newest
}
