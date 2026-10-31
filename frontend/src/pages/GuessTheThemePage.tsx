import { useEffect, useMemo, useRef, useState } from 'react'
import { getGuessTheThemePool, getGuessTheThemeState, prepareGuessTheTheme, startGuessTheTheme } from '../api/client.ts'
import type { GameSettings, GameThemeChoice, GuessTheThemeGameDto, GuessTheThemeStateDto } from '../api/types.ts'
import { GameLobby } from '../components/Games/GameLobby.tsx'
import { GUESS_THE_THEME } from '../components/Games/games.ts'
import {
  readStoredSettings,
  withinYears,
  writeStoredSettings,
  type GameLobbySettings,
} from '../components/Games/gameSettings.ts'
import { GuessTheThemeBoard } from '../components/Games/GuessTheThemeBoard.tsx'
import { PosterPair } from '../components/Games/PosterPair.tsx'
import * as themeAudio from '../components/Games/themeAudio.ts'
import { requestClip } from '../components/Games/themeClips.ts'
import { ThemeChoiceField } from '../components/Games/ThemeChoiceField.tsx'
import {
  readStoredThemeChoice,
  writeStoredThemeChoice,
  type FinishedThemeGame,
  type ThemeOver,
} from '../components/Games/themeGame.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './GuessTheThemePage.css'

// Guess the Opening/Ending (add-anime-themes-and-guess-the-theme design.md D11,
// D12). One read of the game's state, then the board while a game is in progress and
// the setup screen otherwise, as Guess the Anime's page does. The game itself lives on
// the server, so the page only carries what it needs to draw: the read (kept current
// as moves are made, so Back restores the game where it is) and the setup screen's
// settings. A game that has ended stays on the board until I leave it, and nothing
// about it is kept on the server: the one place it is held is this history entry's
// snapshot, so going to another page and coming Back, or Forward, finds it as I left
// it; Back to start, Escape and Play again drop it.

// The longest the page waits for its pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200
// The longest Play waits for the first song before opening the board anyway, which
// then waits for it with a control that says so.
const FIRST_CLIP_WAIT_MS = 3000
// While the song list hasn't loaded for the first time, the setup screen reads the
// state again at this pace, so "on their way" stops being said once they have.
const CATALOGUE_POLL_MS = 20_000
// How long the settings stay as they are before a song is readied for them, so a few
// quick changes don't each read one.
const PREPARE_DELAY_MS = 400

// The settings this game last played with in this browser; each game keeps its own.
const SETTINGS_KEY = 'bettermal.guessTheTheme.settings'

export function GuessTheThemePage() {
  const { hideHentai } = useContentFilter()
  const { data, setData, reload, failed, retry } = usePageData<GuessTheThemeStateDto>(
    `guess-the-theme:${hideHentai}`,
    () =>
      getGuessTheThemeState({ hideHentai }).then(async (state) => {
        // Only what the page will draw: the pictures of a game in progress, so they
        // appear with the board, or the setup screen's poster pair.
        if (state.game) {
          await Promise.all([
            preloadPictures(
              state.game.guessed.map((card) => card.pictureUrl),
              'row',
              PRELOAD_WAIT_MS,
            ),
            preloadPictures([state.game.reveal?.pictureUrl], 'tile', PRELOAD_WAIT_MS),
          ])
        } else {
          await preloadPictures(state.pictures, 'tile', PRELOAD_WAIT_MS)
        }
        return state
      }),
  )

  // Seeded from what was last played with in this browser, and from the history
  // entry's snapshot on Back, so the settings survive the board and back.
  const [initialSettings] = useState(() => readStoredSettings(SETTINGS_KEY))
  const [initialTheme] = useState(() => readStoredThemeChoice())
  const [chosen, setSettings] = useRestorableState<GameLobbySettings>('settings', initialSettings)
  const [themes, setThemes] = useRestorableState<GameThemeChoice>('themes', initialTheme)
  // The game that has just ended on this entry, if one has. A fresh visit starts with
  // none, so a new trip to the page finds the setup screen, and a reload does too.
  const [finished, setFinished] = useRestorableState<FinishedThemeGame | null>('finished', null)
  // The game this page started itself, whose first length may play by itself: the
  // press on Play (or Play again) is what let the browser make sound. A game found
  // already under way never plays until I press.
  const [startedId, setStartedId] = useState<number | null>(null)

  const years = data?.years ?? null
  const settings = useMemo(() => withinYears(chosen, years), [chosen, years])

  // Sound is made ready from the moment the page opens: the audio is set up ahead of any
  // press, and every press lets it run (themeAudio.ts says why it takes both).
  useEffect(() => {
    themeAudio.prepare()
    return themeAudio.listenForPresses()
  }, [])

  // While the setup screen is open a song is readied for its settings, so Play can start on
  // it at once. Only its audio is read, and only one, whatever else I do here.
  const setupOpen = data !== null && data.game === null && finished === null && data.catalogueReady
  const songSettings = useMemo(() => ({ ...settings, hideHentai, themes }), [settings, hideHentai, themes])
  const prepareTimer = useRef(0)
  useEffect(() => {
    if (!setupOpen) return
    prepareTimer.current = window.setTimeout(() => void prepareGuessTheTheme(songSettings).catch(() => undefined), PREPARE_DELAY_MS)
    return () => window.clearTimeout(prepareTimer.current)
  }, [setupOpen, songSettings])

  // Before the first catalogue refresh has ever finished the setup screen says the
  // songs are on their way; reading the state again now and then lets it stop.
  const waitingForSongs = data !== null && data.game === null && !data.catalogueReady
  useEffect(() => {
    if (!waitingForSongs) return
    const timer = window.setInterval(() => void reload(), CATALOGUE_POLL_MS)
    return () => window.clearInterval(timer)
  }, [waitingForSongs, reload])

  // Starts a game, from the setup screen's Play or the board's Play again. The press has
  // let the page make sound by now. The first song is loaded before the board opens, so
  // its first length can play by itself as the board appears.
  async function startGame(forSettings: GameSettings): Promise<GuessTheThemeGameDto> {
    window.clearTimeout(prepareTimer.current)
    const game = await startGuessTheTheme({ ...forSettings, themes })
    await Promise.race([
      requestClip(game.id, game.round).catch(() => undefined),
      new Promise<void>((resolve) => window.setTimeout(resolve, FIRST_CLIP_WAIT_MS)),
    ])
    return game
  }

  function handleStarted(game: GuessTheThemeGameDto, played: GameLobbySettings) {
    writeStoredSettings(SETTINGS_KEY, played)
    writeStoredThemeChoice(themes)
    setFinished(null)
    setStartedId(game.id)
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  // A new game with the settings this one was played with, which are the ones the
  // setup screen last played with. Throws on a refusal, which the board reports.
  async function handlePlayAgain() {
    handleStarted(await startGame({ ...settings, hideHentai }), settings)
  }

  // A move has landed: the read holds the game as it now stands, so Back restores it.
  function handleGame(game: GuessTheThemeGameDto) {
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  // A game has ended: the server holds none now, and this entry holds the one just played.
  function handleFinished(over: ThemeOver) {
    const game = data?.game
    if (game === null || game === undefined) return
    setFinished({ game, over, added: false })
  }

  function handleAdded() {
    setFinished((prev) => (prev === null ? prev : { ...prev, added: true }))
  }

  // Over, or ended with End game and left: nothing is kept of the game.
  function handleLeave() {
    setFinished(null)
    setData((prev) => (prev === null ? prev : { ...prev, game: null }))
  }

  // A finished game is drawn from what this entry kept, whatever the server says now:
  // it holds no game once one has ended.
  const shown = finished !== null ? finished.game : (data?.game ?? null)

  return (
    <div className="guess-the-theme-page">
      {data !== null && shown !== null && (
        <GuessTheThemeBoard
          key={shown.id}
          game={shown}
          finished={finished}
          startedHere={startedId === shown.id}
          onlyMyList={settings.list === 'OnlyMyList'}
          onGame={handleGame}
          onFinished={handleFinished}
          onAdded={handleAdded}
          onLeave={handleLeave}
          onPlayAgain={handlePlayAgain}
          onStale={() => void reload()}
          onAutoplayed={() => setStartedId(null)}
        />
      )}
      {data !== null && shown === null && (
        <GameLobby
          game={GUESS_THE_THEME}
          art={<PosterPair pictures={data.pictures} size="lobby" badge={GUESS_THE_THEME.badge} />}
          years={data.years}
          settings={settings}
          onSettingsChange={setSettings}
          readPool={(forSettings) => getGuessTheThemePool({ ...forSettings, themes })}
          start={startGame}
          onStarted={handleStarted}
          extraSetting={<ThemeChoiceField value={themes} onChange={setThemes} />}
          extraKey={themes}
          showScoreNote={false}
          notReadyText={data.catalogueReady ? undefined : 'The songs are still on their way.'}
          startingLabel="Loading…"
        />
      )}
      {data === null && failed && <LoadFailedNotice what="Guess the Opening/Ending" onRetry={retry} />}
      <LoadingNotice active={data === null && !failed} className="guess-the-theme-page__loading" />
    </div>
  )
}
