import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { AnimeSuggestionRows } from './AnimeSuggestionRows.tsx'
import { SearchField } from './SearchField.tsx'

// Centered navbar type-ahead: debounced. Ranking (local cache merged with live
// MAL, prefix matches first, ordered by popularity, `"…"` for exact) is all
// handled server-side by GET /api/anime/search; this component just debounces
// and renders the top matches. Enter (or the magnifier button) submits to the
// full /search results page, which is an Anime search whatever mode that page's
// own bar was in.
//
// The field is anime-only and empties itself after a submit, as it already does
// after a pick (improve-search-page design.md D5, navigation-and-search "Search
// submission keeps the query text"): the search page's own bar shows the submitted
// query, so two fields never hold the same text, and an OP/ED query is never
// mirrored into an anime field. For the same reason it no longer reads `?q=` from
// the URL.
export function SearchBar() {
  const [query, setQuery] = useState('')
  const navigate = useNavigate()
  const { results, open, dismiss, reopen } = useAnimeSearch(query)

  function goToAnime(id: number) {
    dismiss()
    setQuery('')
    navigate(`/anime/${id}`)
  }

  function goToSeries(seriesId: number) {
    dismiss()
    setQuery('')
    navigate(`/series/${seriesId}`)
  }

  // The field has already dismissed its dropdown and given up focus.
  function submitSearch(submitted: string) {
    setQuery('')
    navigate(`/search?q=${encodeURIComponent(submitted)}`)
  }

  return (
    <SearchField
      value={query}
      onValueChange={setQuery}
      placeholder="Search anime…"
      label="Search anime"
      onSubmit={submitSearch}
      onOpen={reopen}
      onDismiss={dismiss}
      dropdownOpen={open && results.length > 0}
    >
      <AnimeSuggestionRows results={results} onPickAnime={goToAnime} onPickSeries={goToSeries} />
    </SearchField>
  )
}
