import { useRef, useState } from 'react'
import type { AnimeThemeDto } from '../../api/types.ts'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { ThemeRow } from './ThemeRow.tsx'
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
}

const themeKey = (animeId: number, themeId: number) => `${animeId}:${themeId}`

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
export function AnimeThemesSection({ groups, ready, subject }: AnimeThemesSectionProps) {
  const { hideHentai } = useContentFilter()
  const [playing, setPlaying] = useState<string | null>(null)
  const searchRef = useRef<HTMLInputElement>(null)

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

  const heading = <h2>Openings and endings</h2>

  return (
    <section className="themes-section">
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
