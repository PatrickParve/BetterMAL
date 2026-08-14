import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { SeriesBadge } from './SeriesBadge.tsx'
import './SearchBar.css'

// Centered navbar type-ahead: debounced. Ranking (local cache merged with live
// MAL, prefix matches first, ordered by popularity, `"…"` for exact) is all
// handled server-side by GET /api/anime/search; this component just debounces
// and renders the top matches. Enter (or the magnifier button) submits to the
// full /search results page.
export function SearchBar() {
  const [query, setQuery] = useState('')
  const containerRef = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const { results, open, setOpen } = useAnimeSearch(query)

  useClickOutside(containerRef, () => setOpen(false))

  // Keep the input in sync with the URL while on the search page itself, so a
  // reload or a direct link (e.g. /search?q=foo) shows the term that's live.
  const urlQuery = searchParams.get('q') ?? ''
  useEffect(() => {
    if (location.pathname === '/search') setQuery(urlQuery)
  }, [location.pathname, urlQuery])

  function goToAnime(id: number) {
    setOpen(false)
    setQuery('')
    navigate(`/anime/${id}`)
  }

  function goToSeries(rootAnimeId: number) {
    setOpen(false)
    setQuery('')
    navigate(`/series/${rootAnimeId}`)
  }

  function submitSearch() {
    const q = query.trim()
    if (q.length === 0) return
    setOpen(false)
    navigate(`/search?q=${encodeURIComponent(q)}`)
  }

  return (
    <div className="search-bar" ref={containerRef}>
      <input
        type="search"
        className="search-bar__input"
        placeholder="Search anime…"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        onFocus={() => results.length > 0 && setOpen(true)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') submitSearch()
          if (event.key === 'Escape') setOpen(false)
        }}
        aria-label="Search anime"
      />
      <button type="button" className="search-bar__submit" aria-label="Search" onClick={submitSearch}>
        <SearchIcon />
      </button>
      {open && results.length > 0 && (
        <ul className="search-bar__dropdown">
          {results.map((result) =>
            result.kind === 'series' ? (
              <li key={`series-${result.id}`}>
                <button
                  type="button"
                  className="search-bar__result search-bar__result--series"
                  onClick={() => goToSeries(result.rootAnimeId)}
                >
                  {result.pictureUrl && <img src={result.pictureUrl} alt="" className="search-bar__thumb" />}
                  <span className="search-bar__result-text">
                    <span className="search-bar__result-title">{pickDisplayTitle(result.title, result.englishTitle)}</span>
                    <SeriesBadge entryCount={result.entryCount} />
                  </span>
                </button>
              </li>
            ) : (
              <li key={`anime-${result.id}`}>
                <button type="button" className="search-bar__result" onClick={() => goToAnime(result.id)}>
                  {result.pictureUrl && <img src={result.pictureUrl} alt="" className="search-bar__thumb" />}
                  <span>{pickDisplayTitle(result.title, result.englishTitle)}</span>
                </button>
              </li>
            ),
          )}
        </ul>
      )}
    </div>
  )
}

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <line x1="15.5" y1="15.5" x2="21" y2="21" />
    </svg>
  )
}
