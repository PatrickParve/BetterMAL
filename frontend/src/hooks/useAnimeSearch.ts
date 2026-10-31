import { useEffect, useRef, useState } from 'react'
import { searchAnime } from '../api/client.ts'
import type { AnimeSearchResult } from '../api/types.ts'
import { useDebouncedValue } from './useDebouncedValue.ts'

const LOCAL_DEBOUNCE_MS = 120
const LIVE_DEBOUNCE_MS = 300

// Two-stage debounced type-ahead search backing the navbar SearchBar, the
// settings-page anime-refresh picker and Guess the Anime's field. The options
// are the last one's (add-guess-the-anime design.md D7): `series: false` leaves
// series rows out and `limit` asks for more than five, and both are off by
// default so the other two callers' requests are exactly what they were.
//
// A response fills the rows; an interaction opens the dropdown. `open` is a
// plain boolean that only reopen() (typing, focusing) sets true and only
// dismiss() (submit, Escape, click-outside) sets false — no response ever
// touches it. The old guard tried to infer "was this dismissed" from the
// *debounced* query a dismissal recorded, but pressing Enter before the
// debounce fires means that recorded query is a stale prefix of the one the
// response eventually comes back for, so no key derived from the debounced
// value can ever be the right one — the debounce is the thing that is
// behind, not the dismissal.
//
// Both stages debounce off the same trimmed query, at different rates: 120ms
// for the cache-only local stage (a ~10ms Postgres read can afford to be
// eager), 300ms for the live merged one (a miss costs a slot in a
// one-per-second MAL queue). 120 < 300, so for any settled query the local
// value always commits first and the live one second, never the reverse —
// the local stage is never behind the live stage. That invariant is what
// keeps the merge rule below to one line, with no sequence counters: a local
// response is always accepted, and a live response is accepted only if the
// query it was issued for still matches the one the local stage last
// committed.
export function useAnimeSearch(query: string, options?: { series?: boolean; limit?: number }) {
  const [results, setResults] = useState<AnimeSearchResult[]>([])
  const [open, setOpen] = useState(false)
  // The query the rows on screen were fetched for, mirrored from the ref below so
  // a render can tell whether they belong to what's in the field now.
  const [displayedQuery, setDisplayedQuery] = useState<string | null>(null)
  const series = options?.series
  const limit = options?.limit
  const trimmedQuery = query.trim()
  const localQuery = useDebouncedValue(trimmedQuery, LOCAL_DEBOUNCE_MS)
  const liveQuery = useDebouncedValue(trimmedQuery, LIVE_DEBOUNCE_MS)
  // The query the rows currently on screen were fetched for, or null when
  // there are none. Used both to accept/reject a live response (above) and,
  // in reopen(), to tell whether the held rows are still for the query now
  // in the field.
  const displayedQueryRef = useRef<string | null>(null)

  // The cache-only stage. Also owns clearing the rows once the field empties
  // out, since it is the first stage to see that happen.
  useEffect(() => {
    if (!open) return
    if (localQuery.length === 0) {
      setResults([])
      displayedQueryRef.current = null
      setDisplayedQuery(null)
      return
    }

    const controller = new AbortController()
    searchAnime(localQuery, 'local', controller.signal, { series, limit })
      .then((matches) => {
        setResults(matches)
        displayedQueryRef.current = localQuery
        setDisplayedQuery(localQuery)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setResults([])
        displayedQueryRef.current = localQuery
        setDisplayedQuery(localQuery)
      })

    return () => controller.abort()
  }, [open, localQuery, series, limit])

  // The merged live stage. Only ever replaces what is on screen — it never
  // clears it — so a live failure or a superseded response just leaves the
  // local-stage rows (or nothing) in place.
  useEffect(() => {
    if (!open || liveQuery.length === 0) return

    const controller = new AbortController()
    searchAnime(liveQuery, 'full', controller.signal, { series, limit })
      .then((matches) => {
        if (displayedQueryRef.current !== liveQuery) return // local has moved on; discard
        setResults(matches)
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
      })

    return () => controller.abort()
  }, [open, liveQuery, series, limit])

  function dismiss() {
    setOpen(false)
  }

  function reopen() {
    // Rows outlive a dismissal so refocusing is instant, but only when they
    // are still for what is in the field — the search page writing `?q=`
    // into the input is exactly the case where they might not be.
    if (displayedQueryRef.current !== trimmedQuery) {
      setResults([])
      displayedQueryRef.current = null
      setDisplayedQuery(null)
    }
    setOpen(true)
  }

  // True when the rows on screen are for what is in the field: not for a query
  // typed since, and not for none at all. Guess the Anime's Enter reads it, so a
  // fast typist can't guess the previous word's top hit (design.md D16).
  const rowsAreCurrent = trimmedQuery.length > 0 && displayedQuery === trimmedQuery

  return { results, open, dismiss, reopen, rowsAreCurrent }
}
