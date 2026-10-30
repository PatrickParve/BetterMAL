import { useId, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, getHigherOrLowerPool, startHigherOrLower } from '../../api/client.ts'
import type { HigherOrLowerGameDto, HigherOrLowerPoolDto, HigherOrLowerStateDto } from '../../api/types.ts'
import { useActionFailure } from '../../context/ActionFailureContext.tsx'
import { useContentFilter } from '../../context/ContentFilterContext.tsx'
import { useScoreVisibility } from '../../context/ScoreVisibilityContext.tsx'
import { useHeldData } from '../../hooks/useHeldData.ts'
import { usePageData } from '../../hooks/usePageData.ts'
import { MEDIA_TYPE_ORDER, mediaTypeLabel } from '../../utils/anime.ts'
import { preloadPictures } from '../../utils/preloadPictures.ts'
import { HIGHER_OR_LOWER } from './games.ts'
import {
  LIST_SCOPES,
  settingsKey,
  withTypeToggled,
  withYearFrom,
  withYearTo,
  type HigherOrLowerLobbySettings,
} from './higherLowerSettings.ts'
import { PosterPair } from './PosterPair.tsx'
import './GameButton.css'
import './HigherLowerLobby.css'

// The longest Play waits for the first pair's pictures; a slow one is drawn when it comes.
const FIRST_PAIR_WAIT_MS = 1200

type HigherLowerLobbyProps = {
  state: HigherOrLowerStateDto
  settings: HigherOrLowerLobbySettings
  onSettingsChange: (settings: HigherOrLowerLobbySettings) => void
  onStarted: (game: HigherOrLowerGameDto, played: HigherOrLowerLobbySettings) => void
}

// The setup screen (higher-or-lower "Higher or Lower opens on its setup
// screen"): the game's name, line of text and poster pair beside the settings,
// the count they leave, and Play. It shows nothing about a game just played. Two columns where they fit, stacked below with
// the posters left out so the settings stay near the top. The count is its own
// read keyed on the settings, held across the next one so changing a setting
// never flashes it empty (D10).
export function HigherLowerLobby({ state, settings, onSettingsChange, onStarted }: HigherLowerLobbyProps) {
  const { hideHentai } = useContentFilter()
  const { hidden: scoresHidden } = useScoreVisibility()
  const reportFailure = useActionFailure()
  const labelId = useId()

  const key = settingsKey(settings, hideHentai)
  const { data: pool, failed: poolFailed } = usePageData<HigherOrLowerPoolDto>(`higher-or-lower-pool:${key}`, () =>
    getHigherOrLowerPool({ ...settings, hideHentai }),
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
      const game = await startHigherOrLower({ ...settings, hideHentai })
      // The first two pictures download before the table replaces this screen, so
      // it opens with them drawn rather than filling them in a beat later.
      await preloadPictures([game.left.pictureUrl, game.right.pictureUrl], 'card', FIRST_PAIR_WAIT_MS)
      onStarted(game, settings)
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

  const years = state.years === null ? [] : Array.from({ length: state.years.max - state.years.min + 1 }, (_, i) => state.years!.max - i)

  return (
    <div className="higher-lower-lobby">
      <div className="higher-lower-lobby__intro">
        <Link to="/games" className="game-outline-button higher-lower-lobby__back">
          ← Games
        </Link>
        <h1 className="higher-lower-lobby__title">{HIGHER_OR_LOWER.name}</h1>
        <p className="higher-lower-lobby__text">{HIGHER_OR_LOWER.text}</p>
        <div className="higher-lower-lobby__art">
          <PosterPair pictures={state.pictures} size="lobby" />
        </div>
      </div>

      <div className="higher-lower-lobby__setup">
        <div className="higher-lower-lobby__panel">
          <div className="higher-lower-lobby__field">
            <span className="higher-lower-lobby__label">Release year</span>
            <div className="higher-lower-lobby__years">
              <select
                className="higher-lower-lobby__select"
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
                className="higher-lower-lobby__select"
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

          <div className="higher-lower-lobby__field">
            <span className="higher-lower-lobby__label" id={`${labelId}-type`}>
              Type
            </span>
            <div className="higher-lower-lobby__pills" role="group" aria-labelledby={`${labelId}-type`}>
              <button
                type="button"
                className="higher-lower-lobby__pill"
                aria-pressed={settings.mediaTypes === null}
                onClick={() => onSettingsChange({ ...settings, mediaTypes: null })}
              >
                All
              </button>
              {MEDIA_TYPE_ORDER.map((type) => (
                <button
                  key={type}
                  type="button"
                  className="higher-lower-lobby__pill"
                  aria-pressed={settings.mediaTypes?.includes(type) ?? false}
                  onClick={() => onSettingsChange(withTypeToggled(settings, type))}
                >
                  {mediaTypeLabel(type)}
                </button>
              ))}
            </div>
          </div>

          <div className="higher-lower-lobby__field">
            <span className="higher-lower-lobby__label" id={`${labelId}-anime`}>
              Anime
            </span>
            <div className="higher-lower-lobby__segments" role="group" aria-labelledby={`${labelId}-anime`}>
              {LIST_SCOPES.map((scope) => (
                <button
                  key={scope.value}
                  type="button"
                  className="higher-lower-lobby__segment"
                  aria-pressed={settings.list === scope.value}
                  onClick={() => onSettingsChange({ ...settings, list: scope.value })}
                >
                  {scope.label}
                </button>
              ))}
            </div>
          </div>

          <div className="higher-lower-lobby__start">
            <p className="higher-lower-lobby__count" aria-live="polite" aria-atomic="true">
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

          {scoresHidden && <p className="higher-lower-lobby__note">You'll see MAL scores while you play.</p>}
        </div>
      </div>
    </div>
  )
}
