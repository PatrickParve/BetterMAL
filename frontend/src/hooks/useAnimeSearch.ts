import { useEffect, useRef, useState } from 'react'
import { searchAnime } from '../api/client.ts'
import type { AnimeSearchResult } from '../api/types.ts'
import { useDebouncedValue } from './useDebouncedValue.ts'

// Debounced type-ahead search backing both the navbar SearchBar and the
// settings-page anime-refresh picker: trims/debounces the query, fires
// GET /api/anime/search, and aborts a stale request if the query changes
// before it resolves.
export function useAnimeSearch(query: string, debounceMs = 250) {
  const [results, setResults] = useState<AnimeSearchResult[]>([])
  const [open, setOpen] = useState(false)
  const debouncedQuery = useDebouncedValue(query.trim(), debounceMs)
  // The query a caller last dismissed the dropdown for (submit, Escape, or
  // click-outside), so a response already in flight at that moment can't
  // reopen it. Scoped to that one query, not sticky: typing changes the
  // debounced query, so the next response opens normally.
  const dismissedQueryRef = useRef<string | null>(null)

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
        if (dismissedQueryRef.current !== debouncedQuery) setOpen(true)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setResults([])
      })

    return () => controller.abort()
  }, [debouncedQuery])

  function dismiss() {
    dismissedQueryRef.current = debouncedQuery
    setOpen(false)
  }

  function reopen() {
    setOpen(true)
  }

  return { results, open, dismiss, reopen }
}
