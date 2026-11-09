import { useEffect, useRef, useState } from 'react'
import type { ThisOrThatCardDto } from '../../api/types.ts'
import { preloadPictures } from '../../utils/preloadPictures.ts'
import '../Games/GameButton.css'
import {
  choose,
  isPlaced,
  nextPivotIndexes,
  pivotIndex,
  picksLeftAtMost,
  placeOf,
  startPlacement,
  undoChoice,
  type PlacementOutcome,
} from './halving.ts'
import { ThisOrThatPair } from './ThisOrThatPair.tsx'
import { PICK_MARK_MS } from './thisOrThatTiming.ts'
import './ThisOrThatPlacement.css'

/** What the placement needs of a row of the ranking pop-up. */
export type PlacementMember = {
  animeId: number
  title: string
  englishTitle: string | null
  pictureUrl: string | null
}

type ThisOrThatPlacementProps = {
  /** The anime being placed. */
  anime: PlacementMember
  /** Every other row of the score, in the order the editor shows them now, unsaved reorders included. */
  others: PlacementMember[]
  /** The place is found: the index among the other rows, which is also the index the anime ends up at. */
  onPlaced: (index: number) => void
  /** Esc, Cancel: back to the list with nothing moved. */
  onCancel: () => void
}

// Longest a picture is waited on: a picture that isn't in by then isn't going to be.
const PRELOAD_LIMIT_MS = 10000

const prefersReducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

// A row has no type or year, so a card made of one shows neither.
function toCard(member: PlacementMember): ThisOrThatCardDto {
  return { ...member, mediaType: null, year: null }
}

// Placing one anime in the ranking pop-up (anime-ranking "Placing one anime by
// picking"; add-this-or-that-ranking design.md D7). It runs here in the browser
// because the pop-up already holds the score's rows in the order on screen, and
// the result is one ordinary reorder that the editor's own auto-save writes. The
// anime stays on one side for every pick, chosen at random when the placement
// begins; only the other card changes. Nothing is moved until the place is found,
// and Esc, Cancel or ✕ leave the list as it was.
export function ThisOrThatPlacement({ anime, others, onPlaced, onCancel }: ThisOrThatPlacementProps) {
  const [placement, setPlacement] = useState(() => startPlacement(others.length))
  const [animeOnLeft] = useState(() => Math.random() < 0.5)
  const [pickedId, setPickedId] = useState<number | null>(null)
  const [locked, setLocked] = useState(false)
  const lockedRef = useRef(false)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)

  const onCancelRef = useRef(onCancel)
  onCancelRef.current = onCancel

  // Esc is claimed here, in the capture phase as a drag claims it in the editor, so it
  // cancels the placement before the Modal's bubble-phase listener can close the editor.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      event.stopPropagation()
      onCancelRef.current()
    }
    document.addEventListener('keydown', onKeyDown, { capture: true })
    return () => document.removeEventListener('keydown', onKeyDown, { capture: true })
  }, [])

  // The Place button that led here is gone: keep keyboard focus inside the view.
  useEffect(() => {
    rootRef.current?.focus({ preventScroll: true })
    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current)
    }
  }, [])

  // The rows the next pair could show, whichever way the pick goes, start downloading
  // while this pair is on screen.
  useEffect(() => {
    const urls = nextPivotIndexes(placement).map((index) => others[index]?.pictureUrl)
    if (urls.length > 0) void preloadPictures(urls, 'card', PRELOAD_LIMIT_MS)
  }, [placement, others])

  if (isPlaced(placement)) return null

  const pivot = others[pivotIndex(placement.lo, placement.hi)]
  const left = animeOnLeft ? anime : pivot
  const right = animeOnLeft ? pivot : anime
  const picksLeft = picksLeftAtMost(placement)

  // winnerId null is Can't decide. The card is marked for a moment and takes no further
  // pick, so a double click counts once; then the range narrows. Under reduced motion
  // there is no moment to wait out.
  function pick(winnerId: number | null) {
    if (lockedRef.current) return
    lockedRef.current = true
    setLocked(true)
    setPickedId(winnerId)
    const outcome: PlacementOutcome = winnerId === null ? 'cant-decide' : winnerId === anime.animeId ? 'above' : 'below'
    const finish = () => {
      timerRef.current = null
      lockedRef.current = false
      setLocked(false)
      setPickedId(null)
      const next = choose(placement, outcome)
      if (isPlaced(next)) onPlaced(placeOf(next))
      else setPlacement(next)
    }
    if (prefersReducedMotion()) finish()
    else timerRef.current = setTimeout(finish, PICK_MARK_MS)
  }

  function undo() {
    if (lockedRef.current || placement.history.length === 0) return
    setPlacement(undoChoice(placement))
  }

  return (
    <div ref={rootRef} className="this-or-that-placement" tabIndex={-1}>
      <ThisOrThatPair
        pair={{ left: toCard(left), right: toCard(right) }}
        size="overlay"
        locked={locked}
        pickedId={pickedId}
        keysActive
        canUndo={placement.history.length > 0}
        onPick={pick}
        onCantDecide={() => pick(null)}
        onUndo={undo}
      />
      <p className="this-or-that-placement__left">
        up to {picksLeft} {picksLeft === 1 ? 'pick' : 'picks'} left
      </p>
      <div className="this-or-that-placement__actions">
        <button type="button" className="game-outline-button" aria-disabled={locked} onClick={() => pick(null)}>
          Can't decide
        </button>
        <button
          type="button"
          className="game-outline-button"
          aria-disabled={locked || placement.history.length === 0}
          onClick={undo}
        >
          Undo
        </button>
        <button type="button" className="game-outline-button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </div>
  )
}
