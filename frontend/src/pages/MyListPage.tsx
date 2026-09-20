import { useCallback, useEffect, useMemo, useRef, useState, type Dispatch, type SetStateAction } from 'react'
import { useSearchParams } from 'react-router-dom'
import { ApiError, getMyList, getRecap, updateEntry } from '../api/client.ts'
import { RECAP_SEASONS, type IncrementTarget, type MyListItemDto, type RecapDto, type RecapMode, type RecapSeasonName, type RecapTimeFilter, type UserAnimeEntryDto, type WatchStatus } from '../api/types.ts'
import type { FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { MyListControls, type ScoreFilter } from '../components/MyListControls.tsx'
import { MyListRow } from '../components/MyListRow.tsx'
import { RecapPickerOverlay } from '../components/RecapPickerOverlay.tsx'
import { RecapScopeChip } from '../components/RecapScopeChip.tsx'
import { useActionFailure } from '../context/ActionFailureContext.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useEpisodeIncrement, useSetEpisodesWatched } from '../context/CompletionPromptContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import {
  AIRING_STATUS_LABELS,
  composeComparator,
  MEDIA_TYPE_ORDER,
  mediaTypeLabel,
  pickDisplayTitle,
  seasonLabel,
  STATUS_CLASS,
  STATUS_LABELS,
} from '../utils/anime.ts'
import type { AiringStatus, SortDirection, SortKey } from '../utils/anime.ts'
import './MyListPage.css'

// Multi-select (polish-rewatch-more-and-filters design.md D6): any
// combination of statuses can be selected at once; `[]` means All rather
// than being a seventh member of the type, so there is no representable
// contradiction (e.g. `['All', 'Watching']`) to guard against.
type StatusFilter = WatchStatus[]

// Rewatching sits directly after Currently watching — both are runs in
// progress (library-views spec, "My list grouped and ordered by status").
const GROUP_ORDER: WatchStatus[] = ['Watching', 'Rewatching', 'OnHold', 'PlanToWatch', 'Completed', 'Dropped']

// How many rows the page reveals at a time, matching SeriesBrowserPage,
// SeasonPage and YearPage; SearchPage's 48 is for a card grid where one row
// holds several items, and this is a row layout (design.md D1).
const PAGE_SIZE = 24

// The All tab is rendered separately (it isn't a WatchStatus) — see the
// tabs bar below.
const STATUS_TABS: { value: WatchStatus; label: string }[] = [
  { value: 'Watching', label: 'Watching' },
  { value: 'Rewatching', label: 'Rewatching' },
  { value: 'Completed', label: 'Completed' },
  { value: 'PlanToWatch', label: 'Plan to watch' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Dropped', label: 'Dropped' },
]

// The recap handoff's `focus` token (design.md decision 1/3), translated
// into this page's own control values — a *seed* for useRestorableState's
// `initial`, read once per fresh visit rather than an arrival effect (design.md
// decision 2). An unknown or absent token maps to "no narrowing": every
// control stays at its ordinary default. `status` is a one-element array (or
// `[]`) — every deep link still lands on exactly the single status it names
// today (polish-rewatch-more-and-filters design.md D6, tasks.md 5.2).
type FocusSeed = {
  status: StatusFilter
  typeFilter: string[] | null
  scoreFilter: ScoreFilter
}

function parseFocus(token: string | null): FocusSeed {
  const none: FocusSeed = { status: [], typeFilter: null, scoreFilter: 'any' }
  switch (token) {
    case 'completed':
      return { ...none, status: ['Completed'] }
    case 'dropped':
      return { ...none, status: ['Dropped'] }
    case 'watching':
      return { ...none, status: ['Watching'] }
    case 'movies':
      // The stat this mirrors ("Movies watched") counts a movie regardless
      // of status, which the removed Started filter used to narrow to. The
      // status tabs don't have an equivalent (Dropped can still have zero
      // episodes watched), so this deep link is now type-only.
      return { ...none, typeFilter: ['movie'] }
    default: {
      const scoreMatch = token?.match(/^score-([1-9]|10)$/)
      return scoreMatch ? { ...none, scoreFilter: scoreMatch[1] as ScoreFilter } : none
    }
  }
}

// Patches one entry into the list by animeId, leaving every other item's
// reference untouched — the memoised row relies on that to skip re-rendering.
function patchItem(
  setItems: Dispatch<SetStateAction<MyListItemDto[] | null>>,
  animeId: number,
  saved: UserAnimeEntryDto,
) {
  setItems((prev) => prev && prev.map((item) => (item.animeId === animeId ? { ...item, entry: saved } : item)))
}

function buildIncrementTarget(
  item: MyListItemDto,
  setItems: Dispatch<SetStateAction<MyListItemDto[] | null>>,
): IncrementTarget {
  return {
    animeId: item.animeId,
    animeTitle: pickDisplayTitle(item.title, item.englishTitle),
    pictureUrl: item.pictureUrl,
    episodesWatched: item.entry.episodesWatched,
    previousStatus: item.entry.status,
    currentScore: item.entry.myScore,
    mediaType: item.mediaType,
    onSaved: (saved) => patchItem(setItems, item.animeId, saved),
    // Patches rather than reloading (design D11): grouping and sorting are
    // client-side from the patched entry, so the completed row moves to
    // Completed from the patch alone. myRank stays as loaded until the next
    // read, the same as the row's score control and the entry editor.
    onCompleted: (saved) => patchItem(setItems, item.animeId, saved),
  }
}

// The flat (ungrouped) view's own section title (task 5.6): All when nothing
// is selected, the single status's label for one selection, and every
// selected label joined for several — read in selection order like the tabs
// themselves, since this heading isn't reproducing the app's group order the
// way GROUP_ORDER-driven grouping does.
function statusFiltersLabel(statusFilters: StatusFilter): string {
  return statusFilters.length === 0 ? 'All' : statusFilters.map((status) => STATUS_LABELS[status]).join(', ')
}

// The three filter predicates, factored out so the option pass below (which
// tests each candidate against the *other* two) and the match pass apply
// exactly the same rule rather than two copies of it (design D8, tasks.md
// 3.1).
function passesType(item: MyListItemDto, typeFilter: string[] | null): boolean {
  return typeFilter === null || typeFilter.includes(item.mediaType ?? 'unknown')
}

function passesAiring(item: MyListItemDto, airingFilter: string[] | null): boolean {
  return airingFilter === null || airingFilter.includes(item.airingStatus ?? 'unknown')
}

function passesScore(item: MyListItemDto, scoreFilter: ScoreFilter): boolean {
  if (scoreFilter === 'any') return true
  if (scoreFilter === 'rated') return item.entry.myScore != null
  if (scoreFilter === 'unrated') return item.entry.myScore == null
  return item.entry.myScore === Number(scoreFilter)
}

// Builds a Type/Airing option list from the values a base actually holds,
// unioned with the control's own current selection (design D4, tasks.md
// 3.4) — so a selection stays offered, and untickable, even once the rest of
// the page has narrowed away from it. `order` never includes 'unknown'; it's
// appended once, whenever the base holds an untyped/unstatused entry or the
// selection itself names it.
function narrowedFilterOptions(
  order: readonly string[],
  present: Set<string>,
  hasUnknown: boolean,
  selection: string[] | null,
  labelFor: (value: string) => string,
): FilterMultiSelectOption[] {
  const forced = new Set(selection ?? [])
  const values = order.filter((value) => present.has(value) || forced.has(value))
  if (hasUnknown || forced.has('unknown')) values.push('unknown')
  return values.map((value) => ({ value, label: labelFor(value) }))
}

type Derivation =
  | { mode: 'grouped'; total: number; shown: number; groups: { status: WatchStatus; items: MyListItemDto[] }[] }
  | { mode: 'flat'; total: number; shown: number; items: MyListItemDto[] }

// My list page: five bands stacked top to bottom — a header (title only),
// the status tabs (with Recap a period and, while off-default, Reset
// filters & sort together at the far end), the controls block (a Filter
// group and a Sort group, rendered by MyListControls), a results line (the
// recap scope chip, only while scoped), and the list itself, grouped by
// status (Watching -> Rewatching -> On hold -> Plan to watch -> Completed ->
// Dropped) by default, each section's own count next to its title alongside
// the ALL total (see derived.shown in renderBody). Filtering and the
// two-level sort (primary + tiebreaker) compose over the whole page instead
// of per status group.
export function MyListPage() {
  const { data, loading, setData: setItems } = usePageData<MyListItemDto[]>('my-list', getMyList)
  const items = data ?? []

  // Recap scope and focus (design.md decision 1/2/9/10, tasks.md 5.1-5.2,
  // 7.3-7.5): the only use of useSearchParams on this page. A scope and an
  // optional focus token both arrive entirely from the URL (a recap's "see
  // all"/stat/distribution-row link), never from a control on this page —
  // the focus is parsed once here and only feeds the controls' `initial`
  // below, so it seeds a fresh visit without an effect and is never re-read.
  const [searchParams, setSearchParams] = useSearchParams()
  const [pickerOpen, setPickerOpen] = useState(false)
  const focusSeed = parseFocus(searchParams.get('focus'))

  // Renamed from `statusFilter` (design.md D6): the stored value's type
  // changed from a string to an array, and a snapshot written before this
  // change could otherwise feed a bare string into array code on a
  // back-navigation within a live session. A new key makes that unreachable.
  const [statusFilters, setStatusFilters] = useRestorableState<StatusFilter>('statusFilters', focusSeed.status)
  const [query, setQuery] = useRestorableState('query', '')
  const [typeFilter, setTypeFilter] = useRestorableState<string[] | null>('typeFilter', focusSeed.typeFilter)
  const [airingFilter, setAiringFilter] = useRestorableState<string[] | null>('airingFilter', null)
  const [scoreFilter, setScoreFilter] = useRestorableState<ScoreFilter>('scoreFilter', focusSeed.scoreFilter)
  const [sort, setSort] = useRestorableState<SortKey>('sort', 'alphabetical')
  const [sortDirection, setSortDirection] = useRestorableState<SortDirection>('sortDirection', 'natural')
  const [sortThen, setSortThen] = useRestorableState<SortKey | null>('sortThen', null)
  const [groupByStatus, setGroupByStatus] = useRestorableState('groupByStatus', true)

  // One reveal budget for the whole page, not one per status group (design.md
  // D1/D2), restorable like every other control here so a back-navigation
  // brings back the same amount of list.
  const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', PAGE_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)

  // Every input `derived` keys off except `scopedItems`: a change to *which*
  // entries are shown or *what order* they're in starts the reveal over, while
  // the list's contents changing under an unchanged view (an edit, a settle)
  // must not — that would yank a scrolled-down user back to the top. A string
  // rather than a reference comparison because three of the ten are arrays
  // whose identity changes on every set even when the contents match.
  //
  // Adjusted during render, not in a useEffect — the idiom usePageData.ts and
  // useRestorableState.ts already use. An effect would paint the old count's
  // worth of rows against the new filter first and only then reset, which is
  // the full-size render this exists to remove (design.md D3).
  const viewIdentity = JSON.stringify([
    query,
    statusFilters,
    typeFilter,
    airingFilter,
    scoreFilter,
    sort,
    sortDirection,
    sortThen,
    groupByStatus,
  ])
  const [renderedViewIdentity, setRenderedViewIdentity] = useState(viewIdentity)
  if (viewIdentity !== renderedViewIdentity) {
    setRenderedViewIdentity(viewIdentity)
    setVisibleCount(PAGE_SIZE)
  }

  const recapModeParam = searchParams.get('recapMode')
  const hasRecapScope = recapModeParam === 'multiYear' || recapModeParam === 'yearly' || recapModeParam === 'season'
  const recapMode = (hasRecapScope ? recapModeParam : 'yearly') as RecapMode
  const recapFromParam = Number(searchParams.get('recapFrom'))
  const recapToParam = Number(searchParams.get('recapTo'))
  const recapYearParam = Number(searchParams.get('recapYear'))
  const recapSeasonParam = searchParams.get('recapSeason')
  const recapSeason: RecapSeasonName = (RECAP_SEASONS as readonly string[]).includes(recapSeasonParam ?? '')
    ? (recapSeasonParam as RecapSeasonName)
    : RECAP_SEASONS[0]
  const recapFilter: RecapTimeFilter = searchParams.get('recapFilter') === 'aired' ? 'aired' : 'watched'
  const recapType = searchParams.get('recapType') ?? 'all'

  const nowYear = new Date().getFullYear()
  const recapStartYear =
    recapMode === 'multiYear'
      ? Number.isInteger(recapFromParam) && recapFromParam > 0
        ? recapFromParam
        : nowYear
      : Number.isInteger(recapYearParam) && recapYearParam > 0
        ? recapYearParam
        : nowYear
  const recapEndYear =
    recapMode === 'multiYear' ? (Number.isInteger(recapToParam) && recapToParam > 0 ? recapToParam : recapStartYear) : recapStartYear

  const recapScopeKey = hasRecapScope
    ? recapMode === 'multiYear'
      ? `recap:multiYear:${recapStartYear}-${recapEndYear}:${recapFilter}`
      : recapMode === 'yearly'
        ? `recap:yearly:${recapStartYear}:${recapFilter}`
        : `recap:season:${recapStartYear}:${recapSeason}`
    : 'no-recap-scope'

  const { data: recapScopeData, loading: recapScopeLoading } = usePageData<RecapDto | null>(recapScopeKey, () =>
    hasRecapScope
      ? getRecap({ mode: recapMode, startYear: recapStartYear, endYear: recapEndYear, season: recapSeason, filter: recapFilter })
      : Promise.resolve(null),
  )

  // The scope's own inclusion rule lives once, on the server (D9) — this
  // page only intersects ids and, same as the recap page, narrows by media
  // type locally.
  const scopedAnimeIds = useMemo(() => {
    if (!hasRecapScope || !recapScopeData) return null
    const narrowed =
      recapType === 'all'
        ? recapScopeData.items
        : recapScopeData.items.filter((item) => (item.mediaType ?? 'unknown') === recapType)
    return new Set(narrowed.map((item) => item.animeId))
  }, [hasRecapScope, recapScopeData, recapType])

  // Translates the picker's own mode/from/to/year/season/filter vocabulary
  // into the `recap`-prefixed scope vocabulary this page reads (design.md
  // decision 2) — the overlay's onConfirm contract stays a plain query
  // string; only this caller decides it means "apply as a scope" rather
  // than "navigate".
  function applyPickerScope(search: string) {
    const picked = new URLSearchParams(search)
    const mode = picked.get('mode')
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.delete('recapFrom')
      params.delete('recapTo')
      params.delete('recapYear')
      params.delete('recapSeason')
      params.delete('recapFilter')
      params.delete('recapType')

      if (mode) params.set('recapMode', mode)
      if (mode === 'multiYear') {
        const from = picked.get('from')
        const to = picked.get('to')
        if (from) params.set('recapFrom', from)
        if (to) params.set('recapTo', to)
        const filter = picked.get('filter')
        if (filter) params.set('recapFilter', filter)
      } else if (mode === 'yearly') {
        const year = picked.get('year')
        if (year) params.set('recapYear', year)
        const filter = picked.get('filter')
        if (filter) params.set('recapFilter', filter)
      } else if (mode === 'season') {
        const year = picked.get('year')
        const season = picked.get('season')
        if (year) params.set('recapYear', year)
        if (season) params.set('recapSeason', season)
      }
      return params
    })
  }

  function dismissRecapScope() {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      for (const key of [
        'recapMode',
        'recapFrom',
        'recapTo',
        'recapYear',
        'recapSeason',
        'recapFilter',
        'recapType',
        'focus',
      ]) {
        params.delete(key)
      }
      return params
    })
  }

  // The inverse of RecapPage's myListScopeSearch (design.md decision 3):
  // builds the recap page's own param vocabulary (mode/from/to/year/season/
  // filter/type) from this page's `recap`-prefixed scope, so the scope
  // chip's link lands back on the same period, filter, and media type.
  function recapSearchFromScope(): string {
    const params = new URLSearchParams()
    params.set('mode', recapMode)
    if (recapMode === 'multiYear') {
      params.set('from', String(recapStartYear))
      params.set('to', String(recapEndYear))
    } else {
      params.set('year', String(recapStartYear))
    }
    if (recapMode === 'season') params.set('season', recapSeason)
    else params.set('filter', recapFilter)
    if (recapType !== 'all') params.set('type', recapType)
    return params.toString()
  }

  function recapScopeLabel(): string {
    const periodLabel =
      recapMode === 'multiYear'
        ? recapStartYear === recapEndYear
          ? String(recapStartYear)
          : `${recapStartYear}–${recapEndYear}`
        : recapMode === 'yearly'
          ? String(recapStartYear)
          : `${seasonLabel(recapSeason)} ${recapStartYear}`
    const filterLabel = recapMode === 'season' ? 'What aired' : recapFilter === 'aired' ? 'What aired' : 'What I watched'
    // "All types" is the common case and adds nothing the period/filter
    // don't already say — only a real narrowing (a specific media type) is
    // worth the chip's limited space.
    return recapType === 'all' ? `${periodLabel} · ${filterLabel}` : `${periodLabel} · ${filterLabel} · ${mediaTypeLabel(recapType)}`
  }

  const [pendingIncrementId, setPendingIncrementId] = useState<number | null>(null)
  const [pendingScoreId, setPendingScoreId] = useState<number | null>(null)
  // Concurrency guards read synchronously inside the handlers below — a ref
  // rather than the state above, so the handlers' identity doesn't have to
  // change on every click (D2). The state stays, feeding only the row props.
  const pendingIncrementRef = useRef<number | null>(null)
  const pendingScoreRef = useRef<number | null>(null)

  const { openEditor } = useEntryEditor()
  const increment = useEpisodeIncrement()
  const setEpisodesWatched = useSetEpisodesWatched()
  const reportFailure = useActionFailure()

  const openEdit = useCallback(
    (item: MyListItemDto) => {
      openEditor({
        animeId: item.animeId,
        animeTitle: pickDisplayTitle(item.title, item.englishTitle),
        totalEpisodes: item.totalEpisodes,
        airingStatus: item.airingStatus,
        episodesAired: item.episodesAired,
        mediaType: item.mediaType,
        entry: item.entry,
        onSaved: (saved) => patchItem(setItems, item.animeId, saved),
        onDeleted: () => setItems((prev) => prev && prev.filter((i) => i.animeId !== item.animeId)),
      })
    },
    [openEditor, setItems],
  )

  const incrementEpisodes = useCallback(
    async (item: MyListItemDto) => {
      if (pendingIncrementRef.current !== null) return
      pendingIncrementRef.current = item.animeId
      setPendingIncrementId(item.animeId)
      try {
        await increment(buildIncrementTarget(item, setItems))
      } finally {
        pendingIncrementRef.current = null
        setPendingIncrementId(null)
      }
    },
    [increment, setItems],
  )

  const setEpisodesWatchedForItem = useCallback(
    async (item: MyListItemDto, value: number) => {
      if (pendingIncrementRef.current !== null) return
      pendingIncrementRef.current = item.animeId
      setPendingIncrementId(item.animeId)
      try {
        await setEpisodesWatched(buildIncrementTarget(item, setItems), value)
      } finally {
        pendingIncrementRef.current = null
        setPendingIncrementId(null)
      }
    },
    [setEpisodesWatched, setItems],
  )

  const changeScore = useCallback(
    async (item: MyListItemDto, score: number) => {
      if (pendingScoreRef.current !== null) return
      pendingScoreRef.current = item.animeId
      setPendingScoreId(item.animeId)
      try {
        const saved = await updateEntry(item.animeId, { myScore: score })
        patchItem(setItems, item.animeId, saved)
      } catch (err) {
        // The select snaps back to the saved score through its controlled
        // value, and the notice accounts for it (action-failure-notices).
        reportFailure({
          title: `Couldn't update the score for ${pickDisplayTitle(item.title, item.englishTitle)}`,
          reason: err instanceof ApiError ? err.reason : null,
        })
      } finally {
        pendingScoreRef.current = null
        setPendingScoreId(null)
      }
    },
    [setItems, reportFailure],
  )

  // A recap scope narrows the candidate rows *before* every other control
  // below runs (D10) — an inactive scope is a no-op pass-through, so
  // nothing here changes when there's nothing to narrow. While a scope is
  // active but its own fetch hasn't resolved yet, the candidate set is
  // empty rather than the full list, so nothing flashes unscoped first.
  const scopedItems = useMemo(() => {
    if (!hasRecapScope) return items
    if (!scopedAnimeIds) return []
    return items.filter((item) => scopedAnimeIds.has(item.animeId))
  }, [items, hasRecapScope, scopedAnimeIds])

  // Recap scope -> status tabs. Kept addressable on its own (rather than
  // folded into `candidates` below) because `derived.total` must stay its
  // length — that's what separates "Nothing here yet" from "Nothing matches
  // these filters" (design D8, tasks.md 3.2).
  const statusScoped = useMemo(
    () => (statusFilters.length === 0 ? scopedItems : scopedItems.filter((item) => statusFilters.includes(item.entry.status))),
    [scopedItems, statusFilters],
  )

  // The find-in-list text counts as one of the "other controls" the three
  // filters read (design D3) — narrowed here, once, ahead of the option/match
  // pass below.
  const candidates = useMemo(() => {
    const needle = query.trim().toLowerCase()
    if (!needle) return statusScoped
    return statusScoped.filter((item) => {
      const titleMatch = item.title.toLowerCase().includes(needle)
      const englishMatch = item.englishTitle ? item.englishTitle.toLowerCase().includes(needle) : false
      return titleMatch || englishMatch
    })
  }, [statusScoped, query])

  // One pass over `candidates` produces the three option lists and the
  // matched set together (design D8): each entry is tested once against each
  // predicate, and it counts toward a control's options exactly when it
  // passes the *other* two — never its own, so a filter never narrows its
  // own options (design D1) — and toward `matched` when it passes all three.
  const { matched, typeOptions, airingOptions, scoreOptions, scoreDisabledLabel } = useMemo(() => {
    const presentTypes = new Set<string>()
    let hasUnknownType = false
    const presentAiring = new Set<string>()
    let hasUnknownAiring = false
    let scoreBaseTotal = 0
    let scoreBaseRated = 0
    const scoreCounts = new Map<number, number>()
    const matched: MyListItemDto[] = []

    for (const item of candidates) {
      const t = passesType(item, typeFilter)
      const a = passesAiring(item, airingFilter)
      const s = passesScore(item, scoreFilter)

      if (a && s) {
        if (item.mediaType) presentTypes.add(item.mediaType)
        else hasUnknownType = true
      }
      if (t && s) {
        if (item.airingStatus) presentAiring.add(item.airingStatus)
        else hasUnknownAiring = true
      }
      if (t && a) {
        scoreBaseTotal++
        if (item.entry.myScore != null) {
          scoreBaseRated++
          scoreCounts.set(item.entry.myScore, (scoreCounts.get(item.entry.myScore) ?? 0) + 1)
        }
      }
      if (t && a && s) matched.push(item)
    }

    const typeOptions = narrowedFilterOptions(MEDIA_TYPE_ORDER, presentTypes, hasUnknownType, typeFilter, mediaTypeLabel)
    const airingOptions = narrowedFilterOptions(
      Object.keys(AIRING_STATUS_LABELS),
      presentAiring,
      hasUnknownAiring,
      airingFilter,
      (value) => (value === 'unknown' ? 'Unknown' : AIRING_STATUS_LABELS[value as AiringStatus]),
    )

    // A score value is offered iff present and not universal over the base;
    // Rated/Unrated are offered iff the base is mixed (design D2). The base
    // excludes the score filter's own restriction, same as the two controls
    // above (design D1).
    const scoreBaseUnrated = scoreBaseTotal - scoreBaseRated
    const ratedUnratedOffered = scoreBaseRated > 0 && scoreBaseUnrated > 0
    const offeredValues = Array.from({ length: 10 }, (_, i) => 10 - i).filter((value) => {
      const count = scoreCounts.get(value) ?? 0
      return count > 0 && count < scoreBaseTotal
    })

    // Presentation order: Any, Rated, 10..1, Unrated. `scoreFilter`'s own
    // current value is forced in regardless of the rule above (design D4).
    const scoreOptions: ScoreFilter[] = ['any']
    if (ratedUnratedOffered || scoreFilter === 'rated') scoreOptions.push('rated')
    for (let value = 10; value >= 1; value--) {
      if (offeredValues.includes(value) || scoreFilter === String(value)) scoreOptions.push(String(value) as ScoreFilter)
    }
    if (ratedUnratedOffered || scoreFilter === 'unrated') scoreOptions.push('unrated')

    // Disabled exactly when nothing besides Any would ever be offered and the
    // score filter isn't itself narrowing the list (design D5). By D2's own
    // proof there are exactly three ways to reach this — a mixed base or two
    // distinct scores always leave something else offerable (tasks.md 3.6).
    let scoreDisabledLabel: string | null = null
    if (scoreFilter === 'any' && !ratedUnratedOffered && offeredValues.length === 0) {
      if (scoreBaseTotal === 0) scoreDisabledLabel = 'Any'
      else if (scoreBaseRated === 0) scoreDisabledLabel = 'Unrated'
      else scoreDisabledLabel = String([...scoreCounts.keys()][0])
    }

    return { matched, typeOptions, airingOptions, scoreOptions, scoreDisabledLabel }
  }, [candidates, typeFilter, airingFilter, scoreFilter])

  // Sorts and groups `matched` (design D8) — what matches, and the `shown`/
  // `total` counts, are unchanged from before this change; only where the
  // option lists come from moved.
  const derived = useMemo<Derivation>(() => {
    const comparator = composeComparator(sort, sortDirection, sortThen)

    if (groupByStatus) {
      // The app's standard group order, not the order statuses were clicked
      // in (design.md D6).
      const groups = GROUP_ORDER.filter((status) => statusFilters.length === 0 || statusFilters.includes(status))
        .map((status) => ({
          status,
          items: matched.filter((item) => item.entry.status === status).sort(comparator),
        }))
        .filter((group) => group.items.length > 0)
      return { mode: 'grouped', total: statusScoped.length, shown: matched.length, groups }
    }

    return { mode: 'flat', total: statusScoped.length, shown: matched.length, items: [...matched].sort(comparator) }
  }, [matched, statusScoped, statusFilters, sort, sortDirection, sortThen, groupByStatus])

  // Reveal the next page of an array that is already filtered, sorted and in
  // memory once the sentinel comes into view — no network call and no control
  // to press (SeriesBrowserPage.tsx's observer).
  useEffect(() => {
    const node = sentinelRef.current
    if (!node) return

    const observer = new IntersectionObserver((entries) => {
      if (entries[0]?.isIntersecting) {
        setVisibleCount((prev) => Math.min(prev + PAGE_SIZE, derived.shown))
      }
    })
    observer.observe(node)
    return () => observer.disconnect()
  }, [derived, setVisibleCount])

  // The page's one budget, spent group by group (design.md D1/D2): each group
  // draws what the remainder allows and passes the rest on, so the cap is on
  // rows actually mounted rather than on rows per group. Every group stays in
  // the output even when it draws none of its rows — its header still states
  // its own real count. `shown` is clamped rather than left to `slice` so
  // `remaining` can't go negative and make the next group's `Math.min` negative.
  const visibleGroups = useMemo(() => {
    if (derived.mode !== 'grouped') return []
    let remaining = visibleCount
    return derived.groups.map((group) => {
      const shown = Math.min(group.items.length, remaining)
      remaining -= shown
      return { ...group, visibleItems: group.items.slice(0, shown) }
    })
  }, [derived, visibleCount])

  const isOffDefault =
    query !== '' ||
    typeFilter !== null ||
    airingFilter !== null ||
    scoreFilter !== 'any' ||
    sort !== 'alphabetical' ||
    sortDirection !== 'natural' ||
    sortThen !== null ||
    !groupByStatus

  // A status tab toggles its own membership; deselecting the last selected
  // status naturally yields `[]` — i.e. it lands on All rather than an
  // empty list, since filtering the last member out of a one-element array
  // is `[]` with no special-casing needed (design.md D6).
  function toggleStatusFilter(status: WatchStatus) {
    setStatusFilters((prev) => (prev.includes(status) ? prev.filter((s) => s !== status) : [...prev, status]))
  }

  function clearFilters() {
    setQuery('')
    setTypeFilter(null)
    setAiringFilter(null)
    setScoreFilter('any')
    setSort('alphabetical')
    setSortDirection('natural')
    setSortThen(null)
    setGroupByStatus(true)
  }

  function handleSortChange(key: SortKey) {
    setSort(key)
    // A key can't tiebreak itself — drop it rather than leave a stale
    // selection the "then by" control no longer offers.
    setSortThen((prev) => (prev === key ? null : prev))
  }

  function handleThenChange(key: SortKey | null) {
    setSortThen(key)
  }

  // Rank numbers follow the grouping toggle, not the sort key: shown only
  // when the list is flat and not alphabetical (D9).
  const showRanks = !groupByStatus && sort !== 'alphabetical'
  // The airing badge shows on every row while the airing filter is doing
  // something — Plan-to-watch rows always show it regardless (D11), handled
  // per-row below.
  const airingBadgeActive = airingFilter !== null

  function renderRow(item: MyListItemDto, rank?: number) {
    return (
      <MyListRow
        key={item.animeId}
        item={item}
        rank={rank}
        showAiringBadge={item.entry.status === 'PlanToWatch' || airingBadgeActive}
        incrementPending={pendingIncrementId === item.animeId}
        scorePending={pendingScoreId === item.animeId}
        onEdit={openEdit}
        onIncrement={incrementEpisodes}
        onSetWatched={setEpisodesWatchedForItem}
        onScoreChange={changeScore}
      />
    )
  }

  function renderBody() {
    if (loading || (hasRecapScope && recapScopeLoading)) return <p className="my-list-page__loading">Loading…</p>

    if (derived.total === 0) {
      return (
        <div className="my-list-page__empty-block">
          <p className="my-list-page__empty-primary">Nothing here yet</p>
          <p className="my-list-page__empty-secondary">
            {statusFiltersLabel(statusFilters)}
            {hasRecapScope ? ' in this recap period' : ''}
          </p>
        </div>
      )
    }

    if (derived.shown === 0) {
      return (
        <div className="my-list-page__empty-block">
          <p className="my-list-page__empty-primary">Nothing matches these filters</p>
          <p className="my-list-page__empty-secondary">Try removing a filter, or reset them all.</p>
          <button type="button" className="my-list-page__clear-filters my-list-page__clear-filters--reset" onClick={clearFilters}>
            Reset filters &amp; sort
          </button>
        </div>
      )
    }

    if (derived.mode === 'grouped') {
      return (
        <>
          {visibleGroups.map((group) => (
            <section key={group.status} className="my-list-page__group">
              {/* role="status" (not on the h2 itself, which would drop its
                  heading semantics): announces each status's count changing
                  as filters narrow it, same as the flat title below. The ALL
                  total repeats on every section rather than living once
                  above the list, so it reads next to whichever status
                  you're looking at instead of a separate line. */}
              <div className="my-list-page__group-header" role="status">
                <h2>
                  {STATUS_LABELS[group.status]}{' '}
                  <span className="my-list-page__group-count">({group.items.length})</span>{' '}
                  <span className="my-list-page__group-count">(ALL: {derived.shown})</span>
                </h2>
              </div>
              <ul className="my-list-page__list">{group.visibleItems.map((item) => renderRow(item))}</ul>
            </section>
          ))}
          {/* One sentinel for the page, after the last row of the last group
              rather than inside any one of them — the budget above is shared,
              so there is only ever one place more rows come from. */}
          <div ref={sentinelRef} className="my-list-page__sentinel" />
        </>
      )
    }

    return (
      <>
        <section className="my-list-page__group">
          <div className="my-list-page__group-header" role="status">
            <h2>
              {statusFiltersLabel(statusFilters)} <span className="my-list-page__group-count">({derived.shown})</span>
            </h2>
          </div>
          {/* Sliced from 0, so showRanks' index + 1 still numbers from 1. */}
          <ul className="my-list-page__list">
            {derived.items.slice(0, visibleCount).map((item, index) => renderRow(item, showRanks ? index + 1 : undefined))}
          </ul>
        </section>
        <div ref={sentinelRef} className="my-list-page__sentinel" />
      </>
    )
  }

  const resultsVisible = !loading
  const hasResultsLine = resultsVisible && hasRecapScope

  return (
    <div className="my-list-page">
      <div className="my-list-page__header">
        <h1>My list</h1>
      </div>

      {/* Plain toggle buttons, not role="tab"/aria-selected (design.md D6):
          a tablist models exactly one selected tab, and several of these can
          now be selected at once — keeping tablist markup while allowing
          that would misreport the control to assistive technology. */}
      <div className="my-list-page__tabs" aria-label="Filter by status (multi-select)">
        <button
          type="button"
          aria-pressed={statusFilters.length === 0}
          className={`my-list-page__tab${statusFilters.length === 0 ? ' my-list-page__tab--active' : ''}`}
          onClick={() => setStatusFilters([])}
        >
          All
        </button>
        {STATUS_TABS.map((tab) => {
          const active = statusFilters.includes(tab.value)
          const classes = ['my-list-page__tab', `my-list-page__tab--${STATUS_CLASS[tab.value]}`]
          if (active) classes.push('my-list-page__tab--active')
          return (
            <button
              key={tab.value}
              type="button"
              aria-pressed={active}
              className={classes.join(' ')}
              onClick={() => toggleStatusFilter(tab.value)}
            >
              {tab.label}
            </button>
          )
        })}
        {/* Both page-action buttons together, pushed to the far end as one
            unit — a single margin-left: auto here rather than on each
            button, so Reset appearing/disappearing can't shift Recap. */}
        <div className="my-list-page__tabs-actions">
          <button type="button" className="my-list-page__clear-filters" onClick={() => setPickerOpen(true)}>
            Recap a period
          </button>
          {isOffDefault && (
            <button type="button" className="my-list-page__clear-filters my-list-page__clear-filters--reset" onClick={clearFilters}>
              Reset filters &amp; sort
            </button>
          )}
        </div>
      </div>

      <MyListControls
        filters={{
          query,
          onQueryChange: setQuery,
          typeFilter,
          onTypeFilterChange: setTypeFilter,
          airingFilter,
          onAiringFilterChange: setAiringFilter,
          scoreFilter,
          onScoreFilterChange: setScoreFilter,
        }}
        sort={{
          sort,
          sortDirection,
          sortThen,
          groupByStatus,
          onSortChange: handleSortChange,
          onThenChange: handleThenChange,
          onDirectionToggle: () => setSortDirection((prev) => (prev === 'natural' ? 'reversed' : 'natural')),
          onGroupByStatusChange: setGroupByStatus,
        }}
        typeOptions={typeOptions}
        airingOptions={airingOptions}
        scoreOptions={scoreOptions}
        scoreDisabledLabel={scoreDisabledLabel}
      />

      {hasResultsLine && (
        <div className="my-list-page__results">
          {hasRecapScope && (
            <RecapScopeChip
              label={recapScopeLabel()}
              href={`/recap?${recapSearchFromScope()}`}
              onDismiss={dismissRecapScope}
            />
          )}
        </div>
      )}

      {renderBody()}

      {pickerOpen && (
        <RecapPickerOverlay
          title="Recap a period"
          confirmLabel="Apply to list"
          onClose={() => setPickerOpen(false)}
          onConfirm={(search) => {
            setPickerOpen(false)
            applyPickerScope(search)
          }}
        />
      )}
    </div>
  )
}
