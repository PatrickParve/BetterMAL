import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getTopAnime, updateEntry } from '../api/client.ts'
import type { TopAnimeItemDto } from '../api/types.ts'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import './TopAnimePage.css'

// Top anime page: the global MAL ranking (not just my list), re-fetched at
// most once per local day on visit. Each row's action is conditional — Add
// when the anime isn't in my list yet (adds it as Plan to watch and flips in
// place to Edit), Edit otherwise — both open the same overlay used
// everywhere else in the app.
export function TopAnimePage() {
  const [items, setItems] = useState<TopAnimeItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const { openEditor } = useEntryEditor()

  useEffect(() => {
    getTopAnime()
      .then(setItems)
      .catch(() => {
        // Page just stays empty; nothing else to react to here.
      })
      .finally(() => setLoading(false))
  }, [])

  function setEntry(animeId: number, entry: TopAnimeItemDto['entry']) {
    setItems((prev) => prev.map((item) => (item.animeId === animeId ? { ...item, entry } : item)))
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
      animeTitle: item.title,
      totalEpisodes: item.totalEpisodes,
      entry: item.entry,
      onSaved: (saved) => setEntry(item.animeId, saved),
    })
  }

  return (
    <div className="top-anime-page">
      <h1>Top anime</h1>

      {loading ? (
        <p className="top-anime-page__loading">Loading…</p>
      ) : items.length === 0 ? (
        <p className="top-anime-page__empty">No ranking data yet.</p>
      ) : (
        <ol className="top-anime-page__list">
          {items.map((item) => (
            <li key={item.animeId} className="top-anime-row">
              <span className="top-anime-row__rank">#{item.rank}</span>
              <Link to={`/anime/${item.animeId}`} className="top-anime-row__link">
                {item.pictureUrl ? (
                  <img src={item.pictureUrl} alt="" className="top-anime-row__picture" />
                ) : (
                  <div className="top-anime-row__picture top-anime-row__picture--placeholder" aria-hidden="true" />
                )}
                <span className="top-anime-row__title">{item.title}</span>
              </Link>
              <span className="top-anime-row__my-score">{item.entry?.myScore ?? '—'}</span>
              <span className="top-anime-row__mal-score">
                <ScoreValue value={item.malScore} />
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
    </div>
  )
}
