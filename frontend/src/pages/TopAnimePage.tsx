import { useState } from 'react'
import { Link } from 'react-router-dom'
import { getTopAnime, updateEntry } from '../api/client.ts'
import type { TopAnimeItemDto } from '../api/types.ts'
import { Pagination } from '../components/Pagination.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './TopAnimePage.css'

const PAGE_SIZE = 50

// Top anime page: the global MAL ranking (not just my list), re-fetched at
// most once per local day on visit. Each row's action is conditional — Add
// when the anime isn't in my list yet (adds it as Plan to watch and flips in
// place to Edit), Edit otherwise — both open the same overlay used
// everywhere else in the app. The ranking covers up to 500 rows, paginated
// client-side at 50/page.
export function TopAnimePage() {
  const { data, loading, setData: setItems } = usePageData<TopAnimeItemDto[]>('top-anime', getTopAnime)
  const items = data ?? []
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [page, setPage] = useRestorableState('page', 1)
  const { openEditor } = useEntryEditor()

  const totalPages = Math.max(1, Math.ceil(items.length / PAGE_SIZE))
  const pageItems = items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  function setEntry(animeId: number, entry: TopAnimeItemDto['entry']) {
    setItems((prev) => prev && prev.map((item) => (item.animeId === animeId ? { ...item, entry } : item)))
  }

  async function handleAdd(item: TopAnimeItemDto) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      const saved = await updateEntry(item.animeId, {})
      setEntry(item.animeId, saved)
    } catch {
      // Leave it as "Add" so the user can retry.
    } finally {
      setPendingId(null)
    }
  }

  function handleEdit(item: TopAnimeItemDto) {
    if (!item.entry) return
    openEditor({
      animeId: item.animeId,
      animeTitle: pickDisplayTitle(item.title, item.englishTitle),
      totalEpisodes: item.totalEpisodes,
      entry: item.entry,
      onSaved: (saved) => setEntry(item.animeId, saved),
      onDeleted: () => setEntry(item.animeId, null),
    })
  }

  return (
    <div className="top-anime-page">
      <div className="top-anime-page__header">
        <h1>Top anime</h1>
        {items.length > 0 && (
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
        )}
      </div>

      {loading ? (
        <p className="top-anime-page__loading">Loading…</p>
      ) : items.length === 0 ? (
        <p className="top-anime-page__empty">No ranking data yet.</p>
      ) : (
        <ol className="top-anime-page__list">
          {pageItems.map((item) => (
            <li key={item.animeId} className="top-anime-row">
              <span className="top-anime-row__rank">#{item.rank}</span>
              <Link to={`/anime/${item.animeId}`} className="top-anime-row__link">
                {item.pictureUrl ? (
                  <img src={item.pictureUrl} alt="" className="top-anime-row__picture" />
                ) : (
                  <div className="top-anime-row__picture top-anime-row__picture--placeholder" aria-hidden="true" />
                )}
                <span className="top-anime-row__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
                  {pickDisplayTitle(item.title, item.englishTitle)}
                </span>
              </Link>
              <span className="top-anime-row__my-score">{item.entry?.myScore ?? '—'}</span>
              <span className="top-anime-row__mal-score">
                <ScoreValue value={item.malScore} completed={item.entry?.status === 'Completed'} />
              </span>
              {item.entry ? (
                <button type="button" className="top-anime-row__action" onClick={() => handleEdit(item)}>
                  Edit
                </button>
              ) : (
                <button
                  type="button"
                  className="top-anime-row__action"
                  disabled={pendingId === item.animeId}
                  onClick={() => handleAdd(item)}
                >
                  {pendingId === item.animeId ? 'Adding…' : 'Add'}
                </button>
              )}
            </li>
          ))}
        </ol>
      )}

      {items.length > 0 && (
        <div className="top-anime-page__footer">
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
        </div>
      )}
    </div>
  )
}
