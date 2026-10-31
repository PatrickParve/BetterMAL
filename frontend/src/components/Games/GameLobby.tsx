import { useId, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client.ts'
import type { GamePoolDto, GameSettings } from '../../api/types.ts'
import { useActionFailure } from '../../context/ActionFailureContext.tsx'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { useScoreVisibility } from '../../context/ScoreVisibilityContext.tsx'
import { useHeldData } from '../../hooks/useHeldData.ts'
import { usePageData } from '../../hooks/usePageData.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel } from '../../utils/anime.ts'
import type { GameListing } from './games.ts'
import {
  LIST_SCOPES,
  settingsKey,
  withTypeToggled,
  withYearFrom,
  withYearTo,
  type GameLobbySettings,
} from './gameSettings.ts'
import './GameButton.css'
import './GameLobby.css'

type GameLobbyProps<TGame> = {
  /** The game's name and line of text, from the same entry as its Games tile. */
  game: GameListing
  /** The poster pair beside the settings; the lobby only places it. */
  art: ReactNode
  /** The release years the eligible anime cover, for the From and To choices. */
  years: { min: number; max: number } | null
  settings: GameLobbySettings
  onSettingsChange: (settings: GameLobbySettings) => void
  /** How many anime these settings leave, and whether that is enough to play. */
  readPool: (settings: GameSettings) => Promise<GamePoolDto>
  /** Starts a game, and settles whatever it wants drawn when the board opens. */
  start: (settings: GameSettings) => Promise<TGame>
  onStarted: (game: TGame, played: GameLobbySettings) => void
}

// The setup screen both games open on (higher-or-lower "Higher or Lower opens
// on its setup screen", guess-the-anime "Guess the Anime opens on its setup
// screen"): the game's name, line of text and art beside the settings, the count
// they leave, and Play. It shows nothing about a game just played. Two columns
// where they fit, stacked below with the art left out so the settings stay near
// the top. The count is its own read keyed on the settings, held across the next
// one so changing a setting never flashes it empty (D10). What differs between
// the games is handed in: the listing, the art, the pool read and the start.
export function GameLobby<TGame>({
  game: listing,
  art,
  years: yearBounds,
  settings,
  onSettingsChange,
  readPool,
  start,
  onStarted,
}: GameLobbyProps<TGame>) {
  const { hideHentai } = useContentFilter()
  const { hidden: scoresHidden } = useScoreVisibility()
  const reportFailure = useActionFailure()
  const labelId = useId()

  const key = settingsKey(settings, hideHentai)
  const { data: pool, failed: poolFailed } = usePageData<GamePoolDto>(`${listing.to}:pool:${key}`, () =>
    readPool({ ...settings, hideHentai }),
  )
  const { shown: shownPool } = useHeldData(pool, poolFailed)

  const [starting, setStarting] = useState(false)
  // The settings Start refused with a 409 (the data moved under the count):
  // treated like a count that says there are too few, until they change.
  const [refusedKey, setRefusedKey] = useState<string | null>(null)
  const tooFew = (shownPool !== null && !shownPool.playable) || refusedKey === key

  async function play() {
    if (starting || tooFew) return
    setStarting(true)
    try {
      onStarted(await start({ ...settings, hideHentai }), settings)
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) setRefusedKey(key)
      else
        reportFailure({
          title: "Couldn't start the game",
          reason: err instanceof ApiError ? err.reason : null,
        })
    } finally {
      setStarting(false)
    }
  }

  const years = yearBounds === null ? [] : Array.from({ length: yearBounds.max - yearBounds.min + 1 }, (_, i) => yearBounds.max - i)

  return (
    <div className="game-lobby">
      <div className="game-lobby__intro">
        <Link to="/games" className="game-outline-button game-lobby__back">
          ← Games
        </Link>
        <h1 className="game-lobby__title">{listing.name}</h1>
        <p className="game-lobby__text">{listing.text}</p>
        <div className="game-lobby__art">{art}</div>
      </div>

      <div className="game-lobby__setup">
        <div className="game-lobby__panel">
          <div className="game-lobby__field">
            <span className="game-lobby__label">Release year</span>
            <div className="game-lobby__years">
              <select
                className="game-lobby__select"
                aria-label="Release year from"
                value={settings.yearFrom ?? ''}
                onChange={(event) => onSettingsChange(withYearFrom(settings, event.target.value === '' ? null : Number(event.target.value)))}
              >
                <option value="">Any</option>
                {years.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </select>
              <span>to</span>
              <select
                className="game-lobby__select"
                aria-label="Release year to"
                value={settings.yearTo ?? ''}
                onChange={(event) => onSettingsChange(withYearTo(settings, event.target.value === '' ? null : Number(event.target.value)))}
              >
                <option value="">Any</option>
                {years.map((year) => (
                  <option key={year} value={year}>
                    {year}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="game-lobby__field">
            <span className="game-lobby__label" id={`${labelId}-type`}>
              Type
            </span>
            <div className="game-lobby__pills" role="group" aria-labelledby={`${labelId}-type`}>
              <button
                type="button"
                className="game-lobby__pill"
                aria-pressed={settings.mediaTypes === null}
                onClick={() => onSettingsChange({ ...settings, mediaTypes: null })}
              >
                All
              </button>
              {MEDIA_TYPE_ORDER.map((type) => (
                <button
                  key={type}
                  type="button"
                  className="game-lobby__pill"
                  aria-pressed={settings.mediaTypes?.includes(type) ?? false}
                  onClick={() => onSettingsChange(withTypeToggled(settings, type))}
                >
                  {mediaTypeLabel(type)}
                </button>
              ))}
            </div>
          </div>

          <div className="game-lobby__field">
            <span className="game-lobby__label" id={`${labelId}-anime`}>
              Anime
            </span>
            <div className="game-lobby__segments" role="group" aria-labelledby={`${labelId}-anime`}>
              {LIST_SCOPES.map((scope) => (
                <button
                  key={scope.value}
                  type="button"
                  className="game-lobby__segment"
                  aria-pressed={settings.list === scope.value}
                  onClick={() => onSettingsChange({ ...settings, list: scope.value })}
                >
                  {scope.label}
                </button>
              ))}
            </div>
          </div>

          <div className="game-lobby__start">
            <p className="game-lobby__count" aria-live="polite" aria-atomic="true">
              {tooFew ? (
                'Not enough anime for these settings.'
              ) : shownPool !== null ? (
                <>
                  <b>{shownPool.count.toLocaleString('en-US')}</b> anime
                </>
              ) : null}
            </p>
            <button type="button" className="game-button" disabled={tooFew || starting} onClick={() => void play()}>
              Play
            </button>
          </div>

          {scoresHidden && <p className="game-lobby__note">You'll see MAL scores while you play.</p>}
        </div>
      </div>
    </div>
  )
}
