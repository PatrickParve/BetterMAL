import { useRef, useState, type KeyboardEvent } from 'react'
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import type { ThemeSearchResultDto } from '../api/types.ts'
import { useAnimeSearch } from '../hooks/useAnimeSearch.ts'
import { useThemeSearch } from '../hooks/useThemeSearch.ts'
import { pickDisplayTitle } from '../utils/anime.ts'
import { parseSearchMode, searchResultsUrl, withSearchMode, type SearchMode } from '../utils/searchMode.ts'
import { themeKey, themeSuggestionLine } from './AnimeThemes/themeFlags.ts'
import { AnimeSuggestionRows } from './AnimeSuggestionRows.tsx'
import { RowPicture } from './RowPicture.tsx'
import { SearchField } from './SearchField.tsx'
import './SearchPageBar.css'

const MODES: { value: SearchMode; label: string }[] = [
  { value: 'anime', label: 'Anime' },
  { value: 'op-ed', label: 'OP/ED' },
]

const PLACEHOLDERS: Record<SearchMode, string> = {
  anime: 'Search anime…',
  'op-ed': 'Search songs, artists or anime…',
}

const LABELS: Record<SearchMode, string> = {
  anime: 'Search anime',
  'op-ed': 'Search openings and endings',
}

// The search page's own bar (navigation-and-search "The search page has its own search
// bar", improve-search-page design.md D3, D4, D5, D10): a two-way switch between Anime and
// OP/ED, and the shared SearchField beside it.
//
// The mode and the submitted query live in the address (`?q=…&mode=op-ed`; no mode is
// Anime), so a reload, a link and Back restore both, and the bar always follows the
// address: when `q` or `mode` changes from outside it (Back, Forward, the navbar
// submitting an Anime search) the field takes the address's query and every dropdown
// closes, since a navigation, like a response arriving, never opens one. Switching mode
// writes `mode` to the address as a push, as the sort select does, so Back returns to the
// other mode, and keeps the text in the field; the page then shows the submitted query's
// results in the new mode with no further Enter. Only typing in the field or focusing it
// opens a dropdown.
//
// Both type-aheads are mounted, and only the active mode's is given the field's text:
// the other holds an empty query and so nothing to show. Switching can therefore never put
// one mode's suggestions under the other's field, and dismiss reaches both, so a mode that
// was left can't be left open.
export function SearchPageBar() {
  const navigate = useNavigate()
  const location = useLocation()
  const [searchParams, setSearchParams] = useSearchParams()
  const urlQuery = searchParams.get('q') ?? ''
  const mode = parseSearchMode(searchParams.get('mode'))

  const [query, setQuery] = useState(urlQuery)
  const animeSearch = useAnimeSearch(mode === 'anime' ? query : '')
  const themeSearch = useThemeSearch(mode === 'op-ed' ? query : '')
  const optionRefs = useRef<Partial<Record<SearchMode, HTMLButtonElement | null>>>({})

  function dismissAll() {
    animeSearch.dismiss()
    themeSearch.dismiss()
  }

  // Follows the address. Adjusted while rendering, like usePageData's key, so the field
  // never paints the previous query for a frame after a navigation.
  const [followed, setFollowed] = useState({ query: urlQuery, mode })
  if (followed.query !== urlQuery || followed.mode !== mode) {
    setFollowed({ query: urlQuery, mode })
    if (followed.query !== urlQuery) setQuery(urlQuery)
    dismissAll()
  }

  function chooseMode(next: SearchMode) {
    if (next === mode) return
    dismissAll()
    setSearchParams((previous) => withSearchMode(previous, next))
  }

  // Arrow keys move between the two and select as they go, as a radio group does.
  function handleModeKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    const step = event.key === 'ArrowRight' || event.key === 'ArrowDown' ? 1 : event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : 0
    if (step === 0) return
    event.preventDefault()
    const index = MODES.findIndex((option) => option.value === mode)
    const next = MODES[(index + step + MODES.length) % MODES.length]
    chooseMode(next.value)
    optionRefs.current[next.value]?.focus()
  }

  // The field has already dismissed its dropdown and given up focus. Submitting the very
  // address already shown replaces it, so a second Enter doesn't stack a duplicate entry
  // for Back to step through.
  function submit(submitted: string) {
    const url = searchResultsUrl(submitted, mode)
    navigate(url, { replace: url === location.pathname + location.search })
  }

  function goToAnime(animeId: number) {
    dismissAll()
    navigate(`/anime/${animeId}`)
  }

  function goToSeries(seriesId: number) {
    dismissAll()
    navigate(`/series/${seriesId}`)
  }

  // Router state, not the address: a reload or a shared link to the anime page stays clean,
  // and the page opens at the song only on this visit (anime-themes "The anime page can
  // open at one song").
  function goToSong(result: ThemeSearchResultDto) {
    dismissAll()
    navigate(`/anime/${result.animeId}`, { state: { themeId: result.theme.id } })
  }

  const active = mode === 'anime' ? animeSearch : themeSearch

  return (
    <div className="search-page-bar">
      <div className="search-mode-switch" role="radiogroup" aria-label="Search mode">
        {MODES.map((option) => (
          <button
            key={option.value}
            ref={(element) => {
              optionRefs.current[option.value] = element
            }}
            type="button"
            role="radio"
            aria-checked={mode === option.value}
            tabIndex={mode === option.value ? 0 : -1}
            className="search-mode-switch__option"
            onClick={() => chooseMode(option.value)}
            onKeyDown={handleModeKeyDown}
          >
            {option.label}
          </button>
        ))}
      </div>
      <SearchField
        className="search-bar--page"
        value={query}
        onValueChange={setQuery}
        placeholder={PLACEHOLDERS[mode]}
        label={LABELS[mode]}
        onSubmit={submit}
        onOpen={active.reopen}
        onDismiss={dismissAll}
        dropdownOpen={active.open && active.results.length > 0}
      >
        {mode === 'anime' ? (
          <AnimeSuggestionRows results={animeSearch.results} onPickAnime={goToAnime} onPickSeries={goToSeries} />
        ) : (
          <ThemeSuggestionRows results={themeSearch.results} onPick={goToSong} />
        )}
      </SearchField>
    </div>
  )
}

// The OP/ED dropdown's rows (theme-search "Typing in OP/ED mode suggests songs"): the
// anime's picture, the song title and, beneath it on one line, the label, the anime's
// title and the artists, in the fixed row height the anime rows have so the dropdown
// never changes height as I type or switch mode. The picture is always drawn, an empty
// box where the app has none, so titles line up whether or not a song's anime is saved.
// Songs only: a series never appears here.
function ThemeSuggestionRows({
  results,
  onPick,
}: {
  results: ThemeSearchResultDto[]
  onPick: (result: ThemeSearchResultDto) => void
}) {
  return (
    <>
      {results.map((result) => {
        const { theme } = result
        const animeTitle = pickDisplayTitle(result.title, result.englishTitle)
        return (
          <li key={themeKey(result.animeId, theme.id)}>
            <button type="button" className="search-bar__result" onClick={() => onPick(result)}>
              <RowPicture src={result.pictureUrl} className="search-bar__thumb" />
              <span className="search-bar__result-text">
                <span className="search-bar__result-title">
                  {theme.songTitle ? theme.songTitle : <span className="search-bar__unknown">Song title not known</span>}
                </span>
                <span className="search-bar__result-meta">{themeSuggestionLine(theme, animeTitle)}</span>
              </span>
            </button>
          </li>
        )
      })}
    </>
  )
}
