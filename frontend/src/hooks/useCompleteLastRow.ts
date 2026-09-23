import { useCallback, useLayoutEffect, useRef, type Dispatch, type SetStateAction } from 'react'

// A progressively revealed card grid (Season, Year, Search, the Series
// browser) slices its already-loaded list to `visibleCount` and reveals
// another step as a sentinel below the grid scrolls into view. A step of 24
// (48 on Search) ends on a full row at six columns, but an auto-fill grid
// below the desktop breakpoint has whatever column count fits the window,
// which needn't divide the step, and a window resize changes it again. A
// step could therefore end with a lone card on its last row, which then sat
// there alone until the sentinel just below it was reached and the next
// step filled in beside it.
//
// This tops the revealed count up until the last revealed row is complete.
// It's measured from the laid-out grid rather than counted, because the
// column count below the desktop breakpoint is only known once the grid has
// laid out. It only ever reveals more, never less, so it can't fight the
// sentinel or cut back a restored grid, and it stops at the end of the list,
// where a short last row is simply where the list ends.
//
// Returns a callback ref for the grid element: the grid mounts and unmounts
// with its page's terminal state, which an effect's dependency list can't
// express, and the ref is also where the ResizeObserver is attached.
export function useCompleteLastRow(
  items: readonly unknown[],
  visibleCount: number,
  setVisibleCount: Dispatch<SetStateAction<number>>,
) {
  const gridRef = useRef<HTMLElement | null>(null)
  const observerRef = useRef<ResizeObserver | null>(null)
  const latestRef = useRef({ total: items.length, visibleCount, setVisibleCount })
  latestRef.current = { total: items.length, visibleCount, setVisibleCount }

  const topUp = useCallback(() => {
    const grid = gridRef.current
    const { total, visibleCount, setVisibleCount } = latestRef.current
    const last = grid?.lastElementChild
    if (!grid || !last || visibleCount >= total) return
    const style = getComputedStyle(grid)
    // The resolved value lists every track, auto-fill ones included, in px.
    const tracks = style.gridTemplateColumns.split(' ')
    if (tracks.length < 2) return
    const pitch = parseFloat(tracks[0]) + (parseFloat(style.columnGap) || 0)
    const contentRight =
      grid.getBoundingClientRect().left + grid.clientLeft + grid.clientWidth - parseFloat(style.paddingRight)
    const missing = Math.round((contentRight - last.getBoundingClientRect().right) / pitch)
    if (missing <= 0) return
    // An absolute target rather than `prev + missing`, so the layout effect
    // and the observer measuring the same layout can't both add a row.
    const target = Math.min(visibleCount + missing, total)
    setVisibleCount((prev) => Math.max(prev, target))
  }, [])

  // Before paint, after every reveal and every change of list, so a lone
  // card never shows for a frame. Each top-up re-renders and lands back here,
  // so a reveal that still leaves the last row short is topped up again,
  // until the last row is complete.
  useLayoutEffect(() => {
    topUp()
  }, [topUp, items, visibleCount])

  // A window resize moves cards without rendering this page. One that
  // leaves the last row short pushes a card onto a new row, which changes
  // the grid's size.
  return useCallback(
    (el: HTMLElement | null) => {
      observerRef.current?.disconnect()
      observerRef.current = null
      gridRef.current = el
      if (!el) return
      const observer = new ResizeObserver(() => topUp())
      observer.observe(el)
      observerRef.current = observer
    },
    [topUp],
  )
}
