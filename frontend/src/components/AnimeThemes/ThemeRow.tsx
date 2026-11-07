import { Link } from 'react-router-dom'
import type { AnimeThemeDto } from '../../api/types.ts'
import { RowPicture } from '../RowPicture.tsx'
import { ThemePlayer } from './ThemePlayer.tsx'
import { formatEpisodes, isPlayable, themeName, type ThemeStart } from './themeFlags.ts'
import './ThemeRow.css'

/** The anime a song belongs to, for the rows that name it (the search page's OP/ED results). */
export type ThemeRowAnime = {
  animeId: number
  /** Already chosen for display: the English title where the anime has one. */
  title: string
  /** The picture the app displays for it, or null for an empty picture box. */
  pictureUrl: string | null
}

type ThemeRowProps = {
  theme: AnimeThemeDto
  playing: boolean
  hideNsfw: boolean
  /** The play control was pressed: opens this row's player, or closes it if it is open. */
  onToggle: () => void
  /**
   * Names the song's anime on its row: a picture ahead of the label, and the anime's
   * title beneath the song, each a plain link to the anime's page, which opens it as any
   * other link does (improve-search-page design.md D11). The anime and series pages pass
   * nothing.
   */
  anime?: ThemeRowAnime
  /** Which kind of file the row's player begins with: the video unless the audio is asked for. */
  startWith?: ThemeStart
  /** Put on the row so a page can find it again: the anime page, to open at one song. */
  'data-theme-id'?: number
}

// One opening or ending (anime-themes "The themes section lists openings and
// endings"): its label, the song title, who sings it and where it plays, with a
// mark for what it is flagged as and a play control. What isn't known is left out,
// so a song with no artist or a film's theme has no gap where one would be, a title
// that isn't known says so, and a theme with neither a video nor audio has no play
// control. A flagged row always stays; a mark says what it is. The control is named
// after its theme for a screen reader, and its pressed state is "playing".
// With `anime` it is a search result and names its anime too (theme-search "OP/ED
// results are songs that play in place"); without it, it renders exactly as it does on
// the anime and series pages. The picture and the title are both links to the anime's
// page. The picture's is left out of the tab order and of a screen reader's reading, since
// the title beside it goes to the same place and would otherwise be read and tabbed to
// twice; a click or a tap on the picture still goes there.
export function ThemeRow({
  theme,
  playing,
  hideNsfw,
  onToggle,
  anime,
  startWith,
  'data-theme-id': themeId,
}: ThemeRowProps) {
  const episodes = formatEpisodes(theme.episodes)
  const artists = theme.artists.join(', ')
  const meta = [artists, episodes].filter((part) => part)

  return (
    <li className={'theme-row' + (playing ? ' theme-row--playing' : '')} data-theme-id={themeId}>
      <div className="theme-row__line">
        {anime && (
          <Link to={`/anime/${anime.animeId}`} className="theme-row__picture-link" tabIndex={-1} aria-hidden="true">
            <RowPicture src={anime.pictureUrl} className="theme-row__picture" />
          </Link>
        )}
        <span className="theme-row__label" title={theme.label}>{theme.label}</span>
        <div className="theme-row__text">
          <p className="theme-row__title">
            {theme.songTitle ? theme.songTitle : <span className="theme-row__unknown">Song title not known</span>}
            {theme.nsfw && <span className="theme-mark theme-mark--nsfw">NSFW</span>}
            {theme.spoiler && <span className="theme-mark theme-mark--spoiler">Spoiler</span>}
          </p>
          {meta.length > 0 && <p className="theme-row__meta">{meta.join(' · ')}</p>}
          {anime && (
            <p className="theme-row__anime">
              <Link to={`/anime/${anime.animeId}`}>{anime.title}</Link>
            </p>
          )}
        </div>
        {isPlayable(theme) && (
          <button
            type="button"
            className="theme-row__play"
            aria-label={`Play ${themeName(theme)}`}
            aria-pressed={playing}
            onClick={onToggle}
          >
            {playing ? (
              <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">
                <rect x="6" y="6" width="12" height="12" rx="1.5" fill="currentColor" />
              </svg>
            ) : (
              <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false">
                <path d="M8 5.5v13a1 1 0 0 0 1.5.86l10.5-6.5a1 1 0 0 0 0-1.72L9.5 4.64A1 1 0 0 0 8 5.5Z" fill="currentColor" />
              </svg>
            )}
          </button>
        )}
      </div>
      {playing && <ThemePlayer theme={theme} hideNsfw={hideNsfw} startWith={startWith} />}
    </li>
  )
}
