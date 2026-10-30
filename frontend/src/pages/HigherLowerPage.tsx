import { useMemo, useState } from 'react'
import { getHigherOrLowerState, startHigherOrLower } from '../api/client.ts'
import type { HigherOrLowerGameDto, HigherOrLowerStateDto } from '../api/types.ts'
import { HigherLowerLobby } from '../components/Games/HigherLowerLobby.tsx'
import { HigherLowerTable } from '../components/Games/HigherLowerTable.tsx'
import {
  readStoredSettings,
  withinYears,
  writeStoredSettings,
  type HigherOrLowerLobbySettings,
} from '../components/Games/higherLowerSettings.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { useRestorableState } from '../hooks/useRestorableState.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './HigherLowerPage.css'

// Higher or Lower (add-games-higher-or-lower design.md D10). One read of the
// game's state, then the table while a game is in progress and the setup screen
// otherwise. The game itself lives on the server, so the page only carries what
// it needs to draw: the read (kept current as rounds advance, so Back restores
// the round the game is on) and the setup screen's settings. A game that has
// ended stays on the table until I leave it, and nothing about it is kept.
// The longest the page waits for its pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200

export function HigherLowerPage() {
  const { hideHentai } = useContentFilter()
  const { data, setData, reload, failed, retry } = usePageData<HigherOrLowerStateDto>(
    `higher-or-lower:${hideHentai}`,
    () =>
      getHigherOrLowerState({ hideHentai }).then(async (state) => {
        // Only what the page will draw: the two cards of a game in progress, or the
        // setup screen's poster pair.
        if (state.game) await preloadPictures([state.game.left.pictureUrl, state.game.right.pictureUrl], 'card', PRELOAD_WAIT_MS)
        else await preloadPictures(state.pictures, 'tile', PRELOAD_WAIT_MS)
        return state
      }),
  )

  // Seeded from what was last played with in this browser, and from the history
  // entry's snapshot on Back, so the settings survive the table and back.
  const [initialSettings] = useState(readStoredSettings)
  const [chosen, setSettings] = useRestorableState<HigherOrLowerLobbySettings>('settings', initialSettings)
  const years = data?.years ?? null
  const settings = useMemo(() => withinYears(chosen, years), [chosen, years])

  function handleStarted(game: HigherOrLowerGameDto, played: HigherOrLowerLobbySettings) {
    writeStoredSettings(played)
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  function handleAdvanced(game: HigherOrLowerGameDto) {
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  // A new game with the settings this one was played with, which are the ones the
  // setup screen last played with. Throws on a refusal, which the table reports.
  async function handlePlayAgain() {
    const game = await startHigherOrLower({ ...settings, hideHentai })
    await preloadPictures([game.left.pictureUrl, game.right.pictureUrl], 'card', PRELOAD_WAIT_MS)
    handleStarted(game, settings)
  }

  // Over, or ended with End game: the server holds no game any more.
  function handleLeave() {
    setData((prev) => (prev === null ? prev : { ...prev, game: null }))
  }

  return (
    <div className="higher-lower-page">
      {data !== null && data.game !== null && (
        <HigherLowerTable
          key={data.game.id}
          game={data.game}
          onlyMyList={settings.list === 'OnlyMyList'}
          onAdvanced={handleAdvanced}
          onLeave={handleLeave}
          onPlayAgain={handlePlayAgain}
          onStale={() => void reload()}
        />
      )}
      {data !== null && data.game === null && (
        <HigherLowerLobby
          state={data}
          settings={settings}
          onSettingsChange={setSettings}
          onStarted={handleStarted}
        />
      )}
      {data === null && failed && <LoadFailedNotice what="Higher or Lower" onRetry={retry} />}
      <LoadingNotice active={data === null && !failed} className="higher-lower-page__loading" />
    </div>
  )
}
