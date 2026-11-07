import { useEffect, useRef, useState } from 'react'
import { searchThemes } from '../api/client.ts'
import type { ThemeSearchResultDto } from '../api/types.ts'
import { useDebouncedValue } from './useDebouncedValue.ts'

// The anime type-ahead's local stage rate: the search reads memory, so it can be eager.
const DEBOUNCE_MS = 120

// The dropdown lists up to five songs (theme-search "Typing in OP/ED mode suggests songs").
const SUGGESTION_COUNT = 5

// The OP/ED type-ahead behind the search page's bar (improve-search-page design.md D10).
// It keeps useAnimeSearch's contract, so the page bar drives both the same way: a
// response fills the rows and only an interaction opens the dropdown. `open` is set true
// only by reopen() (typing, focusing) and false only by dismiss() (submit, Escape, a press
// outside), and no response ever touches it, so a late answer can't open a dropdown over
// the results page. Rows outlive a dismissal so refocusing is instant, but reopen() drops
// them unless they are for the text now in the field, so a dropdown never shows
// suggestions fetched for another query.
//
// There is one stage, not two: every suggestion comes from the app's stored catalogue, so
// there is no slower, fuller answer to merge in later and no later response replaces rows
// already shown for the text in the field. A request that a later keystroke or a dismissal
// supersedes is aborted rather than left to finish: a dismissed field issues no search
// and abandons the one it has out (navigation-and-search "Search submission keeps the
// query text").
//
// The page bar hands this hook the field's text only while OP/ED is the mode, and an
// empty string otherwise (SearchPageBar). The hook then holds nothing to show, so
// switching modes can't carry one mode's suggestions into the other.
export function useThemeSearch(query: string) {
  const [results, setResults] = useState<ThemeSearchResultDto[]>([])
  const [open, setOpen] = useState(false)
  const trimmedQuery = query.trim()
  const debouncedQuery = useDebouncedValue(trimmedQuery, DEBOUNCE_MS)
  // The query the rows on screen were fetched for, or null when there are none: what
  // reopen() compares the field's text to.
  const displayedQueryRef = useRef<string | null>(null)

  useEffect(() => {
    if (!open) return
    // The debounce is still catching up with the field (the text changed a moment ago,
    // say from the address, and the field was focused before it settled): wait for it
    // rather than fetch for the text the field held before. Typing runs this too, so
    // the request for the previous text is abandoned as soon as the next key lands.
    if (debouncedQuery !== trimmedQuery) return
    if (debouncedQuery.length === 0) {
      setResults([])
      displayedQueryRef.current = null
      return
    }

    const controller = new AbortController()
    searchThemes(debouncedQuery, SUGGESTION_COUNT, controller.signal)
      .then((response) => {
        setResults(response.results)
        displayedQueryRef.current = debouncedQuery
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === 'AbortError') return
        setResults([])
        displayedQueryRef.current = debouncedQuery
      })

    return () => controller.abort()
  }, [open, debouncedQuery, trimmedQuery])

  function dismiss() {
    setOpen(false)
  }

  function reopen() {
    if (displayedQueryRef.current !== trimmedQuery) {
      setResults([])
      displayedQueryRef.current = null
    }
    setOpen(true)
  }

  return { results, open, dismiss, reopen }
}
