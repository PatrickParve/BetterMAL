import { useEffect, useLayoutEffect, useRef } from 'react'
import { useLocation, useNavigationType } from 'react-router-dom'
import { usePageState } from '../state/PageStateContext.tsx'
import * as pageStateStore from '../state/pageStateStore.ts'

// How long the retry loop keeps trying to reach a scroll target on a page
// whose height still depends on a completing fetch — long enough to cover a
// section whose data was never seeded before the page was left. The loop
// still stops on the first frame the target is reachable, so a longer budget
// costs nothing when the page settles quickly.
const RETRY_BUDGET_MS = 1500

// Keys that move the page and should cancel a restore in progress, alongside
// wheel and touch input.
const SCROLL_KEYS = new Set([
  'ArrowUp',
  'ArrowDown',
  'ArrowLeft',
  'ArrowRight',
  'PageUp',
  'PageDown',
  'Home',
  'End',
  ' ',
])

// Mounted once in AppShell so it wraps every route. Pairs with
// `history.scrollRestoration = 'manual'` in main.tsx, which tells the browser
// to stop competing with this.
export function useScrollRestoration(): void {
  const location = useLocation()
  const navigationType = useNavigationType()
  const { key, isRestore, snapshot } = usePageState()

  // Assigned during render, so it already points at the entry being
  // displayed before any layout effect of a navigation runs — including the
  // fresh-visit `scrollTo(0, 0)` below and any scroll event that produces.
  const keyRef = useRef(key)
  keyRef.current = key

  // Recorded synchronously against whichever entry is on screen right now.
  // This used to defer to a requestAnimationFrame and close over the
  // snapshot object captured when the effect ran: a scroll event fired just
  // before navigating could have its frame fire *after* the next page's
  // layout effect had already scrolled it to the top, writing that page's
  // `0` into the departing entry's snapshot through the stale closure.
  // Writing immediately, through a key read fresh off the ref, means a stray
  // event can only ever land on the entry actually being displayed.
  useEffect(() => {
    function onScroll() {
      pageStateStore.putScroll(keyRef.current, window.scrollY)
    }
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  // Applied once per navigation, before paint, so a restore never flashes at
  // the top before jumping to the saved position.
  useLayoutEffect(() => {
    if (!(navigationType === 'POP' && isRestore)) {
      window.scrollTo(0, 0)
      return
    }

    const target = snapshot.scrollY
    let cancelled = false
    let rafId = 0

    const deadline = performance.now() + RETRY_BUDGET_MS

    function attempt() {
      if (cancelled) return
      // Checked *before* scrolling, using this frame's height: checking
      // growth only after `scrollTo` would judge this attempt by a height
      // the page hadn't reached yet when `scrollTo` actually ran, stopping
      // the retry one frame before the target was truly reachable.
      const reachable = document.documentElement.scrollHeight - window.innerHeight >= target
      window.scrollTo(0, target)
      rafId = requestAnimationFrame(() => {
        if (reachable || performance.now() >= deadline) return
        attempt()
      })
    }

    // Cancelled on real user input rather than on scroll events: those are
    // asynchronous and carry nothing identifying a clamped `scrollTo`'s own
    // scroll event from the user's, so watching them cancelled the loop
    // before the target was reached whenever the clamp's event arrived late.
    function onUserInput(event: Event) {
      if (event instanceof KeyboardEvent && !SCROLL_KEYS.has(event.key)) return
      cancelled = true
    }

    window.addEventListener('wheel', onUserInput, { passive: true })
    window.addEventListener('touchstart', onUserInput, { passive: true })
    window.addEventListener('keydown', onUserInput)
    attempt()

    return () => {
      cancelled = true
      cancelAnimationFrame(rafId)
      window.removeEventListener('wheel', onUserInput)
      window.removeEventListener('touchstart', onUserInput)
      window.removeEventListener('keydown', onUserInput)
    }
  }, [location.key, navigationType, isRestore, snapshot])
}
