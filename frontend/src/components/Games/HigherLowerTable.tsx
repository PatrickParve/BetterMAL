import { useEffect, useRef, useState } from 'react'
import { ApiError, endHigherOrLower, pickHigherOrLower, updateEntry } from '../../api/client.ts'
import type { HigherOrLowerCardDto, HigherOrLowerGameDto, HigherOrLowerPickDto } from '../../api/types.ts'
import { useActionFailure } from '../../context/ActionFailureContext.tsx'
import { displayPictureUrl, pickDisplayTitle } from '../../utils/anime.ts'
import { Confetti } from './Confetti.tsx'
import { HigherLowerCard, type HigherLowerCardMotion } from './HigherLowerCard.tsx'
import type { HigherOrLowerResult } from './HigherLowerLobby.tsx'
import {
  CORRECT_HOLD_MS,
  MILESTONE_HOLD_MS,
  REVEAL_MS,
  SWAP_MS,
  WRONG_HOLD_MS,
} from './higherLowerTiming.ts'
import { VsBadge } from './VsBadge.tsx'
import './HigherLowerTable.css'

// choosing: two cards waiting for a pick. sending: the pick is on its way.
// revealing: the verdict is up (the scores count up first), and the game is
// about to move on or end. swapping: the cards change places.
type Phase = 'choosing' | 'sending' | 'revealing' | 'swapping'

type HigherLowerTableProps = {
  game: HigherOrLowerGameDto
  /** Under Only my list every card would carry the In my list mark, so none does. */
  onlyMyList: boolean
  /** The next round is on the table: the page keeps its read current. */
  onAdvanced: (game: HigherOrLowerGameDto) => void
  /** The game is over, whichever way: back to the setup screen with how it went. */
  onEnded: (result: HigherOrLowerResult) => void
  /** A pick or End game found the game moved on elsewhere: re-read it, with no notice. */
  onStale: () => void
}

const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches
const forcedColors = () => window.matchMedia('(forced-colors: active)').matches

const formatScore = (score: number) => score.toFixed(2)

// Left and right arrows pick, except where they belong to something else: a
// modifier held (Alt+left is the browser's Back), a text field or dropdown with
// focus, or an open overlay (design.md D16).
function arrowKeyIsSpoken(event: KeyboardEvent): boolean {
  if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return true
  if (document.querySelector('[role="dialog"]')) return true
  const target = event.target
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || target.closest('input, textarea, select, [role="listbox"], [role="menu"]') !== null)
  )
}

// What a screen reader hears when a verdict appears: whether the pick was right
// (naming a milestone), both scores, and the streak.
function verdictAnnouncement(game: HigherOrLowerGameDto, res: HigherOrLowerPickDto): string {
  const left = pickDisplayTitle(game.left.title, game.left.englishTitle)
  const right = pickDisplayTitle(game.right.title, game.right.englishTitle)
  const verdict = !res.correct
    ? 'Wrong.'
    : res.milestoneLevel !== null
      ? `${res.streak.toLocaleString('en-US')} in a row! Correct.`
      : 'Correct.'
  return `${verdict} ${left} ${formatScore(res.leftScore)}. ${right} ${formatScore(res.rightScore)}. Streak ${res.streak.toLocaleString('en-US')}.`
}

// The game in play (higher-or-lower, design.md D10). The server holds the game
// and judges every pick, so this is a small state machine that paces what it
// is told: lock on a pick, send it, count the scores up, show the verdict, hold,
// then swap the cards or end. Every timer is cleared on unmount, and the server
// has already moved on, so leaving mid-reveal loses nothing.
export function HigherLowerTable({ game, onlyMyList, onAdvanced, onEnded, onStale }: HigherLowerTableProps) {
  const reportFailure = useActionFailure()

  const [phase, setPhase] = useState<Phase>('choosing')
  const [picked, setPicked] = useState<number | null>(null)
  // The server's answer to the pick, from the moment it arrives until the next
  // round is committed.
  const [reveal, setReveal] = useState<HigherOrLowerPickDto | null>(null)
  const [verdictUp, setVerdictUp] = useState(false)
  const [confettiLevel, setConfettiLevel] = useState<number | null>(null)
  const [enteringId, setEnteringId] = useState<number | null>(null)
  const [announcement, setAnnouncement] = useState('')
  const [ending, setEnding] = useState(false)
  // Anime added from a card in this visit, and the adds still in flight. The
  // card's own inMyList only changes when the next round is read.
  const [added, setAdded] = useState<ReadonlySet<number>>(() => new Set())
  const [adding, setAdding] = useState<ReadonlySet<number>>(() => new Set())

  const popupRef = useRef<HTMLDivElement>(null)
  const mountedRef = useRef(true)
  const busyRef = useRef(false)
  const timersRef = useRef(new Set<ReturnType<typeof setTimeout>>())

  useEffect(() => {
    mountedRef.current = true
    const timers = timersRef.current
    return () => {
      mountedRef.current = false
      for (const timer of timers) clearTimeout(timer)
      timers.clear()
    }
  }, [])

  const wait = (ms: number) =>
    new Promise<void>((resolve) => {
      const timer = setTimeout(() => {
        timersRef.current.delete(timer)
        resolve()
      }, ms)
      timersRef.current.add(timer)
    })

  async function pick(animeId: number) {
    if (busyRef.current) return
    busyRef.current = true
    setPhase('sending')
    setPicked(animeId)

    let res: HigherOrLowerPickDto
    try {
      res = await pickHigherOrLower(game.id, game.round, animeId)
    } catch (err) {
      if (!mountedRef.current) return
      // The round stays and both cards are available again. A game that moved
      // on elsewhere is re-read with no notice, since nothing was lost.
      busyRef.current = false
      setPicked(null)
      setPhase('choosing')
      if (err instanceof ApiError && (err.status === 404 || err.status === 409)) onStale()
      else reportFailure({ title: "Couldn't send your pick", reason: err instanceof ApiError ? err.reason : null })
      return
    }
    if (!mountedRef.current) return

    // The new poster loads while the verdict is up, so the swap draws one that's there.
    if (res.next?.right.pictureUrl) new Image().src = displayPictureUrl(res.next.right.pictureUrl, 'card')

    const animate = !prefersReducedMotion()
    setReveal(res)
    setPhase('revealing')
    if (animate) await wait(REVEAL_MS)

    setVerdictUp(true)
    setAnnouncement(verdictAnnouncement(game, res))
    if (res.milestoneLevel !== null && animate && !forcedColors()) setConfettiLevel(res.milestoneLevel)
    await wait(res.end === 'Wrong' ? WRONG_HOLD_MS : res.milestoneLevel !== null ? MILESTONE_HOLD_MS : CORRECT_HOLD_MS)

    if (res.next === null) {
      onEnded({ streak: res.streak, cleared: res.end === 'Cleared' })
      return
    }
    if (animate) {
      setPhase('swapping')
      await wait(SWAP_MS)
    }

    // The swap is done: the next round replaces this one in a single render.
    setEnteringId(animate ? res.next.right.animeId : null)
    setReveal(null)
    setVerdictUp(false)
    setConfettiLevel(null)
    setPicked(null)
    setPhase('choosing')
    busyRef.current = false
    onAdvanced(res.next)
    if (animate) {
      await wait(SWAP_MS)
      setEnteringId(null)
    }
  }

  async function endGame() {
    if (ending || phase === 'sending') return
    // A game the verdict has already ended is over on the server: say how it
    // went from what that verdict told us, rather than asking again.
    if (reveal?.end) {
      onEnded({ streak: reveal.streak, cleared: reveal.end === 'Cleared' })
      return
    }
    setEnding(true)
    try {
      const { streak } = await endHigherOrLower(game.id)
      onEnded({ streak, cleared: false })
    } catch (err) {
      if (!mountedRef.current) return
      setEnding(false)
      if (err instanceof ApiError && (err.status === 404 || err.status === 409)) onStale()
      else reportFailure({ title: "Couldn't end the game", reason: err instanceof ApiError ? err.reason : null })
    }
  }

  async function addToList(card: HigherOrLowerCardDto) {
    if (adding.has(card.animeId)) return
    setAdding((prev) => new Set(prev).add(card.animeId))
    try {
      await updateEntry(card.animeId, { status: 'PlanToWatch' })
      setAdded((prev) => new Set(prev).add(card.animeId))
    } catch (err) {
      // The card still offers Add to list, and the notice says why.
      reportFailure({
        title: `Couldn't add ${pickDisplayTitle(card.title, card.englishTitle)} to list`,
        reason: err instanceof ApiError ? err.reason : null,
      })
    } finally {
      setAdding((prev) => {
        const next = new Set(prev)
        next.delete(card.animeId)
        return next
      })
    }
  }

  // The arrow keys read the latest round and phase through a ref, so the
  // listener is attached once.
  const arrowPickRef = useRef<(side: 'left' | 'right') => void>(() => {})
  arrowPickRef.current = (side) => {
    if (phase !== 'choosing') return
    void pick(side === 'left' ? game.left.animeId : game.right.animeId)
  }
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return
      if (event.repeat || arrowKeyIsSpoken(event)) return
      event.preventDefault()
      arrowPickRef.current(event.key === 'ArrowLeft' ? 'left' : 'right')
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const locked = phase !== 'choosing'
  const milestone = reveal !== null && reveal.correct && reveal.milestoneLevel !== null
  const verdict = verdictUp && reveal !== null ? (reveal.correct ? 'correct' : 'wrong') : null

  // Which card stays and which goes, once the swap has begun.
  const nextLeftId = reveal?.next?.left.animeId
  const leftStays = nextLeftId === game.left.animeId
  const motionFor = (side: 'left' | 'right', card: HigherOrLowerCardDto): HigherLowerCardMotion | null => {
    if (phase === 'swapping') {
      const stays = side === 'left' ? leftStays : !leftStays
      if (!stays) return 'leaving'
      return side === 'right' ? 'to-left' : null
    }
    return card.animeId === enteringId ? 'entering' : null
  }

  function renderCard(side: 'left' | 'right') {
    const card = game[side]
    const known = side === 'left' ? card.score ?? reveal?.leftScore ?? null : card.score ?? reveal?.rightScore ?? null
    return (
      <HigherLowerCard
        key={card.animeId}
        card={card}
        score={known}
        counting={reveal !== null && card.score === null && !prefersReducedMotion()}
        verdict={verdict !== null && picked === card.animeId ? verdict : null}
        dimmed={verdict !== null && picked !== card.animeId}
        motion={motionFor(side, card)}
        locked={locked}
        inMyList={card.inMyList || added.has(card.animeId)}
        showListMark={!onlyMyList}
        adding={adding.has(card.animeId)}
        onPick={() => void pick(card.animeId)}
        onAdd={() => void addToList(card)}
      />
    )
  }

  const streak = verdictUp && reveal !== null ? reveal.streak : game.streak
  const streakClass =
    verdict === 'correct'
      ? ' higher-lower-table__streak-value--' + (milestone ? 'milestone' : 'bump')
      : ''

  return (
    <div
      className="higher-lower-table"
      data-verdict={verdict ?? ''}
      data-milestone={milestone && verdictUp ? 'true' : 'false'}
    >
      <h1 className="visually-hidden">Higher or Lower</h1>
      <div className="higher-lower-table__wash" aria-hidden="true" />

      <div className="higher-lower-table__hud">
        <button
          type="button"
          className="higher-lower-table__end"
          aria-disabled={ending || phase === 'sending'}
          onClick={() => void endGame()}
        >
          End game
        </button>
        <div className="higher-lower-table__streak">
          <span className="higher-lower-table__streak-label">Streak</span>
          <span className={`higher-lower-table__streak-value${streakClass}`}>{streak.toLocaleString('en-US')}</span>
        </div>
      </div>
      <p className="higher-lower-table__prompt">Which has the higher MAL score?</p>

      <div className="higher-lower-table__cards">
        {renderCard('left')}
        <div className={`higher-lower-table__vs${verdict !== null ? ' higher-lower-table__vs--hidden' : ''}`} key="vs">
          <VsBadge size="table" />
        </div>
        {renderCard('right')}
        {verdict !== null && reveal !== null && (
          <div
            key="popup"
            ref={popupRef}
            className={`higher-lower-table__popup higher-lower-table__popup--${milestone ? 'milestone' : verdict}`}
            aria-hidden="true"
          >
            {milestone
              ? `${reveal.streak.toLocaleString('en-US')} in a row!`
              : verdict === 'correct'
                ? '✓ Correct!'
                : '✕ Wrong'}
          </div>
        )}
      </div>

      {confettiLevel !== null && (
        <Confetti level={confettiLevel} originRef={popupRef} onDone={() => setConfettiLevel(null)} />
      )}
      <p className="visually-hidden" aria-live="polite">
        {announcement}
      </p>
    </div>
  )
}
