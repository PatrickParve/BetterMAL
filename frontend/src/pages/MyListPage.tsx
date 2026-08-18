import { useCallback, useMemo, useRef, useState, type Dispatch, type SetStateAction } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getMyList, getRecap, updateEntry } from '../api/client.ts'
import { RECAP_SEASONS, type IncrementTarget, type MyListItemDto, type RecapDto, type RecapMode, type RecapSeasonName, type RecapTimeFilter, type UserAnimeEntryDto, type WatchStatus } from '../api/types.ts'
import { FilterMultiSelect, type FilterMultiSelectOption } from '../components/FilterMultiSelect.tsx'
import { MyListRow } from '../components/MyListRow.tsx'
import { RecapPickerOverlay } from '../components/RecapPickerOverlay.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useEpisodeIncrement, useSetEpisodesWatched } from '../context/CompletionPromptContext.tsx'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
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

type StatusFilter = 'All' | WatchStatus
type ScoreFilter = 'any' | 'rated' | 'unrated'

const GROUP_ORDER: WatchStatus[] = ['Watching', 'OnHold', 'PlanToWatch', 'Completed', 'Dropped']

const FILTER_TABS: { value: StatusFilter; label: string }[] = [
  { value: 'All', label: 'All' },
  { value: 'Watching', label: 'Watching' },
  { value: 'Completed', label: 'Completed' },
  { value: 'PlanToWatch', label: 'Plan to watch' },
  { value: 'OnHold', label: 'On hold' },
  { value: 'Dropped', label: 'Dropped' },
]

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'episodesWatched', label: 'Episodes watched' },
  { value: 'progress', label: 'Progress' },
  { value: 'totalEpisodes', label: 'Total episodes' },
  { value: 'airingStatus', label: 'Airing status' },
  { value: 'type', label: 'Type' },
  { value: 'startDate', label: 'Start date' },
  { value: 'finishDate', label: 'Finish date' },
]

const AIRING_STATUS_FIRST_OPTIONS: { value: AiringStatus; label: string }[] = [
  { value: 'currently_airing', label: AIRING_STATUS_LABELS.currently_airing },
  { value: 'finished_airing', label: AIRING_STATUS_LABELS.finished_airing },
  { value: 'not_yet_aired', label: AIRING_STATUS_LABELS.not_yet_aired },
]

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
  reload: () => Promise<void>,
): IncrementTarget {
  return {
    animeId: item.animeId,
    animeTitle: pickDisplayTitle(item.title, item.englishTitle),
    pictureUrl: item.pictureUrl,
    episodesWatched: item.entry.episodesWatched,
    previousStatus: item.entry.status,
    currentScore: item.entry.myScore,
    onSaved: (saved) => patchItem(setItems, item.animeId, saved),
    // Reloads rather than patching: completing an anime moves it between
    // status groups, a server-computed regrouping no mutation response describes.
    onCompleted: reload,
  }
}

type Derivation =
  | { mode: 'grouped'; total: number; shown: number; groups: { status: WatchStatus; items: MyListItemDto[] }[] }
  | { mode: 'flat'; total: number; shown: number; items: MyListItemDto[] }

// My list page: grouped by status (Watching -> On hold -> Plan to watch ->
// Completed -> Dropped) by default, with a filter bar (find-in-list, type,
// airing status, score) and a two-level sort (primary + tiebreaker) that
// composes over the whole page instead of per status group.
export function MyListPage() {
  const { data, loading, setData: setItems, reload } = usePageData<MyListItemDto[]>('my-list', getMyList)
  const items = data ?? []

  const [statusFilter, setStatusFilter] = useRestorableState<StatusFilter>('statusFilter', 'All')
  const [query, setQuery] = useRestorableState('query', '')
  const [typeFilter, setTypeFilter] = useRestorableState<string[]>('typeFilter', [])
  const [airingFilter, setAiringFilter] = useRestorableState<string[]>('airingFilter', [])
  const [scoreFilter, setScoreFilter] = useRestorableState<ScoreFilter>('scoreFilter', 'any')
  const [sort, setSort] = useRestorableState<SortKey>('sort', 'alphabetical')
  const [sortDirection, setSortDirection] = useRestorableState<SortDirection>('sortDirection', 'natural')
  const [sortThen, setSortThen] = useRestorableState<SortKey | null>('sortThen', null)
  const [groupByStatus, setGroupByStatus] = useRestorableState('groupByStatus', true)
  const [airingStatusFirst, setAiringStatusFirst] = useRestorableState<AiringStatus>(
    'airingStatusFirst',
    'finished_airing',
  )

  // Recap scope (design.md decision 9/10, tasks.md 7.3-7.5): the only use of
  // useSearchParams on this page — every control above keeps its
  // useRestorableState behaviour and knows nothing about this. A scope
  // arrives entirely from the URL (a recap's "see all" link), never from a
  // control on this page.
  const [searchParams, setSearchParams] = useSearchParams()
  const [pickerOpen, setPickerOpen] = useState(false)

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
      for (const key of ['recapMode', 'recapFrom', 'recapTo', 'recapYear', 'recapSeason', 'recapFilter', 'recapType']) {
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
    const typeLabel = recapType === 'all' ? 'All types' : mediaTypeLabel(recapType)
    return `${periodLabel} · ${filterLabel} · ${typeLabel}`
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

  const debouncedQuery = useDebouncedValue(query, 200)

  const openEdit = useCallback(
    (item: MyListItemDto) => {
      openEditor({
        animeId: item.animeId,
        animeTitle: pickDisplayTitle(item.title, item.englishTitle),
        totalEpisodes: item.totalEpisodes,
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
        await increment(buildIncrementTarget(item, setItems, reload))
      } finally {
        pendingIncrementRef.current = null
        setPendingIncrementId(null)
      }
    },
    [increment, setItems, reload],
  )

  const setEpisodesWatchedForItem = useCallback(
    async (item: MyListItemDto, value: number) => {
      if (pendingIncrementRef.current !== null) return
      pendingIncrementRef.current = item.animeId
      setPendingIncrementId(item.animeId)
      try {
        await setEpisodesWatched(buildIncrementTarget(item, setItems, reload), value)
      } finally {
        pendingIncrementRef.current = null
        setPendingIncrementId(null)
      }
    },
    [setEpisodesWatched, setItems, reload],
  )

  const changeScore = useCallback(
    async (item: MyListItemDto, score: number) => {
      if (pendingScoreRef.current !== null) return
      pendingScoreRef.current = item.animeId
      setPendingScoreId(item.animeId)
      try {
        const saved = await updateEntry(item.animeId, { myScore: score })
        patchItem(setItems, item.animeId, saved)
      } catch {
        // Leave the score as-is; the user can retry.
      } finally {
        pendingScoreRef.current = null
        setPendingScoreId(null)
      }
    },
    [setItems],
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

  // Type/airing filter option lists: only the values actually present in the
  // list, so a control never offers a choice that returns nothing (D6) —
  // scoped, same as everything else below, so an option the scope holds
  // none of isn't offered either.
  const filterOptions = useMemo(() => {
    const presentTypes = new Set<string>()
    let hasUnknownType = false
    const presentAiring = new Set<string>()
    let hasUnknownAiring = false
    for (const item of scopedItems) {
      if (item.mediaType) presentTypes.add(item.mediaType)
      else hasUnknownType = true
      if (item.airingStatus) presentAiring.add(item.airingStatus)
      else hasUnknownAiring = true
    }

    const typeOptions: FilterMultiSelectOption[] = MEDIA_TYPE_ORDER.filter((value) => presentTypes.has(value)).map(
      (value) => ({ value, label: mediaTypeLabel(value) }),
    )
    if (hasUnknownType) typeOptions.push({ value: 'unknown', label: 'Unknown' })

    const airingOptions: FilterMultiSelectOption[] = (Object.keys(AIRING_STATUS_LABELS) as AiringStatus[])
      .filter((status) => presentAiring.has(status))
      .map((status) => ({ value: status, label: AIRING_STATUS_LABELS[status] }))
    if (hasUnknownAiring) airingOptions.push({ value: 'unknown', label: 'Unknown' })

    return { typeOptions, airingOptions }
  }, [scopedItems])

  // One derivation: filter (status -> text -> type -> airing -> score), sort
  // with the composed comparator, then either group by status or leave flat
  // (D3). Runs off the debounced query so a keystroke doesn't re-derive over
  // the whole list.
  const derived = useMemo<Derivation>(() => {
    const statusScoped =
      statusFilter === 'All' ? scopedItems : scopedItems.filter((item) => item.entry.status === statusFilter)
    const needle = debouncedQuery.trim().toLowerCase()

    const matched = statusScoped.filter((item) => {
      if (needle) {
        const titleMatch = item.title.toLowerCase().includes(needle)
        const englishMatch = item.englishTitle ? item.englishTitle.toLowerCase().includes(needle) : false
        if (!titleMatch && !englishMatch) return false
      }
      if (typeFilter.length > 0 && !typeFilter.includes(item.mediaType ?? 'unknown')) return false
      if (airingFilter.length > 0 && !airingFilter.includes(item.airingStatus ?? 'unknown')) return false
      if (scoreFilter === 'rated' && item.entry.myScore == null) return false
      if (scoreFilter === 'unrated' && item.entry.myScore != null) return false
      return true
    })

    const comparator = composeComparator(sort, sortDirection, sortThen, airingStatusFirst)

    if (groupByStatus) {
      const groups = GROUP_ORDER.filter((status) => statusFilter === 'All' || statusFilter === status)
        .map((status) => ({
          status,
          items: matched.filter((item) => item.entry.status === status).sort(comparator),
        }))
        .filter((group) => group.items.length > 0)
      return { mode: 'grouped', total: statusScoped.length, shown: matched.length, groups }
    }

    return { mode: 'flat', total: statusScoped.length, shown: matched.length, items: [...matched].sort(comparator) }
  }, [scopedItems, statusFilter, debouncedQuery, typeFilter, airingFilter, scoreFilter, sort, sortDirection, sortThen, airingStatusFirst, groupByStatus])

  const isNarrowed = derived.shown !== derived.total
  const isOffDefault =
    query !== '' ||
    typeFilter.length > 0 ||
    airingFilter.length > 0 ||
    scoreFilter !== 'any' ||
    sort !== 'alphabetical' ||
    sortDirection !== 'natural' ||
    sortThen !== null ||
    !groupByStatus

  function clearFilters() {
    setQuery('')
    setTypeFilter([])
    setAiringFilter([])
    setScoreFilter('any')
    setSort('alphabetical')
    setSortDirection('natural')
    setSortThen(null)
    setGroupByStatus(true)
  }

  function handleSortChange(next: SortKey) {
    setSort(next)
    // A key can't tiebreak itself — drop it rather than leave a stale
    // selection the "then by" control no longer offers.
    setSortThen((prev) => (prev === next ? null : prev))
  }

  // Rank numbers follow the grouping toggle, not the sort key: shown only
  // when the list is flat and not alphabetical (D9).
  const showRanks = !groupByStatus && sort !== 'alphabetical'
  // The airing badge shows on every row while the airing filter is doing
  // something or airing status is the primary sort — Plan-to-watch rows
  // always show it regardless (D11), handled per-row below.
  const airingBadgeActive = airingFilter.length > 0 || sort === 'airingStatus'

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

  function renderFilterBar() {
    const tiebreakOptions = SORT_OPTIONS.filter((option) => option.value !== sort)
    return (
      <div className="my-list-page__filter-bar">
        <div className="my-list-page__filter-cluster">
          <input
            type="text"
            className="my-list-page__query"
            placeholder="Find in list…"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            aria-label="Find in list"
          />
          <FilterMultiSelect label="Type" options={filterOptions.typeOptions} selected={typeFilter} onChange={setTypeFilter} />
          <FilterMultiSelect
            label="Airing"
            options={filterOptions.airingOptions}
            selected={airingFilter}
            onChange={setAiringFilter}
          />
          <select
            className="my-list-page__sort"
            value={scoreFilter}
            onChange={(event) => setScoreFilter(event.target.value as ScoreFilter)}
            aria-label="Filter by score"
          >
            <option value="any">Score: Any</option>
            <option value="rated">Score: Rated</option>
            <option value="unrated">Score: Unrated</option>
          </select>
        </div>
        <div className="my-list-page__order-cluster">
          <select
            className="my-list-page__sort"
            value={sort}
            onChange={(event) => handleSortChange(event.target.value as SortKey)}
            aria-label="Sort by"
          >
            {SORT_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className={`my-list-page__tab${sortDirection === 'reversed' ? ' my-list-page__tab--active' : ''}`}
            aria-pressed={sortDirection === 'reversed'}
            onClick={() => setSortDirection((prev) => (prev === 'natural' ? 'reversed' : 'natural'))}
          >
            Reverse
          </button>
          {sort === 'airingStatus' && (
            <select
              className="my-list-page__sort"
              value={airingStatusFirst}
              onChange={(event) => setAiringStatusFirst(event.target.value as AiringStatus)}
              aria-label="Show which airing status first"
            >
              {AIRING_STATUS_FIRST_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label} first
                </option>
              ))}
            </select>
          )}
          <select
            className="my-list-page__sort"
            value={sortThen ?? ''}
            onChange={(event) => setSortThen(event.target.value === '' ? null : (event.target.value as SortKey))}
            aria-label="Then by"
          >
            <option value="">— then by —</option>
            {tiebreakOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <button
            type="button"
            className={`my-list-page__tab${groupByStatus ? ' my-list-page__tab--active' : ''}`}
            aria-pressed={groupByStatus}
            onClick={() => setGroupByStatus((prev) => !prev)}
          >
            Group by status
          </button>
          {isOffDefault && (
            <button type="button" className="my-list-page__clear-filters" onClick={clearFilters}>
              Clear filters
            </button>
          )}
        </div>
      </div>
    )
  }

  function renderBody() {
    if (loading || (hasRecapScope && recapScopeLoading)) return <p className="my-list-page__loading">Loading…</p>

    if (derived.total === 0) return <p className="my-list-page__empty">Nothing here yet.</p>

    if (derived.shown === 0) {
      return (
        <div className="my-list-page__empty">
          <p>Nothing matches these filters.</p>
          <button type="button" className="my-list-page__clear-filters" onClick={clearFilters}>
            Clear filters
          </button>
        </div>
      )
    }

    if (derived.mode === 'grouped') {
      return (
        <>
          {derived.groups.map((group) => (
            <section key={group.status} className="my-list-page__group">
              <div className="my-list-page__group-header">
                <h2>{STATUS_LABELS[group.status]}</h2>
              </div>
              <ul className="my-list-page__list">{group.items.map((item) => renderRow(item))}</ul>
            </section>
          ))}
        </>
      )
    }

    return (
      <section className="my-list-page__group">
        <div className="my-list-page__group-header">
          <h2>{statusFilter === 'All' ? 'All' : STATUS_LABELS[statusFilter]}</h2>
        </div>
        <ul className="my-list-page__list">
          {derived.items.map((item, index) => renderRow(item, showRanks ? index + 1 : undefined))}
        </ul>
      </section>
    )
  }

  return (
    <div className="my-list-page">
      <div className="my-list-page__header">
        <h1>My list</h1>
      </div>

      <div className="my-list-page__tabs" role="tablist" aria-label="Filter by status">
        {FILTER_TABS.map((tab) => {
          const classes = ['my-list-page__tab']
          if (tab.value !== 'All') classes.push(`my-list-page__tab--${STATUS_CLASS[tab.value]}`)
          if (statusFilter === tab.value) classes.push('my-list-page__tab--active')
          return (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={statusFilter === tab.value}
              className={classes.join(' ')}
              onClick={() => setStatusFilter(tab.value)}
            >
              {tab.label}
            </button>
          )
        })}
        <button
          type="button"
          className="my-list-page__tab my-list-page__recap-button"
          onClick={() => setPickerOpen(true)}
        >
          Recap a period
        </button>
      </div>

      {hasRecapScope && (
        <div className="my-list-page__recap-scope">
          <span>Recap scope: {recapScopeLabel()}</span>
          <Link to={`/recap?${recapSearchFromScope()}`} className="my-list-page__recap-scope-link">
            View recap
          </Link>
          <button type="button" className="my-list-page__recap-scope-dismiss" onClick={dismissRecapScope} aria-label="Dismiss recap scope">
            &times;
          </button>
        </div>
      )}

      {renderFilterBar()}
      {isNarrowed && (
        <p className="my-list-page__count">
          Showing {derived.shown} of {derived.total}
        </p>
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
