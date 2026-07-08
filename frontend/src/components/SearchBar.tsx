import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { searchAnime } from '../api/client.ts'
import type { AnimeSearchResult } from '../api/types.ts'
import { useDebouncedValue } from '../hooks/useDebouncedValue.ts'
import { useClickOutside } from '../hooks/useClickOutside.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import './SearchBar.css'

// Centered navbar type-ahead: debounced, local-cache-first with word-boundary
// prefix matching, live-fallback for uncached titles — all handled server-side
// by GET /api/anime/search; this component just debounces and renders results.
export function SearchBar() {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<AnimeSearchResult[]>([])
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const debouncedQuery = useDebouncedValue(query.trim(), 250)

  useClickOutside(containerRef, () => setOpen(false))

  useEffect(() => {
    if (debouncedQuery.length === 0) {
      setResults([])
      setOpen(false)
      return
    }

    const controller = new AbortController()
    searchAnime(debouncedQuery, controller.signal)
      .then((matches) => {
        setResults(matches)
        setOpen(true)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setResults([])
      })

    return () => controller.abort()
  }, [debouncedQuery])

  function goToAnime(id: number) {
    setOpen(false)
    setQuery('')
    navigate(`/anime/${id}`)
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
          if (event.key === 'Escape') setOpen(false)
        }}
        aria-label="Search anime"
      />
      {open && results.length > 0 && (
        <ul className="search-bar__dropdown">
          {results.map((result) => (
            <li key={result.id}>
              <button type="button" className="search-bar__result" onClick={() => goToAnime(result.id)}>
                {result.pictureUrl && <img src={result.pictureUrl} alt="" className="search-bar__thumb" />}
                <span>{pickDisplayTitle(result.title, result.englishTitle)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
