import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { AnimeThemeDto } from '../../api/types.ts'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { SCROLL_KEYS, type OpenAt } from '../../hooks/useOpenAtSong.ts'
import { scrollWindowTo } from '../../state/navbarReveal.ts'
import { ThemeRow } from './ThemeRow.tsx'
import { themeKey } from './themeFlags.ts'
import { useThemesView } from './useThemesView.ts'
import './AnimeThemesSection.css'

/**
 * The themes of one anime. On a series page its `title` heads a group that opens and
 * closes; the heading is not a link, since the Main series cards above reach the anime's
 * page. Without a title (the anime page) the list stands alone.
 */
export type ThemeGroup = {
  animeId: number
  title: string | null
  themes: AnimeThemeDto[]
}

type AnimeThemesSectionProps = {
  groups: ThemeGroup[]
  /** Whether a catalogue refresh has ever finished: it tells "none listed" from "not loaded yet". */
  ready: boolean
  /**
   * What the quiet line says it has no openings or endings for. `series` also turns on
   * the grouped view: groups that open and close, an expand/collapse-all control and a
   * song search. `anime` is one open list.
   */
  subject: 'anime' | 'series'
  /**
   * The song this visit opened the page at, which the section brings into view and starts
   * (anime-themes "The anime page can open at one song"). Only the anime page passes it,
   * and only for a visit that picked a song in the search page's OP/ED dropdown.
   */
  openAt?: OpenAt | null
}

// How far below the top of the window the row of a song opened at lands. The navbar is
// hidden by then, so the row can sit near the edge; its player is beneath it.
const OPEN_AT_TOP_GAP_PX = 16

// How long after the move the page goes on following the row while its player takes its size.
const OPEN_AT_FOLLOW_MS = 3000

// Brings `row` to OPEN_AT_TOP_GAP_PX below the top of the window, and keeps it there while the
// page takes its final height. A video is 150px tall until its metadata has loaded and 360px
// after, so the page is taller a moment after the move was aimed than when it was, and a row
// near the end of the page was clamped to the shorter page: the move stopped short, with the
// lower part of its player and its controls below the window (11/7, real pages, Chrome). So
// whenever the page has grown enough to let the move go further, it goes further, aimed from
// the row's place now. It stops for good at the first wheel, touch, press or page-moving key
// (that is me taking over, as in useOpenAtSong) and after OPEN_AT_FOLLOW_MS. Returns what
// stops it.
function bringRowIntoView(row: HTMLElement): () => void {
  const smooth = !window.matchMedia('(prefers-reduced-motion: reduce)').matches
  const target = () => Math.max(0, row.getBoundingClientRect().top + window.scrollY - OPEN_AT_TOP_GAP_PX)
  // Where the window can go right now: the target, or the end of the page if that is nearer.
  const reachable = (top: number) => Math.min(top, document.documentElement.scrollHeight - window.innerHeight)

  let top = target()
  let aimedAt = reachable(top)
  scrollWindowTo(top, { navbar: 'hide', smooth })

  const observer = new ResizeObserver(() => {
    top = target()
    const next = reachable(top)
    if (next - aimedAt < 1) return
    aimedAt = next
    scrollWindowTo(top, { navbar: 'hide', smooth })
  })
  observer.observe(document.documentElement)

  function onKeyDown(event: KeyboardEvent) {
    if (SCROLL_KEYS.has(event.key)) stop()
  }
  const timer = window.setTimeout(() => stop(), OPEN_AT_FOLLOW_MS)
  function stop() {
    observer.disconnect()
    window.clearTimeout(timer)
    window.removeEventListener('wheel', stop)
    window.removeEventListener('touchstart', stop)
    window.removeEventListener('pointerdown', stop)
    window.removeEventListener('keydown', onKeyDown)
  }
  window.addEventListener('wheel', stop, { passive: true })
  window.addEventListener('touchstart', stop, { passive: true })
  window.addEventListener('pointerdown', stop)
  window.addEventListener('keydown', onKeyDown)
  return stop
}

// The magnifier of My List's find field, copied so that component stays untouched.
function SearchGlyph() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <circle cx="10.5" cy="10.5" r="6.5" />
      <line x1="15.5" y1="15.5" x2="21" y2="21" />
    </svg>
  )
}

// The openings and endings of an anime, or of a series' main line group by group
// (anime-themes "The themes section lists openings and endings"; series-page "The
// series page lists the main line's openings and endings" and "...can be searched").
// One section serves both pages and holds the one `playing` theme, so starting another
// row's player closes the first, across every group. A theme stops when its row stops
// being shown, whether its group closed, a search no longer matches it, or a route
// switch dropped its group: a song with no row on screen has no control left to stop it.
// Nothing plays until a row's control is pressed. With nothing to list it is one quiet
// line, no retry, saying "not loaded yet" until a first refresh has finished. The section
// carries no credit and no link to AnimeThemes: Settings credits it (anime-themes
// "AnimeThemes is credited in Settings").
//
// The anime page is one open list. The series page turns on the grouped view
// (useThemesView): every group starts closed and opens from its heading, an
// expand/collapse-all control sits at the top right, and a search field keeps the songs
// that match.
export function AnimeThemesSection({ groups, ready, subject, openAt }: AnimeThemesSectionProps) {
  const { hideHentai } = useContentFilter()
  const [playing, setPlaying] = useState<string | null>(null)
  const searchRef = useRef<HTMLInputElement>(null)
  const sectionRef = useRef<HTMLElement>(null)
  // The visit whose song has been dealt with, so a visit opens at its song once however
  // often this renders, and a song the visit never found isn't looked for again.
  const openedKeyRef = useRef<string | null>(null)
  // The theme whose row is to be brought into view once its player is on the page.
  const [scrollToTheme, setScrollToTheme] = useState<number | null>(null)
  // Ends the page following the opened row (bringRowIntoView), for a new visit or on leaving.
  const stopFollowingRef = useRef<(() => void) | null>(null)

  const grouped = subject === 'series'
  const listed = groups.filter((group) => group.themes.length > 0)
  const view = useThemesView(listed, grouped)
  // The control and the search belong to a box that lists something.
  const hasControls = grouped && listed.length > 0

  // A theme whose row isn't shown stops: the rows on screen are the ones that hold the player.
  if (
    playing !== null &&
    !view.groups.some((entry) => entry.rows.some((theme) => themeKey(entry.group.animeId, theme.id) === playing))
  ) {
    setPlaying(null)
  }

  // Opens at one song (anime-themes "The anime page can open at one song"): once, on the
  // render that first shows the rows, it starts the song as if its play control had been
  // pressed, so the row shows as the one playing and the player mounts with `autoPlay`,
  // and then asks for the row to be brought into view. The page's usual rules apply: the
  // video with sound, or for a held song the audio with its note. A song the page doesn't
  // list (the catalogue changed since the search) does nothing, and the page stays at the
  // top. If the browser won't start sound by itself the player is still open under the
  // row with its controls showing, and one press starts it. From then on it is just the
  // playing row, until another plays or I leave.
  useEffect(() => {
    if (!openAt || openedKeyRef.current === openAt.key) return
    openedKeyRef.current = openAt.key
    const group = groups.find((entry) => entry.themes.some((theme) => theme.id === openAt.themeId))
    if (!group) return
    setPlaying(themeKey(group.animeId, openAt.themeId))
    setScrollToTheme(openAt.themeId)
  }, [openAt, groups])

  // The scroll waits for the commit that mounts the player: the row alone is near the end
  // of the page, and the player is what makes the page tall enough to bring the row to the
  // top (a target past the end is clamped, which is as far as the page goes). It goes
  // through scrollWindowTo with the navbar hidden, as SeriesPage's pins do, so it is never
  // read as my scrolling and the sticky navbar doesn't cover the row. A smooth scroll
  // only when I haven't asked my system for reduced motion. The player's video is still
  // taking its size, so the move goes on following the row for a moment (bringRowIntoView).
  useLayoutEffect(() => {
    if (scrollToTheme === null) return
    const row = sectionRef.current?.querySelector<HTMLElement>(`[data-theme-id="${scrollToTheme}"]`)
    if (row) {
      stopFollowingRef.current?.()
      stopFollowingRef.current = bringRowIntoView(row)
    }
    setScrollToTheme(null)
  }, [scrollToTheme])

  useEffect(() => () => stopFollowingRef.current?.(), [])

  const heading = <h2>Openings and endings</h2>

  return (
    <section className="themes-section" ref={sectionRef}>
      {hasControls ? (
        <div className="themes-section__header">
          {heading}
          <button
            type="button"
            className="themes-section__toggle-all"
            disabled={view.groups.length === 0}
            onClick={view.toggleAll}
          >
            {view.allLabel}
          </button>
        </div>
      ) : (
        heading
      )}
      {hasControls && (
        <div className={'themes-section__search' + (view.searching ? ' themes-section__search--active' : '')}>
          <SearchGlyph />
          <input
            ref={searchRef}
            type="text"
            className="themes-section__search-input"
            placeholder="Search songs or artists…"
            aria-label="Search songs or artists"
            autoComplete="off"
            value={view.query}
            onChange={(event) => view.setQuery(event.target.value)}
          />
          {view.query !== '' && (
            <button
              type="button"
              className="themes-section__search-clear"
              aria-label="Clear search"
              onClick={() => {
                view.setQuery('')
                searchRef.current?.focus()
              }}
            >
              &times;
            </button>
          )}
        </div>
      )}
      {listed.length === 0 ? (
        <p className="themes-section__quiet">
          {ready ? `No openings or endings are listed for this ${subject}.` : "Openings and endings haven't loaded yet."}
        </p>
      ) : view.noMatch ? (
        <p className="themes-section__quiet" role="status">
          No songs match your search.
        </p>
      ) : (
        view.groups.map(({ group, open, matching, rows }) => {
          const listId = `themes-group-${group.animeId}`
          return (
            <div key={group.animeId} className="themes-section__group">
              {grouped && (
                <h3 className="themes-section__group-title">
                  <button
                    type="button"
                    className="themes-section__group-toggle"
                    aria-expanded={open}
                    aria-controls={open ? listId : undefined}
                    onClick={() => view.toggle(group.animeId)}
                  >
                    <span className="themes-section__group-caret" aria-hidden="true">
                      {open ? '▾' : '▸'}
                    </span>
                    <span className="themes-section__group-label">
                      {group.title}{' '}
                      <span className="themes-section__group-count">({matching.length})</span>
                    </span>
                  </button>
                </h3>
              )}
              {rows.length > 0 && (
                <ul id={listId} className="themes-section__list">
                  {rows.map((theme) => {
                    const key = themeKey(group.animeId, theme.id)
                    return (
                      <ThemeRow
                        key={theme.id}
                        theme={theme}
                        playing={playing === key}
                        hideNsfw={hideHentai}
                        data-theme-id={theme.id}
                        onToggle={() => setPlaying((current) => (current === key ? null : key))}
                      />
                    )
                  })}
                </ul>
              )}
            </div>
          )
        })
      )}
    </section>
  )
}
