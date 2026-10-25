import { useRef } from 'react'
import type { DragEvent, MouseEvent } from 'react'

// Pointer travel, in pixels, before a press counts as a drag rather than a
// click.
const DRAG_THRESHOLD_PX = 3

type UseDragScrollOptions = {
  // Whether the row can scroll at all. While it can't (every tile fits), a
  // press is left to behave natively, so a click that wanders a few pixels
  // still lands. Callers already track this to show the grab cursor.
  enabled: boolean
  // Called when a press ends (button released, or the pointer left the row),
  // with whether it had become a drag. Currently watching settles on a whole
  // card here.
  onRelease?: (dragged: boolean) => void
}

// A press anywhere in the row that starts on a field of text should place or
// select text, not scroll: the inline episode count's input is the case.
function isTextField(target: EventTarget): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || target.closest('input, textarea, select') !== null)
}

// Mouse drag-to-scroll for a horizontal row: a mouse-down on the row starts
// tracking, mouse-move scrolls it and flags a drag once the pointer has
// moved past DRAG_THRESHOLD_PX, and the click that ends a drag is swallowed
// for every descendant, so a drag never also opens a card or presses a
// button. The flag resets on the next mouse-down, so a plain click works.
//
// Mouse-only: touch and trackpad keep the browser's native scrolling.
// `onDragStart` is cancelled so a native link or image drag can't start from
// inside the row. Spread the returned handlers onto the scrolling element;
// they read it from the event, so the hook needs no ref. Used by Profile's
// poster strips, the Currently watching carousel and the Series page's
// main line.
export function useDragScroll({ enabled, onRelease }: UseDragScrollOptions) {
  const drag = useRef({ isDown: false, startX: 0, scrollLeft: 0, dragged: false })

  function onMouseDown(event: MouseEvent<HTMLElement>) {
    // Whatever the last press left behind is stale from here on.
    drag.current = { isDown: false, startX: 0, scrollLeft: 0, dragged: false }
    if (!enabled || event.button !== 0 || isTextField(event.target)) return
    drag.current = { isDown: true, startX: event.pageX, scrollLeft: event.currentTarget.scrollLeft, dragged: false }
  }

  function onMouseMove(event: MouseEvent<HTMLElement>) {
    const state = drag.current
    if (!state.isDown) return
    event.preventDefault()
    const delta = event.pageX - state.startX
    if (Math.abs(delta) > DRAG_THRESHOLD_PX) state.dragged = true
    event.currentTarget.scrollLeft = state.scrollLeft - delta
  }

  function onMouseUp() {
    const state = drag.current
    if (!state.isDown) return
    state.isDown = false
    onRelease?.(state.dragged)
  }

  function onClickCapture(event: MouseEvent<HTMLElement>) {
    if (!drag.current.dragged) return
    event.preventDefault()
    event.stopPropagation()
  }

  function onDragStart(event: DragEvent<HTMLElement>) {
    event.preventDefault()
  }

  return { onMouseDown, onMouseMove, onMouseUp, onMouseLeave: onMouseUp, onClickCapture, onDragStart }
}
