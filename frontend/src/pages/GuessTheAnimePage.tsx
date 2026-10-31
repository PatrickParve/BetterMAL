import { useMemo, useState } from 'react'
import { getGuessTheAnimePool, getGuessTheAnimeState, startGuessTheAnime } from '../api/client.ts'
import type { GuessTheAnimeGameDto, GuessTheAnimeRowDto, GuessTheAnimeStateDto } from '../api/types.ts'
import { GameLobby } from '../components/Games/GameLobby.tsx'
import { GUESS_THE_ANIME } from '../components/Games/games.ts'
import { GuessBoard } from '../components/Games/GuessBoard.tsx'
import type { FinishedGuessGame, GuessOver } from '../components/Games/guessGame.ts'
import { PosterPair } from '../components/Games/PosterPair.tsx'
import {
  readStoredSettings,
  withinYears,
  writeStoredSettings,
  type GameLobbySettings,
} from '../components/Games/gameSettings.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './GuessTheAnimePage.css'

// Guess the Anime (add-guess-the-anime design.md D10). One read of the game's
// state, then the board while a game is in progress and the setup screen
// otherwise, as Higher or Lower's page does. The game itself lives on the server,
// so the page only carries what it needs to draw: the read (kept current as
// guesses are made, so Back restores the rows the game is on) and the setup
// screen's settings. A game that has ended stays on the board until I leave it, and
// nothing about it is kept on the server. The one place it is held is this history
// entry's snapshot, so going to another page and coming Back, or Forward, finds it as
// I left it (design.md D15); Back to start, Escape and Play again drop it.
// The longest the page waits for its pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200

// The settings this game last played with in this browser; each game keeps its own.
const SETTINGS_KEY = 'bettermal.guessTheAnime.settings'

export function GuessTheAnimePage() {
  const { hideHentai } = useContentFilter()
  const { data, setData, reload, failed, retry } = usePageData<GuessTheAnimeStateDto>(
    `guess-the-anime:${hideHentai}`,
    () =>
      getGuessTheAnimeState({ hideHentai }).then(async (state) => {
        // Only what the page will draw: the rows of a game in progress, so they
        // appear with their pictures, or the setup screen's poster pair.
        if (state.game) {
          await preloadPictures(
            state.game.rows.map((row) => row.pictureUrl),
            'row',
            PRELOAD_WAIT_MS,
          )
        } else {
          await preloadPictures(state.pictures, 'tile', PRELOAD_WAIT_MS)
        }
        return state
      }),
  )

  // Seeded from what was last played with in this browser, and from the history
  // entry's snapshot on Back, so the settings survive the board and back.
  const [initialSettings] = useState(() => readStoredSettings(SETTINGS_KEY))
  const [chosen, setSettings] = useRestorableState<GameLobbySettings>('settings', initialSettings)
  // The game that has just ended on this entry, if one has: the rows and the answer,
  // restored with the rest of the entry. A fresh visit starts with none, so a new
  // trip to the page finds the setup screen, and a reload does too.
  const [finished, setFinished] = useRestorableState<FinishedGuessGame | null>('finished', null)
  const years = data?.years ?? null
  const settings = useMemo(() => withinYears(chosen, years), [chosen, years])

  function handleStarted(game: GuessTheAnimeGameDto, played: GameLobbySettings) {
    writeStoredSettings(SETTINGS_KEY, played)
    setFinished(null)
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  // A guess has landed: the read holds its row, so Back restores the board with it.
  function handleGuessed(row: GuessTheAnimeRowDto) {
    setData((prev) => {
      if (prev === null || prev.game === null || prev.game.rows.some((r) => r.animeId === row.animeId)) return prev
      return { ...prev, game: { ...prev.game, rows: [...prev.game.rows, row] } }
    })
  }

  // A new game with the settings this one was played with, which are the ones the
  // setup screen last played with. Throws on a refusal, which the board reports.
  async function handlePlayAgain() {
    handleStarted(await startGuessTheAnime({ ...settings, hideHentai }), settings)
  }

  // A game has ended: the server holds none now, and this entry holds the one just played.
  function handleFinished(rows: GuessTheAnimeRowDto[], over: GuessOver) {
    const game = data?.game
    if (game === null || game === undefined) return
    setFinished({ game: { id: game.id, maxGuesses: game.maxGuesses }, rows, over, added: false })
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
  const shown = finished !== null ? { ...finished.game, rows: finished.rows } : (data?.game ?? null)

  return (
    <div className="guess-the-anime-page">
      {data !== null && shown !== null && (
        <GuessBoard
          key={shown.id}
          game={shown}
          finished={finished}
          onlyMyList={settings.list === 'OnlyMyList'}
          onGuessed={handleGuessed}
          onFinished={handleFinished}
          onAdded={handleAdded}
          onLeave={handleLeave}
          onPlayAgain={handlePlayAgain}
          onStale={() => void reload()}
        />
      )}
      {data !== null && shown === null && (
        <GameLobby
          game={GUESS_THE_ANIME}
          art={<PosterPair pictures={data.pictures} size="lobby" badge={GUESS_THE_ANIME.badge} />}
          years={data.years}
          settings={settings}
          onSettingsChange={setSettings}
          readPool={getGuessTheAnimePool}
          start={startGuessTheAnime}
          onStarted={handleStarted}
        />
      )}
      {data === null && failed && <LoadFailedNotice what="Guess the Anime" onRetry={retry} />}
      <LoadingNotice active={data === null && !failed} className="guess-the-anime-page__loading" />
    </div>
  )
}
