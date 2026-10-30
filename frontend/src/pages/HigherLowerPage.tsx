import { useMemo, useState } from 'react'
import { getHigherOrLowerState } from '../api/client.ts'
import type { HigherOrLowerGameDto, HigherOrLowerStateDto } from '../api/types.ts'
import { HigherLowerLobby, type HigherOrLowerResult } from '../components/Games/HigherLowerLobby.tsx'
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
import './HigherLowerPage.css'

// Higher or Lower (add-games-higher-or-lower design.md D10). One read of the
// game's state, then the table while a game is in progress and the setup screen
// otherwise. The game itself lives on the server, so the page only carries what
// it needs to draw: the read (kept current as rounds advance, so Back restores
// the round the game is on), the setup screen's settings, and the last game's
// result, which is in memory only on purpose: leaving the page or reloading
// loses it, because nothing about a finished game is kept.
export function HigherLowerPage() {
  const { hideHentai } = useContentFilter()
  const { data, setData, reload, failed, retry } = usePageData<HigherOrLowerStateDto>(
    `higher-or-lower:${hideHentai}`,
    () => getHigherOrLowerState({ hideHentai }),
  )

  // Seeded from what was last played with in this browser, and from the history
  // entry's snapshot on Back, so the settings survive the table and back.
  const [initialSettings] = useState(readStoredSettings)
  const [chosen, setSettings] = useRestorableState<HigherOrLowerLobbySettings>('settings', initialSettings)
  const years = data?.years ?? null
  const settings = useMemo(() => withinYears(chosen, years), [chosen, years])

  const [result, setResult] = useState<HigherOrLowerResult | null>(null)

  function handleStarted(game: HigherOrLowerGameDto, played: HigherOrLowerLobbySettings) {
    writeStoredSettings(played)
    setResult(null)
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  function handleAdvanced(game: HigherOrLowerGameDto) {
    setData((prev) => (prev === null ? prev : { ...prev, game }))
  }

  function handleEnded(ended: HigherOrLowerResult) {
    setResult(ended)
    setData((prev) => (prev === null ? prev : { ...prev, game: null }))
  }

  return (
    <div className="higher-lower-page">
      {data !== null && data.game !== null && (
        <HigherLowerTable
          game={data.game}
          onlyMyList={settings.list === 'OnlyMyList'}
          onAdvanced={handleAdvanced}
          onEnded={handleEnded}
          onStale={() => void reload()}
        />
      )}
      {data !== null && data.game === null && (
        <HigherLowerLobby
          state={data}
          settings={settings}
          onSettingsChange={setSettings}
          result={result}
          onStarted={handleStarted}
        />
      )}
      {data === null && failed && <LoadFailedNotice what="Higher or Lower" onRetry={retry} />}
      <LoadingNotice active={data === null && !failed} className="higher-lower-page__loading" />
    </div>
  )
}
