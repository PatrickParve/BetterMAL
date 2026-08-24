import { useEffect } from 'react'

// Module-level so stacked overlays (a completion prompt opening over an
// editor, say) share one lock: the first acquisition is the only one that
// touches the DOM, and only the last release restores it.
let lockCount = 0
let restoreOverflow = ''
let restorePaddingRight = ''

// overlay-behaviour "An open overlay holds the page behind it still",
// design.md D6. `overflow: hidden` on the body — not `position: fixed` — is
// the load-bearing choice: the body's overflow propagates to the viewport,
// so wheel/trackpad/keyboard scrolling all stop, but `window.scrollY` is
// left untouched and no scroll event fires, so useScrollRestoration's
// snapshot is never disturbed. A fixed-position body would scroll the
// document to 0, fire a scroll event, and write that 0 into the current
// history entry's snapshot.
//
// Hiding the scrollbar reclaims its width, which would shift the page
// sideways; padding-right compensates by that exact width while the lock is
// held, and is removed again on release.
export function useScrollLock(): void {
  useEffect(() => {
    if (lockCount === 0) {
      restoreOverflow = document.body.style.overflow
      restorePaddingRight = document.body.style.paddingRight
      const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth
      document.body.style.overflow = 'hidden'
      document.body.style.paddingRight = `${scrollbarWidth}px`
    }
    lockCount += 1

    return () => {
      lockCount -= 1
      if (lockCount === 0) {
        document.body.style.overflow = restoreOverflow
        document.body.style.paddingRight = restorePaddingRight
      }
    }
  }, [])
}
