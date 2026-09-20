import { useEffect, useReducer, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError, getTopAnime, refreshTopAnime, updateEntry } from '../api/client.ts'
import { TOP_ANIME_RANKING_TYPES, type TopAnimeItemDto, type TopAnimeRankingType } from '../api/types.ts'
import { Pagination } from '../components/Pagination.tsx'
import { RowPicture } from '../components/RowPicture.tsx'
import { ScoreChip } from '../components/ScoreChip.tsx'
import { ScoreValue } from '../components/ScoreValue.tsx'
import { useActionFailure } from '../context/ActionFailureContext.tsx'
import { useEntryEditor } from '../context/EntryEditorContext.tsx'
import { isScoreRevealableStatus, pickDisplayTitle } from '../utils/anime.ts'
import './TopAnimePage.css'

const PAGE_SIZE = 50

const VALID_RANKING_TYPES = new Set<string>(TOP_ANIME_RANKING_TYPES.map((t) => t.value))

function isRankingType(value: string | null): value is TopAnimeRankingType {
  return value !== null && VALID_RANKING_TYPES.has(value)
}

// A given ranking list (up to 500 rows) is identical regardless of which page
// is being viewed — `page` only slices it client-side — so each list is
// cached at module scope and read at most once per app session, rather than
// through usePageData's per-history-entry cache. That cache is keyed by
// location, so it would treat every page's own URL (see `page` below), and
// every list's own URL (see `type` below), as a distinct resource needing its
// own network fetch, flashing "Loading…" on every page or list change even
// though a given list's data never varies once fetched. Keyed by ranking type
// so switching lists doesn't invalidate or block on another list's cache.
//
// A type's first load reads the server's cache (GetRankingAsync — cache-only,
// never calls MAL) and resolves with it immediately; that load then posts one
// background refresh (RefreshAsync) for the type. A `fetched` outcome re-reads
// and replaces the cached rows for that type, in place, with no loading
// state; `skipped` and `failed` leave the cache exactly as it is. The refresh
// rides along with a type's first load only — reusing an already-cached type
// makes no request of any kind — so a list is refreshed at most once per
// session however often it's re-selected (design D3).
const cachedItemsByType = new Map<TopAnimeRankingType, TopAnimeItemDto[]>()
const inFlightLoadByType = new Map<TopAnimeRankingType, Promise<TopAnimeItemDto[]>>()

// Add/Edit/Delete entries made this session, keyed by anime id, re-applied
// over any freshly re-read list before it replaces the cached one — a
// background refresh's re-read can otherwise race an edit's own write and
// revert it (design D4). Holds the same value setEntry below already
// computes per anime, so this is a record of what it did, not new state to
// keep in sync.
const sessionEditsByAnimeId = new Map<number, TopAnimeItemDto['entry']>()

// Mounted instances register here so a background refresh landing while
// they're already on screen reaches the DOM even without a list switch —
// mutating the Maps above doesn't itself trigger a React re-render.
const cacheUpdateListeners = new Set<() => void>()

function notifyCacheUpdate() {
  for (const listener of cacheUpdateListeners) listener()
}

function applySessionEdits(items: TopAnimeItemDto[]): TopAnimeItemDto[] {
  if (sessionEditsByAnimeId.size === 0) return items
  return items.map((item) =>
    sessionEditsByAnimeId.has(item.animeId) ? { ...item, entry: sessionEditsByAnimeId.get(item.animeId) ?? null } : item,
  )
}

function refreshInBackground(type: TopAnimeRankingType) {
  refreshTopAnime(type)
    .then((result) => {
      if (result.outcome !== 'fetched') return undefined

      return getTopAnime(type).then((fresh) => {
        cachedItemsByType.set(type, applySessionEdits(fresh))
        notifyCacheUpdate()
      })
    })
    .catch(() => {
      // Swallowed like every other background refresh in this app — the
      // cached rows, if any, are left exactly as they are.
    })
}

// The most recently displayed list, tracked at module scope (not component
// state) so that even across an unmount/remount — leaving Top Anime and
// coming back to a list not yet cached — the page has something to show
// muted while the new list loads, rather than blanking to "Loading…".
let lastShownType: TopAnimeRankingType | null = null

function loadTopAnimeOnce(type: TopAnimeRankingType): Promise<TopAnimeItemDto[]> {
  const cached = cachedItemsByType.get(type)
  if (cached) return Promise.resolve(cached)
  let inFlight = inFlightLoadByType.get(type)
  if (!inFlight) {
    inFlight = getTopAnime(type).then((result) => {
      cachedItemsByType.set(type, result)
      inFlightLoadByType.delete(type)
      refreshInBackground(type)
      return result
    })
    inFlightLoadByType.set(type, inFlight)
  }
  return inFlight
}

type Display = { type: TopAnimeRankingType; items: TopAnimeItemDto[] }

function initialFallback(type: TopAnimeRankingType): Display | null {
  const cached = cachedItemsByType.get(type)
  if (cached) return { type, items: cached }
  if (lastShownType) {
    const outgoing = cachedItemsByType.get(lastShownType)
    if (outgoing) return { type: lastShownType, items: outgoing }
  }
  return null
}

// Top anime page: MAL's rankings (not just my list) — one of seven
// selectable lists (see the selector row below), All by default. Only the
// flat-row tier (rank 11+) carries a list-action button, conditional — Add
// when the anime isn't in my list yet (adds it as Plan to watch and flips in
// place to Edit), Edit otherwise — which opens the same overlay used
// everywhere else in the app. Each list covers up to 500 rows, paginated
// client-side at 50/page. `page` and `type` live in the URL (`?type=…&page=N`)
// rather than as plain component state, so each page or list change is a real
// history entry — the browser's back/forward buttons, keyboard shortcuts, and
// trackpad swipe gesture step through the ranking's own pages and lists
// instead of leaving the page entirely on the first back.
export function TopAnimePage() {
  const [pendingId, setPendingId] = useState<number | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const { openEditor } = useEntryEditor()
  const reportFailure = useActionFailure()
  const [, forceRerender] = useReducer((n: number) => n + 1, 0)

  // Picks up a background refresh landing for the list already on screen —
  // see `cacheUpdateListeners` above.
  useEffect(() => {
    cacheUpdateListeners.add(forceRerender)
    return () => {
      cacheUpdateListeners.delete(forceRerender)
    }
  }, [])

  const typeParam = searchParams.get('type')
  const selectedType: TopAnimeRankingType = isRankingType(typeParam) ? typeParam : 'all'

  const pageParam = Number(searchParams.get('page'))
  const page = Number.isInteger(pageParam) && pageParam > 0 ? pageParam : 1

  // The cached-hit path is resolved synchronously from the module cache on
  // every render (not via an effect), so switching to an already-loaded list
  // renders it in the same paint — no one-frame flash of the muted state
  // while an effect catches up. `fallback` only matters for the miss path: it
  // holds whatever was last on screen so it can stay visible, muted, while an
  // uncached list's fetch is in flight.
  const [fallback, setFallback] = useState<Display | null>(() => initialFallback(selectedType))
  const cachedForSelected = cachedItemsByType.get(selectedType)
  const display: Display | null = cachedForSelected ? { type: selectedType, items: cachedForSelected } : fallback
  const loading = display === null
  const muted = display !== null && display.type !== selectedType
  const items = display?.items ?? []

  useEffect(() => {
    if (cachedForSelected) {
      lastShownType = selectedType
      return
    }

    // Not cached yet — leave whatever is currently on screen (the previous
    // list, muted) in place rather than blanking to "Loading…", per D7.
    let cancelled = false
    loadTopAnimeOnce(selectedType).then((result) => {
      if (cancelled) return
      lastShownType = selectedType
      setFallback({ type: selectedType, items: result })
    })

    return () => {
      cancelled = true
    }
  }, [selectedType, cachedForSelected])

  // Keeps every cached list in sync with optimistic updates from Add/Edit —
  // an anime added from one list must also flip to "Edit" in every other
  // cached list it appears in (design.md D8) — so a later list switch, page
  // navigation, or a full remount sees the change instead of reverting to the
  // last network response. Also recorded in `sessionEditsByAnimeId` so a
  // background refresh's re-read can't revert it either (design D4).
  function setEntry(animeId: number, entry: TopAnimeItemDto['entry']) {
    sessionEditsByAnimeId.set(animeId, entry)

    const patch = (list: TopAnimeItemDto[]) =>
      list.map((item) => (item.animeId === animeId ? { ...item, entry } : item))

    for (const [type, list] of cachedItemsByType) {
      cachedItemsByType.set(type, patch(list))
    }

    // Mutating the cache Map doesn't itself trigger a re-render; nudge
    // `fallback` so the component re-renders and the (possibly muted)
    // fallback list reflects the edit too, even when it isn't the type
    // driving `display` this render.
    setFallback((prev) => (prev ? { type: prev.type, items: patch(prev.items) } : prev))
  }

  const totalPages = Math.max(1, Math.ceil(items.length / PAGE_SIZE))
  const pageItems = items.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)
  // Ranks 1-3 and 4-10 only ever occur on page 1 (page size is 50), so
  // slicing by rank alone naturally scopes the showcase/card sections to
  // that page without checking `page` directly.
  const showcaseItems = pageItems.filter((item) => item.rank <= 3)
  const cardItems = pageItems.filter((item) => item.rank > 3 && item.rank <= 10)
  const restItems = pageItems.filter((item) => item.rank > 10)

  async function handleAdd(item: TopAnimeItemDto) {
    if (pendingId !== null) return
    setPendingId(item.animeId)
    try {
      const saved = await updateEntry(item.animeId, {})
      setEntry(item.animeId, saved)
    } catch (err) {
      // The button stays "Add", and the notice says why.
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(item.title, item.englishTitle)} to list`,
        reason: err instanceof ApiError ? err.reason : null,
      })
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

  // Selecting the list already shown is a no-op (design.md D5): no history
  // entry, no reload. Otherwise pushes a history entry — same mechanism as
  // goToPage above — and resets to page 1, since a page number from one list
  // has no relationship to the same page number of another.
  function selectType(type: TopAnimeRankingType) {
    if (type === selectedType) return
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (type === 'all') params.delete('type')
      else params.set('type', type)
      params.delete('page')
      return params
    })
  }

  function handleEdit(item: TopAnimeItemDto) {
    if (!item.entry) return
    openEditor({
      animeId: item.animeId,
      animeTitle: pickDisplayTitle(item.title, item.englishTitle),
      totalEpisodes: item.totalEpisodes,
      airingStatus: item.airingStatus,
      episodesAired: item.episodesAired,
      // TopAnimeItemDto (MAL's own ranking lists, unrelated to the
      // anime-ranking capability) doesn't carry a media type yet.
      mediaType: null,
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
      </div>

      <div className="top-anime-page__controls">
        <div className="top-anime-page__selector" role="group" aria-label="Ranking list">
          {TOP_ANIME_RANKING_TYPES.map(({ value, label }) => (
            <button
              key={value}
              type="button"
              className={
                value === selectedType
                  ? 'top-anime-page__selector-button top-anime-page__selector-button--active'
                  : 'top-anime-page__selector-button'
              }
              aria-pressed={value === selectedType}
              onClick={() => selectType(value)}
            >
              {label}
            </button>
          ))}
        </div>
        {items.length > 0 && (
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={goToPage} />
        )}
      </div>

      {loading ? (
        <p className="top-anime-page__loading">Loading…</p>
      ) : items.length === 0 ? (
        <p className="top-anime-page__empty">No ranking data yet.</p>
      ) : (
        <div
          className={muted ? 'top-anime-page__content top-anime-page__content--muted' : 'top-anime-page__content'}
        >
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
                    <span className="top-anime-rank top-anime-rank--lg">{item.rank}</span>
                    <Link to={`/anime/${item.animeId}`} className="top-anime-showcase__title-link">
                      <span
                        className="top-anime-showcase__title"
                        title={pickDisplayTitle(item.title, item.englishTitle)}
                      >
                        {pickDisplayTitle(item.title, item.englishTitle)}
                      </span>
                    </Link>
                    <div className="top-anime-showcase__scores">
                      <ScoreChip role="mal" label="MAL score">
                        <ScoreValue value={item.malScore} completed={isScoreRevealableStatus(item.entry?.status)} />
                      </ScoreChip>
                      <ScoreChip role="mine" label="My score">
                        {item.entry?.myScore ?? '—'}
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
                    <span className="top-anime-card__picture-frame">
                      {item.pictureUrl ? (
                        <img src={item.pictureUrl} alt="" className="top-anime-card__picture" />
                      ) : (
                        <div
                          className="top-anime-card__picture top-anime-card__picture--placeholder"
                          aria-hidden="true"
                        />
                      )}
                    </span>
                    <span className="top-anime-card__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
                      {pickDisplayTitle(item.title, item.englishTitle)}
                    </span>
                  </Link>
                  <span className="top-anime-card__scores">
                    <span className="score--mal">
                      <ScoreValue value={item.malScore} completed={isScoreRevealableStatus(item.entry?.status)} />
                    </span>
                    <span className="score--mine">{item.entry?.myScore ?? '—'}</span>
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
                    <RowPicture src={item.pictureUrl} className="top-anime-row__picture" />
                    <span className="top-anime-row__title" title={pickDisplayTitle(item.title, item.englishTitle)}>
                      {pickDisplayTitle(item.title, item.englishTitle)}
                    </span>
                  </Link>
                  <span className="top-anime-row__mal-score score--mal">
                    <ScoreValue value={item.malScore} completed={isScoreRevealableStatus(item.entry?.status)} />
                  </span>
                  <span className="top-anime-row__my-score score--mine">{item.entry?.myScore ?? '—'}</span>
                  {renderActionButton(item, 'top-anime-row__action')}
                </li>
              ))}
            </ol>
          )}
        </div>
      )}

      {items.length > 0 && (
        <div className="top-anime-page__footer">
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={goToPage} />
        </div>
      )}
    </div>
  )
}
