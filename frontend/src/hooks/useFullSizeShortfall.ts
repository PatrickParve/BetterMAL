import { useLayoutEffect, type RefObject } from 'react'

// How much narrower the window is than it could be on this screen, written to
// the element as `--full-size-shortfall` (add-genre-filter-and-polish-lists,
// design D6). My List's rows read it to know how far to shorten their progress
// bars: at full size the shortfall is 0 and every bar is its full length, and
// each pixel the window narrows by takes a pixel off the bar of a row whose
// title is already cut.
//
// Full size means the screen's available width, or the app's maximum page
// width (#root's max-width, read from the computed style rather than repeated
// here) where that is smaller. Rows span the content width, so a difference in
// window width is a difference in row width. `innerWidth` and `availWidth` both
// count any scrollbar, so a maximised window reads 0 on every OS.
//
// The value is written straight to the element's style, never to React state:
// a resize then re-renders neither the page nor its memoised rows, and React's
// own style diffing leaves a property it never set alone. It is set in a layout
// effect, so it is there before the first paint, and again on `resize`, at most
// once per animation frame. When the screen's width can't be read the property
// is removed, which makes the CSS fall back to a shortfall of 250px: no bar
// shortening at all beyond what each row's own title forces, never an overflow.
export function useFullSizeShortfall(ref: RefObject<HTMLElement | null>) {
  useLayoutEffect(() => {
    const node = ref.current
    if (!node) return
    let frame = 0

    function apply() {
      frame = 0
      const screenWidth = window.screen.availWidth
      if (!Number.isFinite(screenWidth) || screenWidth <= 0) {
        node!.style.removeProperty('--full-size-shortfall')
        return
      }
      const root = document.getElementById('root')
      const parsedCap = root ? Number.parseFloat(getComputedStyle(root).maxWidth) : Number.NaN
      const cap = Number.isFinite(parsedCap) ? parsedCap : Number.POSITIVE_INFINITY
      const shortfall = Math.max(0, Math.min(screenWidth, cap) - Math.min(window.innerWidth, cap))
      node!.style.setProperty('--full-size-shortfall', `${shortfall}px`)
    }

    function onResize() {
      if (frame === 0) frame = requestAnimationFrame(apply)
    }

    apply()
    window.addEventListener('resize', onResize)
    return () => {
      window.removeEventListener('resize', onResize)
      if (frame !== 0) cancelAnimationFrame(frame)
      node.style.removeProperty('--full-size-shortfall')
    }
  }, [ref])
}
