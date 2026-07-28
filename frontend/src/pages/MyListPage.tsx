import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMyList, updateEntry } from '../api/client.ts'
import type { MyListItemDto, WatchStatus } from '../api/types.ts'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { useEpisodeIncrement } from '../context/CompletionPromptContext.tsx'
import {
  AIRING_STATUS_LABELS,
  airingStatusShortLabel,
  compareByAiringStatus,
  compareByMalScoreDesc,
  compareByTitleAlphabetical,
  pickDisplayTitle,
  STATUS_CLASS,
  STATUS_LABELS,
} from '../utils/anime.ts'
import type { AiringStatus } from '../utils/anime.ts'
import './MyListPage.css'

type SortKey = 'alphabetical' | 'malScore' | 'myScore' | 'airingStatus'
type StatusFilter = 'All' | WatchStatus

const GROUP_ORDER: WatchStatus[] = ['Watching', 'OnHold', 'PlanToWatch', 'Completed', 'Dropped']
const SCORE_OPTIONS = Array.from({ length: 10 }, (_, i) => i + 1)

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
  { value: 'malScore', label: 'MAL score' },
  { value: 'myScore', label: 'My score' },
]

const AIRING_STATUS_SORT_OPTION: { value: SortKey; label: string } = {
  value: 'airingStatus',
  label: 'Airing status',
}

const AIRING_STATUS_FIRST_OPTIONS: { value: AiringStatus; label: string }[] = [
  { value: 'currently_airing', label: AIRING_STATUS_LABELS.currently_airing },
  { value: 'finished_airing', label: AIRING_STATUS_LABELS.finished_airing },
  { value: 'not_yet_aired', label: AIRING_STATUS_LABELS.not_yet_aired },
]

function sortByKey(items: MyListItemDto[], sort: SortKey, airingStatusFirst: AiringStatus): MyListItemDto[] {
  const sorted = [...items]
  switch (sort) {
    case 'malScore':
      sorted.sort(compareByMalScoreDesc)
      break
    case 'myScore':
      sorted.sort((a, b) => (b.entry.myScore ?? -1) - (a.entry.myScore ?? -1))
      break
    case 'alphabetical':
      sorted.sort(compareByTitleAlphabetical)
      break
    case 'airingStatus':
      sorted.sort(compareByAiringStatus(airingStatusFirst))
      break
  }
  return sorted
}

// My list page: grouped by status (Watching -> On hold -> Plan to watch ->
// Completed -> Dropped) by default. Sorting by score switches to one flat
// ranked list with rank numbers instead — grouping by status and ranking by
// score don't mix, mirroring how MAL's own list view behaves.
export function MyListPage() {
  const [items, setItems] = useState<MyListItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [sort, setSort] = useState<SortKey>('alphabetical')
  const [airingStatusFirst, setAiringStatusFirst] = useState<AiringStatus>('finished_airing')
  const [pendingIncrementId, setPendingIncrementId] = useState<number | null>(null)
  const [pendingScoreId, setPendingScoreId] = useState<number | null>(null)
  const { openEditor } = useEntryEditor()
  const increment = useEpisodeIncrement()

  const loadList = useCallback(() => {
    return getMyList()
      .then(setItems)
      .catch(() => {
        // Page just stays empty; nothing else to react to here.
      })
  }, [])

  useEffect(() => {
    loadList().finally(() => setLoading(false))
  }, [loadList])

  function handleSaved(animeId: number) {
    return (saved: MyListItemDto['entry']) => {
      setItems((prev) => prev.map((item) => (item.animeId === animeId ? { ...item, entry: saved } : item)))
    }
  }

  function openEdit(item: MyListItemDto) {
    openEditor({
      animeId: item.animeId,
      animeTitle: pickDisplayTitle(item.title, item.englishTitle),
      totalEpisodes: item.totalEpisodes,
      entry: item.entry,
      onSaved: handleSaved(item.animeId),
    })
  }

  async function incrementEpisodes(item: MyListItemDto) {
    if (pendingIncrementId !== null) return
    setPendingIncrementId(item.animeId)
    try {
      await increment({
        animeId: item.animeId,
        animeTitle: pickDisplayTitle(item.title, item.englishTitle),
        pictureUrl: item.pictureUrl,
        episodesWatched: item.entry.episodesWatched,
        previousStatus: item.entry.status,
        currentScore: item.entry.myScore,
        onSaved: handleSaved(item.animeId),
        onCompleted: loadList,
      })
    } finally {
      setPendingIncrementId(null)
    }
  }

  async function changeScore(item: MyListItemDto, score: number) {
    if (pendingScoreId !== null) return
    setPendingScoreId(item.animeId)
    try {
      const saved = await updateEntry(item.animeId, { myScore: score })
      handleSaved(item.animeId)(saved)
    } catch {
      // Leave the score as-is; the user can retry.
    } finally {
      setPendingScoreId(null)
    }
  }

  function renderRow(item: MyListItemDto, rank?: number) {
    const airingLabel =
      item.entry.status === 'PlanToWatch' ? airingStatusShortLabel(item.airingStatus) : null
    return (
      <li key={item.animeId} className={`my-list-row my-list-row--${STATUS_CLASS[item.entry.status]}`}>
        {rank !== undefined && <span className="my-list-row__rank">#{rank}</span>}
        <Link to={`/anime/${item.animeId}`} className="my-list-row__link">
          {item.pictureUrl ? (
            <img src={item.pictureUrl} alt="" className="my-list-row__picture" />
          ) : (
            <div className="my-list-row__picture my-list-row__picture--placeholder" aria-hidden="true" />
          )}
          <span className="my-list-row__info">
            <span className="my-list-row__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
              {pickDisplayTitle(item.title, item.englishTitle)}
            </span>
            <span className="my-list-row__type">
              {item.mediaType ? item.mediaType.toUpperCase() : 'Unknown'}
              {airingLabel && <span className="my-list-row__airing-badge"> · {airingLabel}</span>}
            </span>
          </span>
        </Link>
        <span className="my-list-row__progress">
          <ProgressBar
            watched={item.entry.episodesWatched}
            total={item.totalEpisodes}
            onIncrement={() => incrementEpisodes(item)}
            incrementPending={pendingIncrementId === item.animeId}
            incrementLabel={`Increment episodes watched for ${pickDisplayTitle(item.title, item.englishTitle)}`}
          />
        </span>
        <span className="my-list-row__my-score">
          <select
            className="my-list-row__score-select"
            value={item.entry.myScore ?? 0}
            disabled={pendingScoreId === item.animeId}
            onChange={(event) => changeScore(item, Number(event.target.value))}
            aria-label={`Set your score for ${pickDisplayTitle(item.title, item.englishTitle)}`}
          >
            <option value={0}>—</option>
            {SCORE_OPTIONS.map((score) => (
              <option key={score} value={score}>
                {score}
              </option>
            ))}
          </select>
        </span>
        <span className="my-list-row__mal-score">
          <ScoreValue value={item.malScore} completed={item.entry.status === 'Completed'} />
        </span>
        <button type="button" className="my-list-row__edit" onClick={() => openEdit(item)}>
          Edit
        </button>
      </li>
    )
  }

  const filteredItems = statusFilter === 'All' ? items : items.filter((item) => item.entry.status === statusFilter)
  const showRanked = sort !== 'alphabetical'

  function selectStatusFilter(next: StatusFilter) {
    if (sort === 'airingStatus' && next !== 'PlanToWatch') setSort('alphabetical')
    setStatusFilter(next)
  }

  function renderSortControls() {
    const options = statusFilter === 'PlanToWatch' ? [...SORT_OPTIONS, AIRING_STATUS_SORT_OPTION] : SORT_OPTIONS
    return (
      <div className="my-list-page__sort-controls">
        <select
          className="my-list-page__sort"
          value={sort}
          onChange={(event) => setSort(event.target.value as SortKey)}
          aria-label="Sort my list"
        >
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
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
      </div>
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
              onClick={() => selectStatusFilter(tab.value)}
            >
              {tab.label}
            </button>
          )
        })}
      </div>

      {loading ? (
        <p className="my-list-page__loading">Loading…</p>
      ) : filteredItems.length === 0 ? (
        <p className="my-list-page__empty">Nothing here yet.</p>
      ) : showRanked ? (
        <section className="my-list-page__group">
          <div className="my-list-page__group-header">
            <h2>{statusFilter === 'All' ? 'All' : STATUS_LABELS[statusFilter]}</h2>
            {renderSortControls()}
          </div>
          <ul className="my-list-page__list">
            {sortByKey(filteredItems, sort, airingStatusFirst).map((item, index) =>
              renderRow(item, sort === 'airingStatus' ? undefined : index + 1),
            )}
          </ul>
        </section>
      ) : (
        GROUP_ORDER.filter((status) => statusFilter === 'All' || statusFilter === status).map((status) => {
          const groupItems = sortByKey(
            filteredItems.filter((item) => item.entry.status === status),
            sort,
            airingStatusFirst,
          )
          if (groupItems.length === 0) return null
          return (
            <section key={status} className="my-list-page__group">
              <div className="my-list-page__group-header">
                <h2>{STATUS_LABELS[status]}</h2>
                {renderSortControls()}
              </div>
              <ul className="my-list-page__list">{groupItems.map((item) => renderRow(item))}</ul>
            </section>
          )
        })
      )}
    </div>
  )
}
