import { getHigherOrLowerState } from '../api/client.ts'
import type { HigherOrLowerStateDto } from '../api/types.ts'
import { GAMES } from '../components/Games/games.ts'
import { GameTile } from '../components/Games/GameTile.tsx'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './GamesPage.css'

// The games available, one tile each (games-page). Only games that exist are
// drawn, with no placeholder for one still to come. The one read is Higher or
// Lower's own state, which holds its Continue streak and the two posters for
// its tile, so the page needs no route of its own; when a second game exists a
// `GET /api/games` can fold the two together (add-games-higher-or-lower design.md
// D3). It is keyed on Hide NSFW, which shapes the posters. The read waits a
// moment for the two posters to download, so the tile appears with its pictures
// instead of drawing them in a beat later.
// The longest the page waits for its pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200

export function GamesPage() {
  const { hideHentai } = useContentFilter()
  const { data, failed, retry } = usePageData<HigherOrLowerStateDto>(`higher-or-lower:${hideHentai}`, () =>
    getHigherOrLowerState({ hideHentai }).then(async (state) => {
      await preloadPictures(state.pictures, 'tile', PRELOAD_WAIT_MS)
      return state
    }),
  )

  return (
    <div className="games-page">
      <div className="games-page__header">
        <h1 className="games-page__title">Games</h1>
      </div>

      {data && (
        <div className="games-page__grid">
          {GAMES.map((game) => (
            <GameTile key={game.to} game={game} pictures={data.pictures} streak={data.game?.streak ?? null} />
          ))}
        </div>
      )}
      {failed && <LoadFailedNotice what="the games" onRetry={retry} />}

      <LoadingNotice active={data === null && !failed} className="games-page__loading" />
    </div>
  )
}
