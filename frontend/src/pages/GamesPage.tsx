import { getGuessTheAnimeState, getHigherOrLowerState } from '../api/client.ts'
import type { GuessTheAnimeStateDto, HigherOrLowerStateDto } from '../api/types.ts'
import { GUESS_THE_ANIME, HIGHER_OR_LOWER } from '../components/Games/games.ts'
import { GameTile } from '../components/Games/GameTile.tsx'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { useContentFilter } from '../context/ContentFilterContext.tsx'
import { usePageData } from '../hooks/usePageData.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './GamesPage.css'

// The games available, one tile each (games-page). Only games that exist are
// drawn, with no placeholder for one still to come. Each game's own state holds
// its Continue progress and the two posters for its tile, so the page reads both
// at once inside its one `usePageData` and needs no route of its own
// (add-guess-the-anime design.md D3: a read takes about 12ms, so the two in a
// `Promise.all` cost nothing over one). It is keyed on Hide NSFW, which shapes
// the posters. The read waits a moment for the four posters to download, so the
// tiles appear with their pictures instead of drawing them in a beat later; a
// failed read fails the page, which shows the shared notice.
// The longest the page waits for its pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200

type GamesData = {
  higherOrLower: HigherOrLowerStateDto
  guessTheAnime: GuessTheAnimeStateDto
}

export function GamesPage() {
  const { hideHentai } = useContentFilter()
  const { data, failed, retry } = usePageData<GamesData>(`games:${hideHentai}`, async () => {
    const [higherOrLower, guessTheAnime] = await Promise.all([
      getHigherOrLowerState({ hideHentai }),
      getGuessTheAnimeState({ hideHentai }),
    ])
    await preloadPictures([...higherOrLower.pictures, ...guessTheAnime.pictures], 'tile', PRELOAD_WAIT_MS)
    return { higherOrLower, guessTheAnime }
  })

  const streak =
    data?.higherOrLower.game == null ? null : (
      <>
        Streak <b>{data.higherOrLower.game.streak.toLocaleString('en-US')}</b>
      </>
    )

  // The guess a game in progress is on: one past the rows made.
  const guess =
    data?.guessTheAnime.game == null ? null : (
      <>
        Guess <b>{data.guessTheAnime.game.rows.length + 1}</b> of {data.guessTheAnime.game.maxGuesses}
      </>
    )

  return (
    <div className="games-page">
      <div className="games-page__header">
        <h1 className="games-page__title">Games</h1>
      </div>

      {data && (
        <div className="games-page__grid">
          <GameTile game={HIGHER_OR_LOWER} pictures={data.higherOrLower.pictures} progress={streak} />
          <GameTile game={GUESS_THE_ANIME} pictures={data.guessTheAnime.pictures} progress={guess} />
        </div>
      )}
      {failed && <LoadFailedNotice what="the games" onRetry={retry} />}

      <LoadingNotice active={data === null && !failed} className="games-page__loading" />
    </div>
  )
}
