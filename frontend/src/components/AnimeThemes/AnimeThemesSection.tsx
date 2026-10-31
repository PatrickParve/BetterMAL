import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { AnimeThemeDto } from '../../api/types.ts'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { ThemeRow } from './ThemeRow.tsx'
import './AnimeThemesSection.css'

/** The themes of one anime. A `title` makes the group a headed, linked block, as a series page needs; without one the list stands alone. */
export type ThemeGroup = {
  animeId: number
  title: string | null
  themes: AnimeThemeDto[]
}

type AnimeThemesSectionProps = {
  groups: ThemeGroup[]
  /** Whether a catalogue refresh has ever finished: it tells "none listed" from "not loaded yet". */
  ready: boolean
  /** What the quiet line says it has no openings or endings for. */
  subject: 'anime' | 'series'
}

const CREDIT_URL = 'https://animethemes.moe/'

const themeKey = (animeId: number, themeId: number) => `${animeId}:${themeId}`

// The openings and endings of an anime, or of a series' main line group by group
// (anime-themes "The themes section lists openings and endings"). One section
// serves both pages and holds the one `playing` theme, so starting another row's
// player closes the first, across every group, and a theme that the groups no
// longer hold stops: the series page drops a group when I switch route. Nothing
// plays until a row's control is pressed. With nothing to list it is one quiet line,
// no retry and no credit, saying "not loaded yet" until a first refresh has finished.
// The credit is a plain text link, shown only where the data is.
export function AnimeThemesSection({ groups, ready, subject }: AnimeThemesSectionProps) {
  const { hideHentai } = useContentFilter()
  const [playing, setPlaying] = useState<string | null>(null)

  const listed = groups.filter((group) => group.themes.length > 0)

  // A theme whose group has gone stops: its row no longer exists to hold the player.
  if (playing !== null && !listed.some((group) => group.themes.some((theme) => themeKey(group.animeId, theme.id) === playing))) {
    setPlaying(null)
  }

  return (
    <section className="themes-section">
      <h2>Openings and endings</h2>
      {listed.length === 0 ? (
        <p className="themes-section__quiet">
          {ready ? `No openings or endings are listed for this ${subject}.` : "Openings and endings haven't loaded yet."}
        </p>
      ) : (
        <>
          {listed.map((group) => (
            <div key={group.animeId} className="themes-section__group">
              {group.title !== null && (
                <h3 className="themes-section__group-title">
                  <Link to={`/anime/${group.animeId}`}>{group.title}</Link>
                </h3>
              )}
              <ul className="themes-section__list">
                {group.themes.map((theme) => {
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
            </div>
          ))}
          <p className="themes-section__credit">
            <a href={CREDIT_URL} target="_blank" rel="noreferrer">
              Songs and videos from AnimeThemes
            </a>
          </p>
        </>
      )}
    </section>
  )
}
