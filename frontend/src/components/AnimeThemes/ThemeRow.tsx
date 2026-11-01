import type { AnimeThemeDto } from '../../api/types.ts'
import { ThemePlayer } from './ThemePlayer.tsx'
import { formatEpisodes, isPlayable, themeName } from './themeFlags.ts'
import './ThemeRow.css'

type ThemeRowProps = {
  theme: AnimeThemeDto
  playing: boolean
  hideNsfw: boolean
  /** The play control was pressed: opens this row's player, or closes it if it is open. */
  onToggle: () => void
}

// One opening or ending (anime-themes "The themes section lists openings and
// endings"): its label, the song title, who sings it and where it plays, with a
// mark for what it is flagged as and a play control. What isn't known is left out,
// so a song with no artist or a film's theme has no gap where one would be, a title
// that isn't known says so, and a theme with neither a video nor audio has no play
// control. A flagged row always stays; a mark says what it is. The control is named
// after its theme for a screen reader, and its pressed state is "playing".
export function ThemeRow({ theme, playing, hideNsfw, onToggle }: ThemeRowProps) {
  const episodes = formatEpisodes(theme.episodes)
  const artists = theme.artists.join(', ')
  const meta = [artists, episodes].filter((part) => part)

  return (
    <li className={'theme-row' + (playing ? ' theme-row--playing' : '')}>
      <div className="theme-row__line">
        <span className="theme-row__label" title={theme.label}>{theme.label}</span>
        <div className="theme-row__text">
          <p className="theme-row__title">
            {theme.songTitle ? theme.songTitle : <span className="theme-row__unknown">Song title not known</span>}
            {theme.nsfw && <span className="theme-mark theme-mark--nsfw">NSFW</span>}
            {theme.spoiler && <span className="theme-mark theme-mark--spoiler">Spoiler</span>}
          </p>
          {meta.length > 0 && <p className="theme-row__meta">{meta.join(' · ')}</p>}
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
      {playing && <ThemePlayer theme={theme} hideNsfw={hideNsfw} />}
    </li>
  )
}
