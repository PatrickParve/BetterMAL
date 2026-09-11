import { useEffect, useRef } from 'react'
import type { AnimeUpdateDto } from '../../api/types.ts'

// A card counts as looked at once it has been on screen, whole, for about a
// second (store-seen-updates-on-server design.md D7).
const DWELL_MS = 1000
// Absorbs subpixel rounding: useCappedCardHeight sets max-height to a
// fractional sum, so the third card's bottom lands on the list's bottom
// only to within rounding.
const TOLERANCE_PX = 1

type Rect = { top: number; bottom: number; left: number; right: number }

function intersectRects(a: Rect, b: Rect): Rect {
  return {
    top: Math.max(a.top, b.top),
    bottom: Math.min(a.bottom, b.bottom),
    left: Math.max(a.left, b.left),
    right: Math.min(a.right, b.right),
  }
}

// The list's bounding rect intersected with every ancestor whose computed
// overflow clips, and with the window. Starts from the list's own rect
// (already the visible viewport of a scrolling list) and walks upward, so
// only clipping *above* the list narrows it — for the dropdown that's just
// the window, since nothing between the list and the window clips; for
// History it also picks up `.modal`, which scrolls at 85vh.
function computeVisibleArea(listNode: HTMLElement): Rect {
  const listRect = listNode.getBoundingClientRect()
  let area: Rect = { top: listRect.top, bottom: listRect.bottom, left: listRect.left, right: listRect.right }

  let ancestor = listNode.parentElement
  while (ancestor) {
    const style = getComputedStyle(ancestor)
    if (style.overflowX !== 'visible' || style.overflowY !== 'visible') {
      area = intersectRects(area, ancestor.getBoundingClientRect())
    }
    ancestor = ancestor.parentElement
  }

  return intersectRects(area, { top: 0, left: 0, right: window.innerWidth, bottom: window.innerHeight })
}

// Whole: the card is entirely inside the area. Fills: a card taller than the
// area covers it from above to below — the only way such a card can ever
// qualify.
function qualifies(cardRect: DOMRect, area: Rect): boolean {
  const whole = cardRect.top >= area.top - TOLERANCE_PX && cardRect.bottom <= area.bottom + TOLERANCE_PX
  const fills = cardRect.top <= area.top + TOLERANCE_PX && cardRect.bottom >= area.bottom - TOLERANCE_PX
  return whole || fills
}

// Geometric visibility tracking for both surfaces (store-seen-updates-on-server
// design.md D7): measures only unseen, unreported `li[data-update-id]` cards
// against the visible area, and runs a 1000ms dwell timer per qualifying
// card. Hover and following a card are handled separately by
// useSeenTracking's caller (design D8) — this hook only covers "on screen
// for about a second".
export function useSeenTracking(
  listNode: HTMLUListElement | null,
  items: AnimeUpdateDto[],
  isSeen: (item: AnimeUpdateDto) => boolean,
  report: (id: number) => void,
): void {
  const itemsRef = useRef(items)
  itemsRef.current = items
  const isSeenRef = useRef(isSeen)
  isSeenRef.current = isSeen
  const reportRef = useRef(report)
  reportRef.current = report
  const recomputeRef = useRef<() => void>(() => {})

  useEffect(() => {
    if (!listNode) return

    // Ids this hook instance has already reported — kept alongside isSeen
    // rather than instead of it, since a report doesn't retroactively
    // update `items` this render, and the tracker must never report the
    // same card twice while waiting for that to happen.
    const reported = new Set<number>()
    const dwellTimers = new Map<number, number>()
    let rafId: number | null = null

    function clearDwell(id: number) {
      const timer = dwellTimers.get(id)
      if (timer !== undefined) {
        window.clearTimeout(timer)
        dwellTimers.delete(id)
      }
    }

    function clearAllDwells() {
      for (const id of Array.from(dwellTimers.keys())) clearDwell(id)
    }

    function reportNow(id: number) {
      clearDwell(id)
      reported.add(id)
      reportRef.current(id)
    }

    function isReportable(id: number): boolean {
      if (reported.has(id)) return false
      const item = itemsRef.current.find((entry) => entry.id === id)
      if (!item) return false
      return !isSeenRef.current(item)
    }

    function recompute() {
      // A hidden tab pauses the dwell rather than letting it keep running
      // unseen (design D7); a re-qualifying card starts a fresh second once
      // the tab is visible again.
      if (document.visibilityState === 'hidden') {
        clearAllDwells()
        return
      }

      const area = computeVisibleArea(listNode!)
      const cards = Array.from(listNode!.querySelectorAll<HTMLElement>('li[data-update-id]'))
      const presentIds = new Set<number>()

      for (const card of cards) {
        const id = Number(card.dataset.updateId)
        presentIds.add(id)
        cardObserver.observe(card)

        if (!isReportable(id)) {
          clearDwell(id)
          continue
        }

        const qualifying = qualifies(card.getBoundingClientRect(), area)
        if (qualifying) {
          if (!dwellTimers.has(id)) {
            dwellTimers.set(id, window.setTimeout(() => reportNow(id), DWELL_MS))
          }
        } else {
          clearDwell(id)
        }
      }

      for (const id of Array.from(dwellTimers.keys())) {
        if (!presentIds.has(id)) clearDwell(id)
      }
    }

    recomputeRef.current = recompute

    function scheduleRecompute() {
      if (rafId !== null) return
      rafId = requestAnimationFrame(() => {
        rafId = null
        recompute()
      })
    }

    function findCardId(target: EventTarget | null): number | null {
      if (!(target instanceof Element)) return null
      const li = target.closest('li[data-update-id]')
      const idAttr = li?.getAttribute('data-update-id') ?? null
      return idAttr === null ? null : Number(idAttr)
    }

    // Real mouse movement, not a synthesised hover: after a scroll, browsers
    // re-run hover under a still pointer by sending boundary events, and
    // pointerenter would count every card wheel-scrolled past the cursor
    // (design D8). Touch has no hover and is excluded by pointerType.
    function handlePointerMove(event: PointerEvent) {
      if (event.pointerType !== 'mouse') return
      if (event.movementX === 0 && event.movementY === 0) return
      const id = findCardId(event.target)
      if (id !== null && isReportable(id)) reportNow(id)
    }

    // click covers a mouse click, a touch tap, and Enter on the focused
    // link in one listener; auxclick covers a middle-click into a new tab
    // (design D8). Attached natively on the list, so this runs during
    // bubbling before React's root-level handler runs the card's
    // onNavigate — the report is queued before that close unmounts the
    // surface.
    function handleFollow(event: Event) {
      const id = findCardId(event.target)
      if (id !== null && isReportable(id)) reportNow(id)
    }

    // Pictures loading, and the third card's fractional height, both move
    // cards after first render — a ResizeObserver on the list and its cards
    // catches that the same way useCappedCardHeight does.
    const cardObserver = new ResizeObserver(scheduleRecompute)
    cardObserver.observe(listNode)

    listNode.addEventListener('scroll', scheduleRecompute, { passive: true })
    // Catches History's `.modal` scrolling, which the list itself isn't a
    // descendant-scroll target of otherwise.
    document.addEventListener('scroll', scheduleRecompute, { capture: true, passive: true })
    window.addEventListener('resize', scheduleRecompute)
    document.addEventListener('visibilitychange', scheduleRecompute)
    listNode.addEventListener('pointermove', handlePointerMove)
    listNode.addEventListener('click', handleFollow)
    listNode.addEventListener('auxclick', handleFollow)

    recompute()

    return () => {
      if (rafId !== null) cancelAnimationFrame(rafId)
      clearAllDwells()
      cardObserver.disconnect()
      listNode.removeEventListener('scroll', scheduleRecompute)
      document.removeEventListener('scroll', scheduleRecompute, { capture: true })
      window.removeEventListener('resize', scheduleRecompute)
      document.removeEventListener('visibilitychange', scheduleRecompute)
      listNode.removeEventListener('pointermove', handlePointerMove)
      listNode.removeEventListener('click', handleFollow)
      listNode.removeEventListener('auxclick', handleFollow)
      recomputeRef.current = () => {}
    }
  }, [listNode])

  useEffect(() => {
    recomputeRef.current()
  }, [items])
}
