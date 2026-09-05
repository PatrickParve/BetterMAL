import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { RowPicture } from './RowPicture.tsx'
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
  const inputRef = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams] = useSearchParams()
  const { results, open, dismiss, reopen } = useAnimeSearch(query)

  useClickOutside(containerRef, dismiss)

  // Keep the input in sync with the URL while on the search page itself, so a
  // reload or a direct link (e.g. /search?q=foo) shows the term that's live.
  const urlQuery = searchParams.get('q') ?? ''
  useEffect(() => {
    if (location.pathname === '/search') setQuery(urlQuery)
  }, [location.pathname, urlQuery])

  function goToAnime(id: number) {
    dismiss()
    setQuery('')
    navigate(`/anime/${id}`)
  }

  function goToSeries(rootAnimeId: number) {
    dismiss()
    setQuery('')
    navigate(`/series/${rootAnimeId}`)
  }

  function submitSearch() {
    const q = query.trim()
    if (q.length === 0) return
    dismiss()
    inputRef.current?.blur()
    navigate(`/search?q=${encodeURIComponent(q)}`)
  }

  return (
    <div className="search-bar" ref={containerRef}>
      <input
        ref={inputRef}
        type="search"
        className="search-bar__input"
        placeholder="Search anime…"
        value={query}
        onChange={(event) => {
          setQuery(event.target.value)
          reopen()
        }}
        onFocus={reopen}
        onKeyDown={(event) => {
          if (event.key === 'Enter') submitSearch()
          if (event.key === 'Escape') dismiss()
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
                  {result.pictureUrl && <RowPicture src={result.pictureUrl} className="search-bar__thumb" />}
                  <span className="search-bar__result-text">
                    <span className="search-bar__result-title">{pickDisplayTitle(result.title, result.englishTitle)}</span>
                    <SeriesBadge entryCount={result.entryCount} />
                  </span>
                </button>
              </li>
            ) : (
              <li key={`anime-${result.id}`}>
                <button type="button" className="search-bar__result" onClick={() => goToAnime(result.id)}>
                  {result.pictureUrl && <RowPicture src={result.pictureUrl} className="search-bar__thumb" />}
                  <span className="search-bar__result-title search-bar__result-title--multiline">
                    {pickDisplayTitle(result.title, result.englishTitle)}
                  </span>
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
