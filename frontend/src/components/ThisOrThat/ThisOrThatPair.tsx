import { useEffect, useRef, useState } from 'react'
import type { ThisOrThatPairDto } from '../../api/types.ts'
import { pickDisplayTitle } from '../../utils/anime.ts'
import { VsBadge } from '../Games/VsBadge.tsx'
import { ThisOrThatCard } from './ThisOrThatCard.tsx'
import { CARD_FADE_MS } from './thisOrThatTiming.ts'
import './ThisOrThatPair.css'

type ThisOrThatPairProps = {
  pair: ThisOrThatPairDto
  /** `page` sizes the cards from the window; `overlay` sizes them for the ranking pop-up. */
  size: 'page' | 'overlay'
  /** A pick is on its way or marked: no further pick, no hover. */
  locked: boolean
  /** The card that was picked, while it is marked. */
  pickedId: number | null
  /** The keys act only while this holds. Off while the content is held for another score, or a confirm is up. */
  keysActive: boolean
  canUndo: boolean
  onPick: (animeId: number) => void
  onCantDecide: () => void
  onUndo: () => void
}

type Sides = { left: boolean; right: boolean }
const NO_SIDES: Sides = { left: false, right: false }

// The keys (the left and right arrows, down, Backspace) are left alone where they
// belong to something else, as in Higher or Lower (add-games-higher-or-lower
// design.md D16): a modifier held (Alt+left is the browser's Back), a text field or
// dropdown with focus, or a dialog other than the one this pair sits in. The pair
// in the ranking pop-up is inside the pop-up's own dialog, so that one doesn't count,
// and neither does a dialog the pop-up was opened over (the entry editor's Rank opens
// it on top of the editor, which stays mounted beneath): that one comes earlier in
// the page than the pop-up's, and only a dialog above the pair's own takes the keys.
function keyIsSpoken(event: KeyboardEvent, root: HTMLElement | null): boolean {
  if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return true
  const ownDialog = root?.closest('[role="dialog"]') ?? null
  for (const dialog of document.querySelectorAll('[role="dialog"]')) {
    if (root === null) return true
    if (dialog.contains(root)) continue
    if (ownDialog !== null && dialog.compareDocumentPosition(ownDialog) & Node.DOCUMENT_POSITION_FOLLOWING) continue
    return true
  }
  const target = event.target
  return (
    target instanceof HTMLElement &&
    (target.isContentEditable || target.closest('input, textarea, select, [role="listbox"], [role="menu"]') !== null)
  )
}

// The two cards of a This or That pair with the VS medallion between them
// (this-or-that "A pair shows two anime side by side"), shared by the page and the
// ranking pop-up's placement view (add-this-or-that-ranking design.md D11). It
// draws what it is given and reports what is asked of it: the parent owns the lock
// and the mark. It owns the keys, the fade of whichever side's anime changed, and
// the polite announcement of each new pair.
export function ThisOrThatPair({ pair, size, locked, pickedId, keysActive, canUndo, onPick, onCantDecide, onUndo }: ThisOrThatPairProps) {
  const rootRef = useRef<HTMLDivElement>(null)

  // Which side's anime changed from the pair drawn before this one. Adjusted while
  // rendering, so the changed card's very first frame is already the fading one.
  const [seen, setSeen] = useState(pair)
  const [entering, setEntering] = useState<Sides>(NO_SIDES)
  if (pair !== seen) {
    setSeen(pair)
    setEntering({
      left: pair.left.animeId !== seen.left.animeId,
      right: pair.right.animeId !== seen.right.animeId,
    })
  }
  useEffect(() => {
    if (!entering.left && !entering.right) return
    const timer = setTimeout(() => setEntering(NO_SIDES), CARD_FADE_MS + 50)
    return () => clearTimeout(timer)
  }, [entering])

  // The latest pair, lock and callbacks are read through a ref, so the listener is
  // attached once.
  const keyRef = useRef<(key: string) => boolean>(() => false)
  keyRef.current = (key) => {
    if (!keysActive || locked) return false
    if (key === 'ArrowLeft') onPick(pair.left.animeId)
    else if (key === 'ArrowRight') onPick(pair.right.animeId)
    else if (key === 'ArrowDown') onCantDecide()
    else if (key === 'Backspace' && canUndo) onUndo()
    else return false
    return true
  }
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight' && event.key !== 'ArrowDown' && event.key !== 'Backspace') return
      if (event.repeat || keyIsSpoken(event, rootRef.current)) return
      // A bare Backspace is claimed even when there is nothing to undo or a pick is in
      // flight: a browser that still reads it as Back would otherwise leave the page, or
      // close the pop-up, in the middle of a ranking.
      if (keyRef.current(event.key) || event.key === 'Backspace') event.preventDefault()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  // The announcement is set after the pair is on screen rather than rendered with
  // it, so a screen reader hears the first pair of a ranking as well as the rest.
  const text = `${pickDisplayTitle(pair.left.title, pair.left.englishTitle)} or ${pickDisplayTitle(pair.right.title, pair.right.englishTitle)}`
  const [announcement, setAnnouncement] = useState('')
  useEffect(() => setAnnouncement(text), [text])

  return (
    <div ref={rootRef} className={`this-or-that-pair this-or-that-pair--${size}`}>
      <ThisOrThatCard
        card={pair.left}
        locked={locked}
        marked={pickedId === pair.left.animeId}
        entering={entering.left}
        onPick={() => onPick(pair.left.animeId)}
      />
      <div className="this-or-that-pair__vs">
        <VsBadge size="table" />
      </div>
      <ThisOrThatCard
        card={pair.right}
        locked={locked}
        marked={pickedId === pair.right.animeId}
        entering={entering.right}
        onPick={() => onPick(pair.right.animeId)}
      />
      <p className="visually-hidden" aria-live="polite">
        {announcement}
      </p>
    </div>
  )
}
