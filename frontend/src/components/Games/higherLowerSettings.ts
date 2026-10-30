import type { HigherOrLowerListScope, HigherOrLowerSettings } from '../../api/types.ts'
import { MEDIA_TYPE_ORDER } from '../../utils/anime.ts'

// What the setup screen chooses. Hide NSFW is the app-wide setting, read when a
// game starts, so it isn't one of these (add-games-higher-or-lower design.md D2:
// a game keeps the Hide NSFW setting it started with).
export type HigherOrLowerLobbySettings = Omit<HigherOrLowerSettings, 'hideHentai'>

export const LIST_SCOPES: { value: HigherOrLowerListScope; label: string }[] = [
  { value: 'All', label: 'All anime' },
  { value: 'OnlyMyList', label: 'Only my list' },
  { value: 'NotOnMyList', label: 'Not in my list' },
]

export const DEFAULT_SETTINGS: HigherOrLowerLobbySettings = {
  yearFrom: null,
  yearTo: null,
  mediaTypes: null,
  list: 'All',
}

// A per-browser preference, as bettermal.hideNsfw is, so nothing on the server
// has to outlive the game for the setup screen to remember it (design.md D2).
const STORAGE_KEY = 'bettermal.higherOrLower.settings'

function asYear(value: unknown): number | null {
  return typeof value === 'number' && Number.isInteger(value) ? value : null
}

// Reads what was last played with, field by field, so a stored value that no
// longer means anything (a type that was renamed, a hand-edited entry) falls
// back to its default rather than breaking the screen. Storage can throw (a
// private window, blocked site data), and then there is simply nothing stored.
export function readStoredSettings(): HigherOrLowerLobbySettings {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (raw === null) return DEFAULT_SETTINGS
    const stored = JSON.parse(raw) as Record<string, unknown> | null
    if (stored === null || typeof stored !== 'object') return DEFAULT_SETTINGS
    const types = Array.isArray(stored.mediaTypes)
      ? MEDIA_TYPE_ORDER.filter((type) => (stored.mediaTypes as unknown[]).includes(type))
      : []
    const yearFrom = asYear(stored.yearFrom)
    const yearTo = asYear(stored.yearTo)
    return {
      yearFrom: yearFrom !== null && yearTo !== null ? Math.min(yearFrom, yearTo) : yearFrom,
      yearTo: yearFrom !== null && yearTo !== null ? Math.max(yearFrom, yearTo) : yearTo,
      mediaTypes: types.length === 0 || types.length === MEDIA_TYPE_ORDER.length ? null : types,
      list: LIST_SCOPES.find((scope) => scope.value === stored.list)?.value ?? DEFAULT_SETTINGS.list,
    }
  } catch {
    return DEFAULT_SETTINGS
  }
}

export function writeStoredSettings(settings: HigherOrLowerLobbySettings): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
  } catch {
    // Nothing to remember it in: the next visit opens on the defaults.
  }
}

// A year the stored anime no longer cover (the list changed, or Hide NSFW
// narrowed it) can't be chosen in the selects, so it counts as Any instead of
// leaving a select showing a value it doesn't offer.
export function withinYears(
  settings: HigherOrLowerLobbySettings,
  years: { min: number; max: number } | null,
): HigherOrLowerLobbySettings {
  const inside = (year: number | null) => (year !== null && years !== null && year >= years.min && year <= years.max ? year : null)
  const yearFrom = inside(settings.yearFrom)
  const yearTo = inside(settings.yearTo)
  if (yearFrom === settings.yearFrom && yearTo === settings.yearTo) return settings
  return { ...settings, yearFrom, yearTo }
}

// Setting one end past the other moves the other to match, so the range is
// never backwards (higher-or-lower "The setup screen sets what a game plays with").
export function withYearFrom(settings: HigherOrLowerLobbySettings, year: number | null): HigherOrLowerLobbySettings {
  const yearTo = year !== null && settings.yearTo !== null && year > settings.yearTo ? year : settings.yearTo
  return { ...settings, yearFrom: year, yearTo }
}

export function withYearTo(settings: HigherOrLowerLobbySettings, year: number | null): HigherOrLowerLobbySettings {
  const yearFrom = year !== null && settings.yearFrom !== null && year < settings.yearFrom ? year : settings.yearFrom
  return { ...settings, yearFrom, yearTo: year }
}

// With All pressed (null), pressing a type selects that type alone. Later
// presses add or remove types. Removing the last one, or pressing every type,
// returns to All. Kept in MEDIA_TYPE_ORDER so the same choice is the same list.
export function withTypeToggled(settings: HigherOrLowerLobbySettings, type: string): HigherOrLowerLobbySettings {
  if (settings.mediaTypes === null) return { ...settings, mediaTypes: [type] }
  const chosen = settings.mediaTypes.includes(type)
    ? settings.mediaTypes.filter((t) => t !== type)
    : [...settings.mediaTypes, type]
  const ordered = MEDIA_TYPE_ORDER.filter((t) => chosen.includes(t))
  return { ...settings, mediaTypes: ordered.length === 0 || ordered.length === MEDIA_TYPE_ORDER.length ? null : ordered }
}

// The pool read's identity: everything the count depends on.
export function settingsKey(settings: HigherOrLowerLobbySettings, hideHentai: boolean): string {
  return JSON.stringify([settings.yearFrom, settings.yearTo, settings.mediaTypes, settings.list, hideHentai])
}
