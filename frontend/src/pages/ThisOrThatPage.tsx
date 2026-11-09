import { useEffect, useRef } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { getThisOrThatScore, getThisOrThatScores } from '../api/client.ts'
import type { ThisOrThatScoreStateDto, ThisOrThatScoreSummaryDto } from '../api/types.ts'
import { LoadFailedNotice } from '../components/LoadFailedNotice.tsx'
import { LoadingNotice } from '../components/LoadingNotice.tsx'
import { ScoreTabs } from '../components/ScoreTabs.tsx'
import { ThisOrThatScore } from '../components/ThisOrThat/ThisOrThatScore.tsx'
import { useDelayedFlag } from '../hooks/useDelayedFlag.ts'
import { useHeldData } from '../hooks/useHeldData.ts'
import { usePageData } from '../hooks/usePageData.ts'
import { preloadPictures } from '../utils/preloadPictures.ts'
import './ThisOrThatPage.css'

// This or That (add-this-or-that-ranking design.md D11). The page ranks one score
// of mine at a time, named by the last part of its address (/ranking/8). It has no
// navbar link: the ranking pop-up leads here.
//
// Two reads, as the period pages have: the tabs (`ranking:tabs`) and the selected
// score's state (`ranking:{score}`). With a score in the address they run side by
// side, and the page draws once both are in. At /ranking, or at a score that is not
// offered, the score is chosen once the tabs arrive (the highest with a ranking in
// progress, else the highest offered) and the address is replaced with it. A tab
// replaces the address too, so Back leaves the page, and React Router keeps the page
// mounted across it: the previous score's content is held on screen, muted and
// inert after the loading delay, until the new score's arrives (useHeldData).

// The longest the page waits for the first pair's pictures; a slow one is drawn when it comes.
const PRELOAD_WAIT_MS = 1200
// Long past anything on screen: a picture that isn't in by then isn't going to be.
const UPCOMING_PRELOAD_LIMIT_MS = 10000

// The score the address names, if it names a whole number.
function parseScore(param: string | undefined): number | null {
  return param !== undefined && /^\d+$/.test(param) ? Number(param) : null
}

// The score the page shows: the address's when that score is offered, else the
// highest with a ranking in progress, else the highest offered. Null when nothing is
// offered.
function chooseScore(requested: number | null, offered: ThisOrThatScoreSummaryDto[]): number | null {
  if (offered.length === 0) return null
  if (requested !== null && offered.some((s) => s.score === requested)) return requested
  const highestFirst = [...offered].sort((a, b) => b.score - a.score)
  return (highestFirst.find((s) => s.inProgress) ?? highestFirst[0]).score
}

// A score's state, with its first pair's pictures waited for (up to a moment) so the
// page opens with them drawn, and the pictures of whatever could follow started with
// no wait.
async function readScore(score: number): Promise<ThisOrThatScoreStateDto> {
  const state = await getThisOrThatScore(score)
  const pair = state.run?.pair
  if (state.run !== null && state.run.upcomingPictureUrls.length > 0) {
    void preloadPictures(state.run.upcomingPictureUrls, 'card', UPCOMING_PRELOAD_LIMIT_MS)
  }
  if (pair) await preloadPictures([pair.left.pictureUrl, pair.right.pictureUrl], 'card', PRELOAD_WAIT_MS)
  return state
}

export function ThisOrThatPage() {
  const { score: scoreParam } = useParams()
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const requested = parseScore(scoreParam)

  const tabsRead = usePageData('ranking:tabs', getThisOrThatScores)
  const tabs = tabsRead.data

  // Until the tabs say otherwise the address's score is read, so the two reads overlap.
  // At /ranking there is no score to read yet: the key waits and its read is empty.
  const selected = tabs === null ? requested : chooseScore(requested, tabs.scores)
  const scoreRead = usePageData<ThisOrThatScoreStateDto | null>(`ranking:${selected ?? 'none'}`, () =>
    selected === null ? Promise.resolve(null) : readScore(selected),
  )
  const { shown, stale } = useHeldData(scoreRead.data, scoreRead.failed)
  const holdingMuted = useDelayedFlag(stale)

  // The address names the score shown, or no score when nothing is offered. Replaced
  // rather than pushed, and `keepScroll` so a replace does not read as a fresh visit
  // and jump the page to the top, as the Airing page's week steps do.
  const target = tabs === null ? null : selected === null ? '/ranking' : `/ranking/${selected}`
  useEffect(() => {
    if (target !== null && pathname !== target) navigate(target, { replace: true, state: { keepScroll: true } })
  }, [target, pathname, navigate])

  // An action's answer lands on the score it was made on. A pick still in flight when
  // another tab is chosen answers after the page has moved on, and must not be written
  // over the new score's read.
  const selectedRef = useRef(selected)
  selectedRef.current = selected
  const { setData, reload } = scoreRead
  function handleState(next: ThisOrThatScoreStateDto) {
    if (next.score === selectedRef.current) setData(next)
  }

  function chooseTab(score: number) {
    if (score !== selected) navigate(`/ranking/${score}`, { replace: true, state: { keepScroll: true } })
  }

  function retryAll() {
    tabsRead.retry()
    scoreRead.retry()
  }

  // The selected tab follows the score's own state, which is newer than the tabs read
  // (a ranking just started, a list change taken in).
  const tabItems = (tabs?.scores ?? [])
    .slice()
    .sort((a, b) => b.score - a.score)
    .map((s) => {
      const live = shown !== null && shown.score === s.score ? shown : null
      return { score: s.score, count: live ? live.count : s.count, marked: live ? live.run !== null : s.inProgress }
    })

  const failed = tabsRead.failed || (tabs !== null && scoreRead.failed && shown === null)

  return (
    <div className="this-or-that-page">
      <h1 className="this-or-that-page__title">This or That</h1>

      {failed && <LoadFailedNotice what="This or That" onRetry={retryAll} />}
      {!failed && (tabs === null || (tabs.scores.length > 0 && shown === null)) && (
        <LoadingNotice className="this-or-that-page__loading" />
      )}
      {!failed && tabs !== null && tabs.scores.length === 0 && <p className="this-or-that-page__empty">Nothing to rank yet.</p>}

      {!failed && tabs !== null && tabs.scores.length > 0 && shown !== null && (
        <>
          <ScoreTabs tabs={tabItems} selected={selected} onSelect={chooseTab} />
          <div className={holdingMuted ? 'this-or-that-page__body held-content--muted' : 'this-or-that-page__body'} inert={stale}>
            <ThisOrThatScore
              key={shown.score}
              state={shown}
              stale={stale}
              onState={handleState}
              onReread={reload}
              onTabsChanged={() => void tabsRead.reload()}
            />
          </div>
        </>
      )}
    </div>
  )
}
