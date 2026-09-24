import {
  RECAP_SEASONS,
  type RecapSeasonName,
  type SeriesListItemDto,
  type SeriesProgressBadge,
  type SeriesStatus,
  type TmdbLanguage,
  type WatchStatus,
} from '../api/types.ts'
import { type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'

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
  return status === 'Completed' || status === 'Dropped' || status === 'Rewatching'
}

export type ScoreTier = 'apex' | 'red' | 'blue' | 'gold' | 'silver' | 'bronze'

// Fixed 10 -> 1 tier mapping (design.md decision 3 of
// add-recap-score-board-and-hold-scroll) — a design decision, not a computed
// ranking, so it's stated here where TypeScript can be read rather than
// derived from `score` in CSS. Shared by ScoreBoardOverlay and
// ScoreDistribution's recap-only tier colouring so the two can never
// disagree on which colour a score carries.
export function scoreTier(score: number): ScoreTier {
  if (score === 10) return 'apex'
  if (score === 9) return 'red'
  if (score === 8) return 'blue'
  if (score === 7) return 'gold'
  if (score === 6 || score === 5) return 'silver'
  return 'bronze'
}

export const STATUS_LABELS: Record<WatchStatus, string> = {
  Watching: 'Watching',
  OnHold: 'On hold',
  PlanToWatch: 'Plan to watch',
  Completed: 'Completed',
  Dropped: 'Dropped',
  Rewatching: 'Rewatching',
}

// Modifier-class suffix per status, used to color-code list rows and filter
// tabs (watching = green, completed = blue, plan to watch = purple, on hold
// = yellow, dropped = red, rewatching = a darker blue than completed) via the
// --status-* variables in index.css.
export const STATUS_CLASS: Record<WatchStatus, string> = {
  Watching: 'watching',
  OnHold: 'onhold',
  PlanToWatch: 'plantowatch',
  Completed: 'completed',
  Dropped: 'dropped',
  Rewatching: 'rewatching',
}

// Mirrors backend/AnimeTracker.Api/Services/Entries/AiredEpisodeGate.cs
// HasAired (design.md D1 of gate-editing-on-aired-episodes): a known aired
// count settles it outright; an unknown count falls back to airing status,
// treating anything but "not yet aired" — including an unrecorded status —
// as having aired, since the app cannot prove otherwise.
export function hasAiredEpisodes(airingStatus: string | null, episodesAired: number | null): boolean {
  return episodesAired !== null ? episodesAired >= 1 : airingStatus !== 'not_yet_aired'
}

// Mirrors backend/AnimeTracker.Api/Services/Entries/UserAnimeEntryEditService.cs
// ApplyEpisodesWatched's maxEpisodes (design.md D3/D4 of
// fix-auto-date-fill-and-episode-cap): the lower of the two figures wins
// when both are known, so stored airing data reporting more episodes than
// the published total never raises the ceiling past the total — either
// figure alone stands in for the other when only one is known, and no cap
// applies when neither is.
export function episodeCeiling(episodesAired: number | null, totalEpisodes: number | null): number | null {
  return episodesAired !== null && totalEpisodes !== null ? Math.min(episodesAired, totalEpisodes) : episodesAired ?? totalEpisodes
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

// The complete universe of options a Type filter can ever offer, independent
// of what data has loaded — reserves a filter trigger's width before any
// entry has arrived (fit-my-list-filters-to-the-list design D7). `Unknown`
// is unconditional here, unlike the *offered* type lists below, which add it
// only when an untyped entry is actually present.
export const MEDIA_TYPE_FILTER_OPTIONS: FilterMultiSelectOption[] = [
  ...MEDIA_TYPE_ORDER.map((value) => ({ value, label: mediaTypeLabel(value) })),
  { value: 'unknown', label: 'Unknown' },
]

// Builds the present-types option list for a Type filter — canonical order
// (MEDIA_TYPE_ORDER then Unknown, the latter added only when a null value is
// seen). Shared by SeasonPage, YearPage and SearchPage, whose Type filters
// used to build this same loop independently.
export function mediaTypeFilterOptions(values: Iterable<string | null>): FilterMultiSelectOption[] {
  const present = new Set<string>()
  let hasUnknown = false
  for (const value of values) {
    if (value) present.add(value)
    else hasUnknown = true
  }
  const options: FilterMultiSelectOption[] = MEDIA_TYPE_ORDER.filter((value) => present.has(value)).map((value) => ({
    value,
    label: mediaTypeLabel(value),
  }))
  if (hasUnknown) options.push({ value: 'unknown', label: 'Unknown' })
  return options
}

// Mirrors backend Services/Ranking/RankBand.cs RankBandResolver.Resolve's
// HandOrdered test (add-anime-ranking design.md D2): scored, aired, not Plan
// to watch, not Dropped, not a Music/CM/PV entry. Used to gate the Rank
// action (entry editor) and the save-and-rank action (completion prompt) —
// both need "would this anime actually get a row in the ranking editor?"
// before offering an action that opens one.
const SHORT_FORM_MEDIA_TYPES = new Set(['music', 'cm', 'pv'])

export function isHandOrderable(
  myScore: number | null,
  status: WatchStatus,
  mediaType: string | null,
  hasAired: boolean,
): boolean {
  if (myScore === null || !hasAired) return false
  if (status === 'PlanToWatch' || status === 'Dropped') return false
  return !(mediaType && SHORT_FORM_MEDIA_TYPES.has(mediaType.toLowerCase()))
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

// A separate formatter from formatRuntime, not an option on it (design.md
// D11): a poster badge needs something shorter than days/hours/minutes, in
// days and decimal hours instead, so no existing caller of formatRuntime is
// affected by adding this. States any series-level watch total on the
// profile page — "Most rewatched"'s Series scope and "Most time spent"
// alike — so the two read identically where they sit one above the other.
// e.g. 3d 7h 40min -> "3d 7.7h", 23min -> "0.38h", 24min -> "0.4h" (trailing
// zero trimmed).
export function formatRewatchTime(totalSeconds: number): string {
  const totalHours = totalSeconds / 3600

  if (totalHours < 1) {
    // Two decimals rather than one below the hour mark, so a short rewatch
    // is stated rather than rounded away.
    const formatted = trimTrailingZeros(totalHours.toFixed(2))
    return totalSeconds > 0 && formatted === '0' ? '<0.01h' : `${formatted}h`
  }

  // Rounded to a tenth of an hour *before* days are split off — this is what
  // stops 47.96h from rendering "1d 24h": rounded first it's 48.0h, which
  // splits cleanly to "2d 0h". The sub-hour branch above can never collide
  // with this rounding, since it only runs when the total is under an hour.
  const roundedTenths = Math.round(totalHours * 10) / 10
  const days = Math.floor(roundedTenths / 24)
  const hours = roundedTenths - days * 24
  const hoursLabel = `${trimTrailingZeros(hours.toFixed(1))}h`
  return days > 0 ? `${days}d ${hoursLabel}` : hoursLabel
}

// A 0 total alongside hasUnknown means every main-line entry's episode count
// is unknown (e.g. the only main-line entry is still airing with no
// published total) — "0+ ep" would read as a real zero padded with a
// lower-bound marker, so it gets its own label instead. Shared by SeriesPage
// (main-series episode total) and the Series page's cards (main-line episode
// total, add-series-browser design.md D7) — the two must read identically.
export function formatEpisodeTotal(total: number, hasUnknown: boolean): string {
  if (total === 0 && hasUnknown) return 'Unknown'
  return `${total}${hasUnknown ? '+' : ''} ep`
}

// "2013 – 2023", or the single year when a franchise's whole run fell in one
// year. Shared by SeriesPage and the Series page's cards (add-series-browser
// design.md), which must show the same span for the same series.
export function formatYearSpan(firstYear: number | null, lastYear: number | null): string {
  if (firstYear === null || lastYear === null) return '—'
  return firstYear === lastYear ? String(firstYear) : `${firstYear} – ${lastYear}`
}

function trimTrailingZeros(value: string): string {
  return value.includes('.') ? value.replace(/0+$/, '').replace(/\.$/, '') : value
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

// The complete universe of options an Airing filter can ever offer — the
// same width-reservation role as MEDIA_TYPE_FILTER_OPTIONS (design D7).
export const AIRING_FILTER_OPTIONS: FilterMultiSelectOption[] = [
  { value: 'finished_airing', label: AIRING_STATUS_LABELS.finished_airing },
  { value: 'currently_airing', label: AIRING_STATUS_LABELS.currently_airing },
  { value: 'not_yet_aired', label: AIRING_STATUS_LABELS.not_yet_aired },
  { value: 'unknown', label: 'Unknown' },
]

// My list's two-level sort (D5): a primary key with an optional tiebreaker,
// each direction-aware and built so a missing value never leads the list.
export type SortKey =
  | 'alphabetical'
  | 'myScore'
  | 'malScore'
  | 'popularity'
  | 'episodesWatched'
  | 'progress'
  | 'totalEpisodes'
  | 'type'
  | 'startDate'
  | 'finishDate'

// 'natural' is each key's own natural order (descending for scores/counts/
// dates, ascending for alphabetical/type, most popular first for Popularity);
// 'reversed' flips every key. The tiebreaker always applies in 'natural'.
export type SortDirection = 'natural' | 'reversed'

export type SortableListItem = {
  title: string
  englishTitle: string | null
  mediaType: string | null
  malScore: number | null
  airingStatus: string | null
  totalEpisodes: number | null
  // anime-ranking: this item's overall rank, null when it isn't in the
  // ranking — breaks a My-score tie wherever My score is the primary or the
  // tiebreaker key (see composeComparator below).
  myRank: number | null
  popularityRank: number | null
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

const SORT_KEY_FACTORIES: Record<SortKey, (direction: SortDirection) => Comparator<SortableListItem>> = {
  // Keyed on the displayed title, not the romaji one, so the order matches
  // what the row shows — composeComparator's final tie-break below is this
  // same factory, so every sort's last fallback inherits it too.
  alphabetical: nullsLast<SortableListItem, string>(
    (item) => pickDisplayTitle(item.title, item.englishTitle),
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
  // Rank 1 is the most popular, so its natural direction sorts ascending by
  // rank — unlike the other numeric keys above, which sort descending.
  popularity: nullsLast<SortableListItem, number>(
    (item) => item.popularityRank,
    (a, b) => a - b,
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

export function sortComparator(key: SortKey, direction: SortDirection): Comparator<SortableListItem> {
  return SORT_KEY_FACTORIES[key](direction)
}

// anime-ranking: wherever My score is the primary or the tiebreaker key,
// entries left tied on it are separated by rank — best-ranked first, always
// in its own natural direction, unranked last — before the alphabetical
// fallback (library-views spec, "My list two-level sorting").
const compareByRank = nullsLast<SortableListItem, number>(
  (item) => item.myRank,
  (a, b) => a - b,
)('natural')

// Runs [primary, tiebreak, rank, byTitle] in order and returns the first
// non-zero result, with title order always appended so a full tie has one
// defined order and Array.prototype.sort's stability is never load-bearing.
export function composeComparator(
  primaryKey: SortKey,
  direction: SortDirection,
  tiebreakKey: SortKey | null,
): Comparator<SortableListItem> {
  const primary = sortComparator(primaryKey, direction)
  const tiebreak = tiebreakKey ? sortComparator(tiebreakKey, 'natural') : null
  const rankBreaksTie = primaryKey === 'myScore' || tiebreakKey === 'myScore'
  const byTitle = SORT_KEY_FACTORIES.alphabetical('natural')
  return (a, b) => {
    const primaryResult = primary(a, b)
    if (primaryResult !== 0) return primaryResult
    if (tiebreak) {
      const tiebreakResult = tiebreak(a, b)
      if (tiebreakResult !== 0) return tiebreakResult
    }
    if (rankBreaksTie) {
      const rankResult = compareByRank(a, b)
      if (rankResult !== 0) return rankResult
    }
    return byTitle(a, b)
  }
}

// The Series page's eight sort orders (add-series-browser design.md D8,
// extended by time-spent-sort-and-main-line-gate).
export type SeriesSortKey =
  | 'alphabetical'
  | 'malScore'
  | 'myScore'
  | 'status'
  | 'newest'
  | 'oldest'
  | 'myProgress'
  | 'timeSpent'

const SERIES_STATUS_ORDER: Record<SeriesStatus, number> = { Airing: 0, Ongoing: 1, Finished: 2 }

// Watched ÷ aired over the main line, not watched ÷ total — being current on
// a running series ranks alongside having finished a done one. A series with
// nothing aired at all has no ratio (design.md D8) — represented as null so
// it sorts after every series with a real ratio, including zero.
function seriesProgressRatio(item: SeriesListItemDto): number | null {
  if (item.mainLineAiredEpisodes <= 0) return null
  return item.mainLineWatchedEpisodes / item.mainLineAiredEpisodes
}

function seriesDisplayTitleKey(item: SeriesListItemDto): string {
  return pickDisplayTitle(item.title, item.englishTitle).toLowerCase()
}

// A series the key can't rank (no average, no known first-aired year) sorts
// after every series the key can rank, rather than being dropped or sorted
// as a zero (design.md D8) — nulls sort last regardless of direction.
function seriesNullsLast(
  get: (item: SeriesListItemDto) => number | null,
  direction: 'ascending' | 'descending',
): (a: SeriesListItemDto, b: SeriesListItemDto) => number {
  return (a, b) => {
    const va = get(a)
    const vb = get(b)
    if (va === null && vb === null) return 0
    if (va === null) return 1
    if (vb === null) return -1
    return direction === 'ascending' ? va - vb : vb - va
  }
}

// My average's tie-break chain (polish-search-sort-and-titles design.md D9):
// average my-score, then average ranking position (nearer the top first),
// then main-line episodes aired, before falling through to sortSeries' own
// title tie-break. Two series that both have no my-average compare equal at
// the first step and continue down the chain rather than jumping straight
// to the title.
const compareByMyScore = seriesNullsLast((item) => item.mineMain.value, 'descending')
const compareByAverageRank = seriesNullsLast((item) => item.mainLineAverageRank, 'ascending')
const compareByAiredEpisodes = seriesNullsLast((item) => item.mainLineAiredEpisodes, 'descending')
const compareMyScoreChain = (a: SeriesListItemDto, b: SeriesListItemDto): number =>
  compareByMyScore(a, b) || compareByAverageRank(a, b) || compareByAiredEpisodes(a, b)

const SERIES_SORT_COMPARATORS: Record<SeriesSortKey, (a: SeriesListItemDto, b: SeriesListItemDto) => number> = {
  alphabetical: (a, b) => seriesDisplayTitleKey(a).localeCompare(seriesDisplayTitleKey(b)),
  malScore: seriesNullsLast((item) => item.malMain.value, 'descending'),
  myScore: compareMyScoreChain,
  status: (a, b) => SERIES_STATUS_ORDER[a.status] - SERIES_STATUS_ORDER[b.status],
  // "Newest" puts the most recently started series first — descending.
  newest: seriesNullsLast((item) => item.firstYear, 'descending'),
  // "Oldest" puts the earliest started series first — ascending; unknown
  // first-aired years still sort last, not first.
  oldest: seriesNullsLast((item) => item.firstYear, 'ascending'),
  myProgress: seriesNullsLast(seriesProgressRatio, 'descending'),
  // Never null (design.md D5), so a zero-total series sorts last by value
  // alone and falls to sortSeries' title tie-break among the other zeros.
  timeSpent: seriesNullsLast((item) => item.watchedSeconds, 'descending'),
}

// Sorts the whole listed set at once (design.md D8) — every comparator's
// final tie-break is display title ascending, so the order is total and
// stable across re-sorts, matching what the card itself displays.
export function sortSeries(items: SeriesListItemDto[], sort: SeriesSortKey): SeriesListItemDto[] {
  const comparator = SERIES_SORT_COMPARATORS[sort]
  return [...items].sort((a, b) => comparator(a, b) || seriesDisplayTitleKey(a).localeCompare(seriesDisplayTitleKey(b)))
}

// The Series page's two filter groups (polish-series-badges-and-filters
// design.md D6). URL-friendly lowercase values, distinct from the wire
// SeriesProgressBadge/SeriesStatus values they map to.
export type SeriesProgressFilterValue = 'watched' | 'behind' | 'dropped' | 'unwatched'
export type SeriesStatusFilterValue = 'airing' | 'ongoing' | 'finished'

export const SERIES_PROGRESS_FILTER_OPTIONS: { value: SeriesProgressFilterValue; label: string }[] = [
  { value: 'watched', label: 'Watched' },
  { value: 'behind', label: 'Behind' },
  { value: 'dropped', label: 'Dropped' },
  { value: 'unwatched', label: 'Unwatched' },
]

export const SERIES_STATUS_FILTER_OPTIONS: { value: SeriesStatusFilterValue; label: string }[] = [
  { value: 'airing', label: 'Airing' },
  { value: 'ongoing', label: 'Ongoing' },
  { value: 'finished', label: 'Finished' },
]

// "Watched" covers both Completed and Caught up — "everything from the main
// series that has aired has been watched" is what both badges mean, just at
// different points in the franchise's run.
const PROGRESS_FILTER_BADGES: Record<SeriesProgressFilterValue, SeriesProgressBadge[]> = {
  watched: ['Completed', 'CaughtUp'],
  behind: ['Behind'],
  dropped: ['Dropped'],
  unwatched: ['Unwatched'],
}

const STATUS_FILTER_VALUES: Record<SeriesStatusFilterValue, SeriesStatus> = {
  airing: 'Airing',
  ongoing: 'Ongoing',
  finished: 'Finished',
}

// Two multi-select filters plus one toggle over the whole listed set:
// buttons within one group OR together, the groups AND together. An empty
// selection in a group applies no filter for that group. A series with no
// progress badge ('None') matches no Progress button — selecting any
// Progress filter hides it, same as any other non-matching value. multiOnly
// applies the identical rule ProfilePage's filterMultiEntry applies to
// TopSeriesItemDto.mainLineAiredCount (polish-search-sort-and-titles
// design.md D10) — the two must move together, or the Series page's toggle
// and the profile's Top series control would disagree about what
// "multi-entry" means.
export function filterSeries(
  items: SeriesListItemDto[],
  progressFilter: SeriesProgressFilterValue[],
  statusFilter: SeriesStatusFilterValue[],
  multiOnly: boolean,
): SeriesListItemDto[] {
  const allowedBadges =
    progressFilter.length > 0 ? new Set(progressFilter.flatMap((value) => PROGRESS_FILTER_BADGES[value])) : null
  const allowedStatuses = statusFilter.length > 0 ? new Set(statusFilter.map((value) => STATUS_FILTER_VALUES[value])) : null

  return items.filter((item) => {
    if (allowedBadges && !allowedBadges.has(item.progressBadge)) return false
    if (allowedStatuses && !allowedStatuses.has(item.status)) return false
    if (multiOnly && item.mainLineAiredCount <= 1) return false
    return true
  })
}

// Mirrors backend Services/Series/SeriesTitleRule.cs (design D8) — a
// mirror-only check so the series title picker can disable its confirm
// button and explain a refusal before submitting. The server is the
// authority; this must never be treated as validation on its own.
const TITLE_BOUNDARY_PUNCTUATION = /^[:;,\-–—.!?"'“”‘’\s]+|[:;,\-–—.!?"'“”‘’\s]+$/g

function collapseWhitespace(value: string): string {
  return value.split(/\s+/).filter((part) => part.length > 0).join(' ')
}

export function normalizeSeriesTitleCandidate(candidate: string): string {
  return collapseWhitespace(candidate).replace(TITLE_BOUNDARY_PUNCTUATION, '')
}

export function isSeriesTitleAcceptable(candidate: string, offeredTitles: string[]): boolean {
  const normalized = normalizeSeriesTitleCandidate(candidate)
  if (normalized.length === 0) return false

  const needle = normalized.toLowerCase()
  return offeredTitles.some((offered) => collapseWhitespace(offered).toLowerCase().includes(needle))
}

// Mirrors backend Services/Artwork/PictureIdentity.cs — MAL sometimes serves
// the exact same photo under two different URLs that differ only by file
// extension (main_picture returning the .webp rendition of a picture while
// `pictures` lists the identical photo as .jpg, or vice versa). Byte-for-byte
// URL equality treats those as two different pictures, which is what made a
// picker occasionally show "the same" picture twice.
const PICTURE_FILE_EXTENSIONS = ['.webp', '.jpg', '.jpeg', '.png', '.gif']

export function pictureIdentityKey(url: string): string {
  const lower = url.toLowerCase()
  const extension = PICTURE_FILE_EXTENSIONS.find((ext) => lower.endsWith(ext))
  return extension ? url.slice(0, url.length - extension.length) : url
}

// Dedupes a set of picture URLs by pictureIdentityKey rather than raw string
// equality. `canonical` URLs (the anime/series' own current/MAL picture) win
// their slot over a plain pictures-array entry sharing the same identity, so
// the current selection is always present in the result by exact string —
// which is how a picker's "Current" badge matches it.
export function dedupePictureOptions(pictureUrls: (string | null | undefined)[], canonical: (string | null | undefined)[]): string[] {
  const options: string[] = []
  const indexByKey = new Map<string, number>()

  function upsert(url: string | null | undefined, preferOverExisting: boolean) {
    if (!url) return
    const key = pictureIdentityKey(url)
    const existingIndex = indexByKey.get(key)
    if (existingIndex !== undefined) {
      if (preferOverExisting) options[existingIndex] = url
      return
    }
    indexByKey.set(key, options.length)
    options.push(url)
  }

  for (const url of pictureUrls) upsert(url, false)
  for (const url of canonical) upsert(url, true)

  return options
}

// Mirrors backend Services/Tmdb/TmdbImageUrl.IsTmdbImage (design D6) — the one
// test for "is this a TMDB picture". A chosen TMDB image is stored as its full
// URL (design D9), so this is how a stored choice is told from a MAL one: a
// TMDB choice must never surface under the MyAnimeList heading (design D13).
const TMDB_IMAGE_URL_PREFIX = 'https://image.tmdb.org/'

export function isTmdbImageUrl(url: string | null | undefined): boolean {
  return url != null && url.startsWith(TMDB_IMAGE_URL_PREFIX)
}

// One IMDb link per mapped id (anime-detail "External MyAnimeList link from
// id", series-page "Series external links"): a lone id is labelled "IMDb",
// several are numbered in the mapping's order. No id gives no link at all —
// unlike AniList there is no search fallback.
export function imdbLinks(ids: string[]): { href: string; label: string }[] {
  return ids.map((id, index) => ({
    href: `https://www.imdb.com/title/${encodeURIComponent(id)}/`,
    label: ids.length === 1 ? 'IMDb' : `IMDb ${index + 1}`,
  }))
}

// The picker's language group headings, in the order the backend sends them
// (none, then ja, then en).
export const TMDB_LANGUAGE_LABELS: Record<TmdbLanguage, string> = {
  none: 'No language',
  ja: 'Japanese',
  en: 'English',
}
