import { useRef, useState } from 'react'
import type { ThemeSearchResultDto } from '../../api/types.ts'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { useRestorableState } from '../../hooks/useRestorableState.ts'
import { useScrollReveal } from '../../hooks/useScrollReveal.ts'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { ThemeRow } from './ThemeRow.tsx'
import { themeKey } from './themeFlags.ts'
import './ThemeResults.css'

// How many more rows each pass of the sentinel reveals. The server's 200 are all in
// memory; slicing keeps a long list from drawing every picture at once.
const CHUNK_SIZE = 50

// The search page's OP/ED results (theme-search "OP/ED results are songs that play in
// place", improve-search-page design.md D11): the matching songs as the series page's
// song rows, each with its anime's picture and name, in the server's order. The caller
// has already decided there are songs to list: "not loaded yet", "no songs found" and a
// failed read are the page's own notes.
//
// It plays as the anime and series pages do. One `playing` key (anime id and theme id)
// names the song whose player is open, so starting another row's closes the first, and a
// song whose row isn't on screen stops (the rule AnimeThemesSection applies to its own
// rows): a song with no row has no control left to stop it. The page keys this list by the
// query, so a new search or a switch to Anime replaces it and stops the song. Every row
// starts with its audio and can switch to the video (`startWith`); a song flagged NSFW or
// spoiler holds its video back as it does anywhere. Nothing plays until a control is
// pressed.
//
// Rows are revealed 50 at a time as a sentinel below them scrolls into view, with no
// page controls, and the count revealed is restorable view state, so Back returns to the
// same depth.
export function ThemeResults({ songs }: { songs: ThemeSearchResultDto[] }) {
  const { hideHentai } = useContentFilter()
  const [playing, setPlaying] = useState<string | null>(null)
  const [visibleCount, setVisibleCount] = useRestorableState('themeVisibleCount', CHUNK_SIZE)
  const sentinelRef = useRef<HTMLDivElement>(null)

  useScrollReveal(sentinelRef, songs.length, CHUNK_SIZE, setVisibleCount)

  const shown = songs.slice(0, visibleCount)
  if (playing !== null && !shown.some((song) => themeKey(song.animeId, song.theme.id) === playing)) {
    setPlaying(null)
  }

  return (
    <>
      <ul className="theme-results">
        {shown.map((song) => {
          const key = themeKey(song.animeId, song.theme.id)
          return (
            <ThemeRow
              key={key}
              theme={song.theme}
              playing={playing === key}
              hideNsfw={hideHentai}
              startWith="audio"
              data-theme-id={song.theme.id}
              anime={{
                animeId: song.animeId,
                title: pickDisplayTitle(song.title, song.englishTitle),
                pictureUrl: song.pictureUrl,
              }}
              onToggle={() => setPlaying((current) => (current === key ? null : key))}
            />
          )
        })}
      </ul>
      <div ref={sentinelRef} className="theme-results__sentinel" />
    </>
  )
}
