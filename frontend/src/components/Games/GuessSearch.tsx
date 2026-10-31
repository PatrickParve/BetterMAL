import { useId, useImperativeHandle, useLayoutEffect, useRef, useState, type KeyboardEvent, type Ref } from 'react'
import type { AnimeSearchResult } from '../../api/types.ts'
import { useAnimeSearch } from '../../hooks/useAnimeSearch.ts'
import { useClickOutside } from '../../hooks/useClickOutside.ts'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { RowPicture } from '../RowPicture.tsx'
import '../SearchBar.css'
import './GuessSearch.css'

export type GuessSearchPick = Extract<AnimeSearchResult, { kind: 'anime' }>

export type GuessSearchHandle = {
  focus: () => void
  /** Puts back text a failed guess took out of the field, and opens the list for it. */
  restore: (text: string) => void
}

type GuessSearchProps = {
  ref?: Ref<GuessSearchHandle>
  /** The anime already guessed in this game, which the list leaves out. */
  guessedIds: ReadonlySet<number>
  /** A guess is on its way: the field keeps focus but takes no more text. */
  sending: boolean
  /** `typed` is what was in the field, for a guess that can't go ahead to put back. */
  onGuess: (pick: GuessSearchPick, typed: string) => void
}

// As many anime as the search will give (the server's most is 60), in a list that
// shows five rows and scrolls for the rest (design.md D7). What I've already
// guessed is dropped from it in the browser.
const ASKED_FOR = 60

// Guess the Anime's search field (design.md D7, D16): the navbar's search, through
// the same hook, endpoint and look, as a combobox whose Enter makes the highlighted
// anime my guess instead of opening a results page. The first row is highlighted,
// so a title and Enter is the fast path; the arrow keys move, and carry the list
// along when the highlight leaves the five rows showing; Escape closes. Enter
// does nothing while the rows on screen are for an earlier query, or a fast typist
// would guess the previous word's top hit. While a guess is on its way the field
// is read-only rather than disabled, so it keeps focus.
export function GuessSearch({ ref, guessedIds, sending, onGuess }: GuessSearchProps) {
  const [query, setQuery] = useState('')
  const [highlight, setHighlight] = useState(0)
  const containerRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)
  // Set by the arrow keys, so the list follows a highlight they moved and not one a
  // pointer resting over a row set while the wheel scrolled under it.
  const followRef = useRef(false)
  const listId = useId()
  const { results, open, dismiss, reopen, rowsAreCurrent } = useAnimeSearch(query, { series: false, limit: ASKED_FOR })

  useClickOutside(containerRef, dismiss)

  useImperativeHandle(ref, () => ({
    focus: () => inputRef.current?.focus({ preventScroll: true }),
    restore: (text) => {
      setQuery(text)
      reopen()
      inputRef.current?.focus({ preventScroll: true })
    },
  }))

  const rows = results.filter((result): result is GuessSearchPick => result.kind === 'anime' && !guessedIds.has(result.id))
  const showList = open && rows.length > 0

  // The highlight goes back to the first row whenever the rows change: the
  // merged stage replaces the stored matches under a pointer or a key press.
  const rowsKey = rows.map((row) => row.id).join(',')
  const [highlightedFor, setHighlightedFor] = useState(rowsKey)
  if (highlightedFor !== rowsKey) {
    setHighlightedFor(rowsKey)
    setHighlight(0)
  }
  const active = Math.min(highlight, rows.length - 1)

  // New rows start at the top of the list, as the highlight does.
  useLayoutEffect(() => {
    if (listRef.current) listRef.current.scrollTop = 0
  }, [rowsKey])

  // A key that moves the highlight past the rows showing scrolls the list by just
  // enough to bring it in, by the list's own offset rather than scrollIntoView,
  // which would move anything above it that scrolls too.
  useLayoutEffect(() => {
    const list = listRef.current
    const row = list?.children[active] as HTMLElement | undefined
    if (!list || !row || !followRef.current) return
    followRef.current = false
    const edge = 4 // the list's own padding
    if (row.offsetTop < list.scrollTop + edge) list.scrollTop = row.offsetTop - edge
    else if (row.offsetTop + row.offsetHeight > list.scrollTop + list.clientHeight - edge)
      list.scrollTop = row.offsetTop + row.offsetHeight - list.clientHeight + edge
  }, [active])

  function pick(row: GuessSearchPick) {
    const typed = query
    setQuery('')
    dismiss()
    onGuess(row, typed)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.nativeEvent.isComposing) return
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      if (!showList) {
        reopen()
        return
      }
      const step = event.key === 'ArrowDown' ? 1 : -1
      followRef.current = true
      setHighlight((active + step + rows.length) % rows.length)
    } else if (event.key === 'Enter') {
      event.preventDefault()
      if (sending || !showList || !rowsAreCurrent) return
      pick(rows[active])
    } else if (event.key === 'Escape') {
      // Only closes the list: a search field's own Escape would also clear what I typed.
      event.preventDefault()
      dismiss()
    }
  }

  return (
    <div className="search-bar guess-search" ref={containerRef}>
      <input
        ref={inputRef}
        type="search"
        role="combobox"
        className="search-bar__input"
        placeholder="Guess an anime…"
        aria-label="Guess an anime"
        aria-autocomplete="list"
        aria-expanded={showList}
        aria-controls={listId}
        aria-activedescendant={showList ? `${listId}-${active}` : undefined}
        autoComplete="off"
        enterKeyHint="go"
        readOnly={sending}
        value={query}
        onChange={(event) => {
          setQuery(event.target.value)
          reopen()
        }}
        onFocus={reopen}
        onKeyDown={handleKeyDown}
      />
      <ul ref={listRef} id={listId} role="listbox" aria-label="Matching anime" className="search-bar__dropdown" hidden={!showList}>
        {showList &&
          rows.map((row, index) => (
            <li
              key={row.id}
              id={`${listId}-${index}`}
              role="option"
              aria-selected={index === active}
              className={'search-bar__result guess-search__option' + (index === active ? ' guess-search__option--active' : '')}
              // The field keeps focus through a click, so nothing blurs and closes.
              onMouseDown={(event) => event.preventDefault()}
              onMouseMove={() => setHighlight(index)}
              onClick={() => pick(row)}
            >
              {row.pictureUrl && <RowPicture src={row.pictureUrl} className="search-bar__thumb" />}
              <span className="search-bar__result-title search-bar__result-title--multiline">
                {pickDisplayTitle(row.title, row.englishTitle)}
              </span>
            </li>
          ))}
      </ul>
    </div>
  )
}
