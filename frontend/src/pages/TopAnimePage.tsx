import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getTopAnime, updateEntry } from '../api/client.ts'
import type { TopAnimeItemDto } from '../api/types.ts'
import { Pagination } from '../components/Pagination.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { pickDisplayTitle } from '../utils/anime.ts'
import './TopAnimePage.css'

const PAGE_SIZE = 50

// The full ranking (up to 500 rows) is identical regardless of which page is
// being viewed — `page` only slices it client-side — so it's cached at module
// scope and fetched at most once per app session, rather than through
// usePageData's per-history-entry cache. That cache is keyed by location, so
// it would treat every page's own URL (see `page` below) as a distinct
// resource needing its own network fetch, flashing "Loading…" on every page
// change even though the underlying data never varies.
let cachedItems: TopAnimeItemDto[] | null = null
let inFlightLoad: Promise<TopAnimeItemDto[]> | null = null

function loadTopAnimeOnce(): Promise<TopAnimeItemDto[]> {
  if (cachedItems) return Promise.resolve(cachedItems)
  inFlightLoad ??= getTopAnime().then((result) => {
    cachedItems = result
    inFlightLoad = null
    return result
  })
  return inFlightLoad
}

// Top anime page: the global MAL ranking (not just my list). Only the
// flat-row tier (rank 11+) carries a list-action button, conditional — Add
// when the anime isn't in my list yet (adds it as Plan to watch and flips in
// place to Edit), Edit otherwise — which opens the same overlay used
// everywhere else in the app. The ranking covers up to 500 rows, paginated
// client-side at 50/page. `page` lives in the URL (`?page=N`) rather than as
// plain component state, so each page change is a real history entry — the
// browser's back/forward buttons, keyboard shortcuts, and trackpad swipe
// gesture step through the ranking's own pages instead of leaving the page
// entirely on the first back.
export function TopAnimePage() {
  const [rawItems, setRawItems] = useState<TopAnimeItemDto[] | null>(cachedItems)
  const items = rawItems ?? []
  const [loading, setLoading] = useState(cachedItems === null)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const { openEditor } = useEntryEditor()

  const pageParam = Number(searchParams.get('page'))
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1

  useEffect(() => {
    if (cachedItems) return
    let cancelled = false
    loadTopAnimeOnce().then((result) => {
      if (!cancelled) {
        setRawItems(result)
        setLoading(false)
      }
    })
    return () => {
      cancelled = true
    }
  }, [])

  // Keeps the module cache in sync with optimistic updates from Add/Edit, so
  // a later page navigation or a full remount (leaving Top Anime and coming
  // back) sees the change instead of reverting to the last network response.
  function setItems(update: TopAnimeItemDto[] | null | ((prev: TopAnimeItemDto[] | null) => TopAnimeItemDto[] | null)) {
    setRawItems((prev) => {
      const next = typeof update === 'function' ? update(prev) : update
      cachedItems = next
      return next
    })
  }

  const totalPages = Math.max(1, Math.ceil(items.length / PAGE_SIZE))
  const pageItems = items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  // Ranks 1-3 and 4-10 only ever occur on page 1 (page size is 50), so
  // slicing by rank alone naturally scopes the showcase/card sections to
  // that page without checking `page` directly.
  const showcaseItems = pageItems.filter((item) => item.rank <= 3)
  const cardItems = pageItems.filter((item) => item.rank > 3 && item.rank <= 10)
  const restItems = pageItems.filter((item) => item.rank > 10)

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

  // Pushes a real history entry rather than replacing the current one, so
  // browser/keyboard/swipe back steps to the previous page instead of
  // leaving Top Anime entirely — and, as a side effect, so does
  // useScrollRestoration's existing location-keyed scroll-to-top, since a
  // page change is now a genuine navigation rather than local state.
  function goToPage(nextPage: number) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (nextPage <= 1) params.delete('page')
      else params.set('page', String(nextPage))
      return params
    })
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

  // Shared across the showcase, card grid, and flat list — only the button's
  // own class differs per layout.
  function renderActionButton(item: TopAnimeItemDto, className: string) {
    return item.entry ? (
      <button type="button" className={className} onClick={() => handleEdit(item)}>
        Edit
      </button>
    ) : (
      <button
        type="button"
        className={className}
        disabled={pendingId === item.animeId}
        onClick={() => handleAdd(item)}
      >
        {pendingId === item.animeId ? 'Adding…' : 'Add'}
      </button>
    )
  }

  return (
    <div className="top-anime-page">
      <div className="top-anime-page__header">
        <h1>Top anime</h1>
        {items.length > 0 && (
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={goToPage} />
        )}
      </div>

      {loading ? (
        <p className="top-anime-page__loading">Loading…</p>
      ) : items.length === 0 ? (
        <p className="top-anime-page__empty">No ranking data yet.</p>
      ) : (
        <>
          {showcaseItems.length > 0 && (
            <div className="top-anime-showcase">
              {showcaseItems.map((item) => (
                <div
                  key={item.animeId}
                  className={`top-anime-showcase__item top-anime-showcase__item--rank-${item.rank}`}
                >
                  <Link to={`/anime/${item.animeId}`} className="top-anime-showcase__poster-link">
                    {item.pictureUrl ? (
                      <img src={item.pictureUrl} alt="" className="top-anime-showcase__picture" />
                    ) : (
                      <div
                        className="top-anime-showcase__picture top-anime-showcase__picture--placeholder"
                        aria-hidden="true"
                      />
                    )}
                  </Link>
                  <div className="top-anime-showcase__info">
                    <span className="top-anime-rank top-anime-rank--lg">#{item.rank}</span>
                    <Link to={`/anime/${item.animeId}`} className="top-anime-showcase__title-link">
                      <span
                        className="top-anime-showcase__title"
                        title={pickDisplayTitle(item.title, item.englishTitle)}
                      >
                        {pickDisplayTitle(item.title, item.englishTitle)}
                      </span>
                    </Link>
                    <div className="top-anime-showcase__scores">
                      <ScoreChip role="mine" label="My score">
                        {item.entry?.myScore ?? '—'}
                      </ScoreChip>
                      <ScoreChip role="mal" label="MAL">
                        <ScoreValue value={item.malScore} completed={item.entry?.status === 'Completed'} />
                      </ScoreChip>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {cardItems.length > 0 && (
            <ul className="top-anime-cards">
              {cardItems.map((item) => (
                <li key={item.animeId} className="top-anime-card">
                  <Link to={`/anime/${item.animeId}`} className="top-anime-card__link">
                    <span className="top-anime-rank top-anime-rank--sm">#{item.rank}</span>
                    {item.pictureUrl ? (
                      <img src={item.pictureUrl} alt="" className="top-anime-card__picture" />
                    ) : (
                      <div
                        className="top-anime-card__picture top-anime-card__picture--placeholder"
                        aria-hidden="true"
                      />
                    )}
                    <span className="top-anime-card__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
                      {pickDisplayTitle(item.title, item.englishTitle)}
                    </span>
                  </Link>
                  <span className="top-anime-card__scores">
                    <span className="score--mine">{item.entry?.myScore ?? '—'}</span>
                    <span className="score--mal">
                      <ScoreValue value={item.malScore} completed={item.entry?.status === 'Completed'} />
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}

          {restItems.length > 0 && (
            <ol className="top-anime-page__list">
              {restItems.map((item) => (
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
                  <span className="top-anime-row__my-score score--mine">{item.entry?.myScore ?? '—'}</span>
                  <span className="top-anime-row__mal-score score--mal">
                    <ScoreValue value={item.malScore} completed={item.entry?.status === 'Completed'} />
                  </span>
                  {renderActionButton(item, 'top-anime-row__action')}
                </li>
              ))}
            </ol>
          )}
        </>
      )}

      {items.length > 0 && (
        <div className="top-anime-page__footer">
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={goToPage} />
        </div>
      )}
    </div>
  )
}
