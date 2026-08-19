import { RECAP_SEASONS, type RecapSeasonName, type WatchStatus } from '../api/types.ts'

// Prefer the English title wherever an anime title is displayed, falling
// back to the default (usually romaji/native) title when MAL has none.
export function pickDisplayTitle(title: string, englishTitle: string | null | undefined): string {
  return englishTitle && englishTitle.trim().length > 0 ? englishTitle : title
}

// Mirrors backend/AnimeTracker.Api/Services/Series/SeriesRelations.cs
// TraversalSet — kept in sync by hand rather than fetched, since the detail
// page only needs a yes/no check on relation types it already has loaded.
export const SERIES_TRAVERSAL_RELATIONS = new Set([
  'sequel',
  'prequel',
  'side_story',
  'parent_story',
  'summary',
  'full_story',
  'spin_off',
  'alternative_version',
])

// score-visibility: "always show MAL scores for completed and dropped
// shows" reveals a score when the entry is Completed or Dropped — both are
// statuses in which the user has settled their relationship with the anime,
// so a community average can no longer bias or spoil a viewing still ahead
// of them.
export function isScoreRevealableStatus(status: WatchStatus | null | undefined): boolean {
  return status === 'Completed' || status === 'Dropped'
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

// Display labels for the raw `mediaType` values MAL returns — used by the my
// list type filter and by list rows, which used to print the raw value
// (`TV_SPECIAL`) directly.
const MEDIA_TYPE_LABELS: Record<string, string> = {
  tv: 'TV',
  movie: 'Movie',
  ova: 'OVA',
  ona: 'ONA',
  special: 'Special',
  tv_special: 'TV special',
  music: 'Music',
  cm: 'CM',
  pv: 'PV',
  unknown: 'Unknown',
}

// Display order for the my-list type filter's options — the known types in a
// fixed, sensible order (not raw-value alphabetical, which would separate
// "TV" from "TV special"); "Unknown" is added separately by the filter,
// only when the list actually contains an untyped entry.
export const MEDIA_TYPE_ORDER = Object.keys(MEDIA_TYPE_LABELS).filter((key) => key !== 'unknown')

// Falls back to a prettified form of any value MAL adds that isn't mapped
// above yet, the same underscore-to-space-and-capitalize transform
// RelatedAnimeOverlay's relationLabel uses for unrecognized relation types.
export function mediaTypeLabel(raw: string | null | undefined): string {
  if (!raw) return 'Unknown'
  const known = MEDIA_TYPE_LABELS[raw]
  if (known) return known
  const spaced = raw.replace(/_/g, ' ')
  return spaced.charAt(0).toUpperCase() + spaced.slice(1)
}

// Capitalizes a season name for display — e.g. "fall" -> "Fall". Shared by
// RecapPage (period label, ranking rows) and MyListPage (the scope
// indicator), both of which name a recap's season the same way.
export function seasonLabel(season: string): string {
  return season.charAt(0).toUpperCase() + season.slice(1)
}

// Steps a (year, season) point by `delta` seasons, crossing year boundaries
// as needed — e.g. delta=1 from fall rolls into winter of the next year.
// Moved out of SeasonPage.tsx (polish-recap-page design.md decision 7) so
// RecapPage's period stepper can share the same "what is the season after
// fall" arithmetic rather than reimplementing it.
export function shiftSeason(
  year: number,
  season: RecapSeasonName,
  delta: number,
): { year: number; season: RecapSeasonName } {
  const total = year * 4 + RECAP_SEASONS.indexOf(season) + delta
  const nextYear = Math.floor(total / 4)
  const nextIndex = ((total % 4) + 4) % 4
  return { year: nextYear, season: RECAP_SEASONS[nextIndex] }
}

// A single monotonically increasing integer for a (year, season) point, so a
// ceiling/floor can be compared against a viewed season with plain integer
// arithmetic — mirrors the backend's SeasonCalendar.GetSeasonPointIndex.
export function seasonPointIndex(year: number, season: RecapSeasonName): number {
  return year * 4 + RECAP_SEASONS.indexOf(season)
}

// e.g. 1548 minutes -> "1d 1h 48min" — once a day is present the hours
// segment always shows, even at 0h, matching the series page design spec's
// own example ("4d 6h 30min"). Shared by SeriesPage (main-line/extras/
// watched runtime) and RecapPage (time spent) — both derive seconds the
// same way (EpisodesWatched x per-episode duration).
export function formatRuntime(totalSeconds: number): string {
  const totalMinutes = Math.round(totalSeconds / 60)
  const days = Math.floor(totalMinutes / (24 * 60))
  const hours = Math.floor((totalMinutes % (24 * 60)) / 60)
  const minutes = totalMinutes % 60
  const parts: string[] = []
  if (days > 0) parts.push(`${days}d`)
  if (days > 0 || hours > 0) parts.push(`${hours}h`)
  parts.push(`${minutes}min`)
  return parts.join(' ')
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

// My list's two-level sort (D5): a primary key with an optional tiebreaker,
// each direction-aware and built so a missing value never leads the list.
export type SortKey =
  | 'alphabetical'
  | 'myScore'
  | 'malScore'
  | 'episodesWatched'
  | 'progress'
  | 'totalEpisodes'
  | 'airingStatus'
  | 'type'
  | 'startDate'
  | 'finishDate'

// 'natural' is each key's own natural order (descending for scores/counts/
// dates, ascending for alphabetical/type, the airing-status cycle for airing
// status); 'reversed' flips it. The tiebreaker always applies in 'natural'.
export type SortDirection = 'natural' | 'reversed'

export type SortableListItem = {
  title: string
  mediaType: string | null
  malScore: number | null
  airingStatus: string | null
  totalEpisodes: number | null
  entry: {
    myScore: number | null
    episodesWatched: number
    startedAt: string | null
    completedAt: string | null
  }
}

type Comparator<T> = (a: T, b: T) => number

// Resolves presence *before* the direction-sensitive comparison: both
// missing -> tie, one missing -> it goes last, otherwise compare and apply
// direction. Keeps "missing sorts last in both directions" structurally
// impossible to get wrong, rather than relying on every comparator to
// remember it.
function nullsLast<T, V>(
  get: (item: T) => V | null | undefined,
  compare: (a: V, b: V) => number,
): (direction: SortDirection) => Comparator<T> {
  return (direction) => (a, b) => {
    const va = get(a)
    const vb = get(b)
    if (va == null && vb == null) return 0
    if (va == null) return 1
    if (vb == null) return -1
    const result = compare(va, vb)
    return direction === 'reversed' ? -result : result
  }
}

function dateValue(iso: string | null): number | null {
  return iso ? new Date(iso).getTime() : null
}

// Airing status isn't here — its order additionally depends on the "show
// first" control, so it's resolved separately in sortComparator below,
// reusing compareByAiringStatus rather than duplicating its cycle logic.
const SORT_KEY_FACTORIES: Record<
  Exclude<SortKey, 'airingStatus'>,
  (direction: SortDirection) => Comparator<SortableListItem>
> = {
  alphabetical: nullsLast<SortableListItem, string>(
    (item) => item.title,
    (a, b) => a.localeCompare(b),
  ),
  myScore: nullsLast<SortableListItem, number>(
    (item) => item.entry.myScore,
    (a, b) => b - a,
  ),
  malScore: nullsLast<SortableListItem, number>(
    (item) => item.malScore,
    (a, b) => b - a,
  ),
  episodesWatched: nullsLast<SortableListItem, number>(
    (item) => item.entry.episodesWatched,
    (a, b) => b - a,
  ),
  // An unknown total counts as missing progress, not zero progress.
  progress: nullsLast<SortableListItem, number>(
    (item) => (item.totalEpisodes ? item.entry.episodesWatched / item.totalEpisodes : null),
    (a, b) => b - a,
  ),
  totalEpisodes: nullsLast<SortableListItem, number>(
    (item) => item.totalEpisodes,
    (a, b) => b - a,
  ),
  // Sorts on the display label so the order matches what the row shows.
  type: nullsLast<SortableListItem, string>(
    (item) => mediaTypeLabel(item.mediaType),
    (a, b) => a.localeCompare(b),
  ),
  startDate: nullsLast<SortableListItem, number>(
    (item) => dateValue(item.entry.startedAt),
    (a, b) => b - a,
  ),
  finishDate: nullsLast<SortableListItem, number>(
    (item) => dateValue(item.entry.completedAt),
    (a, b) => b - a,
  ),
}

export function sortComparator(
  key: SortKey,
  direction: SortDirection,
  airingStatusFirst: AiringStatus,
): Comparator<SortableListItem> {
  if (key === 'airingStatus') {
    const cmp = compareByAiringStatus<SortableListItem>(airingStatusFirst)
    return direction === 'reversed' ? (a, b) => -cmp(a, b) : cmp
  }
  return SORT_KEY_FACTORIES[key](direction)
}

// Runs [primary, tiebreak, byTitle] in order and returns the first non-zero
// result, with title order always appended so a full tie has one defined
// order and Array.prototype.sort's stability is never load-bearing.
export function composeComparator(
  primaryKey: SortKey,
  direction: SortDirection,
  tiebreakKey: SortKey | null,
  airingStatusFirst: AiringStatus,
): Comparator<SortableListItem> {
  const primary = sortComparator(primaryKey, direction, airingStatusFirst)
  const tiebreak = tiebreakKey ? sortComparator(tiebreakKey, 'natural', airingStatusFirst) : null
  const byTitle = SORT_KEY_FACTORIES.alphabetical('natural')
  return (a, b) => {
    const primaryResult = primary(a, b)
    if (primaryResult !== 0) return primaryResult
    if (tiebreak) {
      const tiebreakResult = tiebreak(a, b)
      if (tiebreakResult !== 0) return tiebreakResult
    }
    return byTitle(a, b)
  }
}
