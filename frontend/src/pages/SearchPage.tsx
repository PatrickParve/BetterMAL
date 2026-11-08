import { useEffect, useMemo, useRef } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getSearchPage, searchThemes } from '../api/client.ts'
import type { AnimeBrowseItemDto, SeriesSearchResultDto, ThemeSearchResultDto } from '../api/types.ts'
import { AnimeCard, AnimeCardMeta, BrowseCardScore } from '../components/AnimeCard.tsx'
import { ThemeResults } from '../components/AnimeThemes/ThemeResults.tsx'
import { FilterMultiSelect } from '../components/FilterMultiSelect.tsx'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { SearchPageBar } from '../components/SearchPageBar.tsx'
import { SeriesBadge } from '../components/SeriesBadge.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useCompleteLastRow } from '../hooks/useCompleteLastRow.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { useScrollReveal } from '../hooks/useScrollReveal.ts'
import { MEDIA_TYPE_FILTER_OPTIONS, mediaTypeFilterOptions } from '../utils/anime.ts'
import { parseSearchMode } from '../utils/searchMode.ts'
import './SearchPage.css'

type SortKey = 'relevance' | 'popularity' | 'malScore' | 'alphabetical' | 'myScore'

// What one read of the page holds, tagged with the mode that read it. The page keys its
// read by mode and query (`search:<mode>:<q>`), so each mode's results are their own
// restorable snapshot and a snapshot is only ever read back under the mode that made it.
type SearchReadState =
  | {
      mode: 'anime'
      items: AnimeBrowseItemDto[]
      totalCount: number
      series: SeriesSearchResultDto[]
      malSearchFailed: boolean
    }
  | {
      mode: 'op-ed'
      // Whether the openings-and-endings catalogue has ever finished a refresh: it tells
      // "not loaded yet" from "no songs found", which are both an empty list.
      ready: boolean
      songs: ThemeSearchResultDto[]
    }

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'relevance', label: 'Relevance' },
  { value: 'popularity', label: 'Popularity' },
  { value: 'malScore', label: 'MAL score' },
  { value: 'alphabetical', label: 'Alphabetical' },
  { value: 'myScore', label: 'My score' },
]

// How many revealed cards grow by per scroll-triggered reveal. Distinct from
// CANDIDATE_LIMIT below — this only controls how much of the already-loaded
// array is rendered at once.
const CHUNK_SIZE = 48

// The whole candidate set for a query is fetched once — the 60 highest-
// relevance matches, the one figure the live search, the endpoint's clamp,
// and this page all agree on (design.md D6) — so scrolling further just
// reveals more of what's already in memory instead of re-running the live
// MAL search per chunk.
const CANDIDATE_LIMIT = 60

// The most songs the OP/ED results list: the server's own cap for the endpoint. A broad
// query ("love" matches 315) shows the first 200, the way the anime results stop at
// their 60 candidates.
const THEME_RESULT_LIMIT = 200

function isSortKey(value: string | null): value is SortKey {
  return value !== null && SORT_OPTIONS.some((option) => option.value === value)
}

// Full search results page: fetches the whole (≤60) candidate set once per
// (query, sort) — the search endpoint has no cache behind it, so paging would
// re-run the live MAL search per chunk — and reveals it in chunks of
// CHUNK_SIZE via useScrollReveal's sentinel, the same continuous-scroll
// pattern as the season page. Query/sort live in the URL so back-navigation
// from an anime detail page restores exactly where the user left off,
// including how much of the result set had been revealed.
// A legacy `?page=N` link is simply ignored — the query itself still resolves.
//
// The page has two modes, held in the URL beside the query (`mode=op-ed`; none is
// Anime) and switched by its own bar in the header (SearchPageBar). Anime is all of the
// above. OP/ED lists the stored openings and endings matching the query as songs that
// play in place (ThemeResults), up to 200 of them, with no Type filter, sort or series
// cards, and its count counts songs. `sort` and `type` stay in the address in OP/ED mode,
// unused, so switching back restores them.
export function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()

  const q = searchParams.get('q') ?? ''
  const mode = parseSearchMode(searchParams.get('mode'))
  const sortParam = searchParams.get('sort')
  const sort = isSortKey(sortParam) ? sortParam : 'relevance'
  const typeParam = searchParams.get('type')
  // The ticked types. Absent (null), empty ('') and a list of nothing all read
  // as none ticked, which is no restriction: `?type=tv,movie` is the only
  // shape that narrows. A link made while an empty `type` still meant None
  // therefore shows everything. Memoised on the parameter so an unchanged
  // address hands the filter below the same array.
  const typeFilter = useMemo(() => (typeParam ?? '').split(',').filter(Boolean), [typeParam])

  // Keyed on the mode and the query: a different query is a different history
  // snapshot, so restoring one restores its results regardless of which sort
  // was active when it was left (D5), and each mode's results for a query are
  // their own snapshot. A sort change within the same query is handled below
  // via `reload`, not a second key.
  const { data, loading, failed, retry, reload } = usePageData<SearchReadState>(
    `search:${mode}:${q}`,
    (): Promise<SearchReadState> => {
      if (mode === 'op-ed') {
        return q.length === 0
          ? Promise.resolve({ mode, ready: true, songs: [] })
          : searchThemes(q, THEME_RESULT_LIMIT).then((result) => ({
              mode,
              ready: result.ready,
              songs: result.results,
            }))
      }
      return q.length === 0
        ? Promise.resolve({ mode, items: [], totalCount: 0, series: [], malSearchFailed: false })
        : getSearchPage(q, { sort, offset: 0, limit: CANDIDATE_LIMIT }).then((result) => ({
            mode,
            items: result.items,
            totalCount: result.totalCount,
            series: result.series,
            malSearchFailed: result.malSearchFailed,
          }))
    },
  )
  // The read's own mode decides which half is used, so a snapshot can never be shown
  // under the other mode's layout.
  const animeData = data?.mode === 'anime' ? data : null
  const themeData = data?.mode === 'op-ed' ? data : null
  const items = animeData?.items ?? []
  const totalCount = animeData?.totalCount ?? 0
  const series = animeData?.series ?? []
  const malSearchFailed = animeData?.malSearchFailed ?? false
  const songCount = themeData?.songs.length ?? 0
  const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', CHUNK_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const reloadRef = useRef(reload)
  reloadRef.current = reload

  function setSort(next: SortKey) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      params.set('sort', next)
      return params
    })
  }

  function setTypeFilter(next: string[]) {
    setSearchParams((prev) => {
      const params = new URLSearchParams(prev)
      if (next.length === 0) params.delete('type')
      else params.set('type', next.join(','))
      return params
    })
  }

  // Sort changed within the same query: the query's initial or restored load
  // is already covered by usePageData itself (above), so this only fires on
  // a later change, reusing `reload`'s own generation guard. Read through a
  // ref so a query change (which gives `reload` a new identity) doesn't also
  // retrigger this effect.
  const isFirstSortRun = useRef(true)
  useEffect(() => {
    if (isFirstSortRun.current) {
      isFirstSortRun.current = false
      return
    }
    reloadRef.current()
  }, [sort])

  // Type filter options: only the media types actually present in the loaded
  // candidate set, mirroring MyListPage's presentTypes/hasUnknownType (D6).
  const typeOptions = useMemo(() => mediaTypeFilterOptions(items.map((item) => item.mediaType)), [items])

  const filteredItems = useMemo(
    () => (typeFilter.length === 0 ? items : items.filter((item) => typeFilter.includes(item.mediaType ?? 'unknown'))),
    [items, typeFilter],
  )

  // Reveal more of the already-loaded array once the sentinel enters view —
  // no network call, everything for this (query, sort) is already in memory.
  useScrollReveal(sentinelRef, filteredItems.length, CHUNK_SIZE, setVisibleCount)

  const visibleItems = filteredItems.slice(0, visibleCount)
  // Tops the reveal up so its last row is never left with a lone card
  // (below the desktop breakpoint the auto-fill column count needn't divide the step).
  const gridRef = useCompleteLastRow(filteredItems, visibleCount, setVisibleCount)

  return (
    <div className="search-page">
      {/* The header's three parts, in the order they read: the title, the page's own search
          bar, and the count, Type filter and sort. SearchPage.css keeps the bar centred on
          the page, on one line with the other two where the window is wide enough. */}
      <div className="search-page__header">
        <h1>{q.length > 0 ? <>Results for “{q}”</> : 'Search'}</h1>
        <SearchPageBar />
        <div className="search-page__controls">
          {mode === 'anime' ? (
            <>
              {totalCount > 0 && (
                <span className="search-page__count">{filteredItems.length + series.length} results</span>
              )}
              <FilterMultiSelect
                label="Type"
                options={typeOptions}
                widthOptions={MEDIA_TYPE_FILTER_OPTIONS}
                selected={typeFilter}
                onChange={setTypeFilter}
              />
              <select
                className="search-page__sort"
                value={sort}
                onChange={(event) => setSort(event.target.value as SortKey)}
                aria-label="Sort search results"
              >
                {SORT_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </>
          ) : (
            // The Type filter and the sort belong to the anime results; the count
            // stays, and counts the songs listed.
            songCount > 0 && (
              <span className="search-page__count">
                {songCount} {songCount === 1 ? 'song' : 'songs'}
              </span>
            )
          )}
        </div>
      </div>

      {malSearchFailed && (
        <p className="search-page__fallback-notice" role="status">
          The MAL search couldn't be reached — these are the app's locally stored matches.
        </p>
      )}

      {/* Empty and no-match messages come only from a settled read: a read
          still in flight shows nothing here (the loading notice below), and a
          read that failed is a failure, not "No anime found." */}
      {q.length === 0 ? (
        <p className="search-page__empty">Enter a search term to begin.</p>
      ) : failed ? (
        <LoadFailedNotice what="the search results" onRetry={retry} />
      ) : data === null ? null : mode === 'op-ed' ? (
        // The OP/ED notes, in the page's own note style. Both come from a settled
        // read, as the anime ones do: a read still in flight shows nothing here, and
        // a read that failed is the failure notice above, never "No songs found."
        themeData === null ? null : !themeData.ready ? (
          <p className="search-page__empty">Openings and endings haven't loaded yet.</p>
        ) : themeData.songs.length === 0 ? (
          <p className="search-page__empty" role="status">
            No songs found.
          </p>
        ) : (
          // Keyed by the query: a new search replaces the list, which stops a playing song.
          <ThemeResults key={q} songs={themeData.songs} />
        )
      ) : items.length === 0 && series.length === 0 ? (
        // A failed-search empty result is explained by the notice above
        // instead of the ordinary "not found" message, so it reads as
        // "nothing stored here matches" rather than "this does not exist".
        malSearchFailed ? null : (
          <p className="search-page__empty">No anime found.</p>
        )
      ) : (
        <>
          <div ref={gridRef} className="search-page__grid">
            {series.map((s) => (
              <AnimeCard
                key={`series-${s.seriesId}`}
                animeId={s.seriesId}
                title={s.title}
                englishTitle={s.englishTitle}
                pictureUrl={s.pictureUrl}
                to={`/series/${s.seriesId}`}
                className="anime-card--fluid"
              >
                {/* The badge line, ending in the series' main-line MAL average where an
                    anime card's meta line ends in its score, or a dash when no main-line
                    entry has one. Hidden, the slot is simply not drawn (BrowseCardScore),
                    and the badge stays where it is. */}
                <span className="anime-card__meta">
                  <SeriesBadge entryCount={s.entryCount} />
                  <BrowseCardScore malScore={s.malScore} showMissing />
                </span>
              </AnimeCard>
            ))}
            {visibleItems.map((item) => (
              <AnimeCard
                key={item.animeId}
                animeId={item.animeId}
                title={item.title}
                englishTitle={item.englishTitle}
                pictureUrl={item.pictureUrl}
                className="anime-card--fluid"
              >
                <AnimeCardMeta
                  mediaType={item.mediaType}
                  totalEpisodes={item.totalEpisodes}
                  malScore={item.malScore}
                  showMissingScore
                />
              </AnimeCard>
            ))}
          </div>
          {/* Distinct from "No anime found." above: the query matched anime,
              the type filter just leaves none of them standing. Series cards
              aren't subject to the type filter, so they keep rendering
              alongside this rather than being hidden by it. */}
          {items.length > 0 && filteredItems.length === 0 && (
            <p className="search-page__empty">No anime match the current filters.</p>
          )}
        </>
      )}

      <div ref={sentinelRef} className="search-page__sentinel" />
      <LoadingNotice active={loading} className="search-page__loading" />
    </div>
  )
}
