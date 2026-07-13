import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getMyList } from '../api/client.ts'
import type { MyListItemDto, WatchStatus } from '../api/types.ts'
import { ProgressBar } from '../components/ProgressBar.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { compareByMalScoreDesc, compareByTitleAlphabetical, pickDisplayTitle, STATUS_LABELS } from '../utils/anime.ts'
import './MyListPage.css'

type SortKey = 'alphabetical' | 'malScore' | 'myScore'
type StatusFilter = 'All' | WatchStatus

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
  { value: 'malScore', label: 'MAL score' },
  { value: 'myScore', label: 'My score' },
]

function sortByKey(items: MyListItemDto[], sort: SortKey): MyListItemDto[] {
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
  const { openEditor } = useEntryEditor()

  useEffect(() => {
    getMyList()
      .then(setItems)
      .catch(() => {
        // Page just stays empty; nothing else to react to here.
      })
      .finally(() => setLoading(false))
  }, [])

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

  function renderRow(item: MyListItemDto, rank?: number) {
    return (
      <li key={item.animeId} className="my-list-row">
        {rank !== undefined && <span className="my-list-row__rank">#{rank}</span>}
        <Link to={`/anime/${item.animeId}`} className="my-list-row__link">
          {item.pictureUrl ? (
            <img src={item.pictureUrl} alt="" className="my-list-row__picture" />
          ) : (
            <div className="my-list-row__picture my-list-row__picture--placeholder" aria-hidden="true" />
          )}
          <span className="my-list-row__info">
            <span className="my-list-row__title">{pickDisplayTitle(item.title, item.englishTitle)}</span>
            <span className="my-list-row__type">{item.mediaType ? item.mediaType.toUpperCase() : 'Unknown'}</span>
          </span>
        </Link>
        <span className="my-list-row__progress">
          <ProgressBar watched={item.entry.episodesWatched} total={item.totalEpisodes} />
        </span>
        <span className="my-list-row__my-score">{item.entry.myScore ?? '—'}</span>
        <span className="my-list-row__mal-score">
          <ScoreValue value={item.malScore} />
        </span>
        <button type="button" className="my-list-row__edit" onClick={() => openEdit(item)}>
          Edit
        </button>
      </li>
    )
  }

  const filteredItems = statusFilter === 'All' ? items : items.filter((item) => item.entry.status === statusFilter)
  const showRanked = sort !== 'alphabetical'

  return (
    <div className="my-list-page">
      <div className="my-list-page__header">
        <h1>My list</h1>
        <select
          className="my-list-page__sort"
          value={sort}
          onChange={(event) => setSort(event.target.value as SortKey)}
          aria-label="Sort my list"
        >
          {SORT_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="my-list-page__tabs" role="tablist" aria-label="Filter by status">
        {FILTER_TABS.map((tab) => (
          <button
            key={tab.value}
            type="button"
            role="tab"
            aria-selected={statusFilter === tab.value}
            className={
              statusFilter === tab.value ? 'my-list-page__tab my-list-page__tab--active' : 'my-list-page__tab'
            }
            onClick={() => setStatusFilter(tab.value)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {loading ? (
        <p className="my-list-page__loading">Loading…</p>
      ) : filteredItems.length === 0 ? (
        <p className="my-list-page__empty">Nothing here yet.</p>
      ) : showRanked ? (
        <ul className="my-list-page__list">{sortByKey(filteredItems, sort).map((item, index) => renderRow(item, index + 1))}</ul>
      ) : (
        GROUP_ORDER.filter((status) => statusFilter === 'All' || statusFilter === status).map((status) => {
          const groupItems = sortByKey(
            filteredItems.filter((item) => item.entry.status === status),
            sort,
          )
          if (groupItems.length === 0) return null
          return (
            <section key={status} className="my-list-page__group">
              <h2>{STATUS_LABELS[status]}</h2>
              <ul className="my-list-page__list">{groupItems.map((item) => renderRow(item))}</ul>
            </section>
          )
        })
      )}
    </div>
  )
}
