import { useEffect, useLayoutEffect } from 'react'
import { useLocation, useNavigationType } from 'react-router-dom'
import { usePageState } from '../state/PageStateContext.tsx'

// How long the retry loop keeps trying to reach a scroll target on a page
// whose height still depends on a completing fetch.
const RETRY_BUDGET_MS = 500

// Mounted once in AppShell so it wraps every route. Pairs with
// `history.scrollRestoration = 'manual'` in main.tsx, which tells the browser
// to stop competing with this.
export function useScrollRestoration(): void {
  const location = useLocation()
  const navigationType = useNavigationType()
  const { isRestore, snapshot } = usePageState()

  // Recorded continuously (not just at navigation time) so the value is
  // already correct whenever an entry is left, by any means.
  useEffect(() => {
    let scheduled = false
    function onScroll() {
      if (scheduled) return
      scheduled = true
      requestAnimationFrame(() => {
        snapshot.scrollY = window.scrollY
        scheduled = false
      })
    }
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [snapshot])

  // Applied once per navigation, before paint, so a restore never flashes at
  // the top before jumping to the saved position.
  useLayoutEffect(() => {
    if (!(navigationType === 'POP' && isRestore)) {
      window.scrollTo(0, 0)
      return
    }

    const target = snapshot.scrollY
    let cancelled = false
    let programmatic = false
    let rafId = 0

    const deadline = performance.now() + RETRY_BUDGET_MS

    function attempt() {
      if (cancelled) return
      // Checked *before* scrolling, using this frame's height: checking
      // growth only after `scrollTo` would judge this attempt by a height
      // the page hadn't reached yet when `scrollTo` actually ran, stopping
      // the retry one frame before the target was truly reachable.
      const reachable = document.documentElement.scrollHeight - window.innerHeight >= target
      programmatic = true
      window.scrollTo(0, target)
      rafId = requestAnimationFrame(() => {
        programmatic = false
        if (reachable || performance.now() >= deadline) return
        attempt()
      })
    }

    function onUserScroll() {
      if (programmatic) return
      cancelled = true
    }

    window.addEventListener('scroll', onUserScroll, { passive: true })
    attempt()

    return () => {
      cancelled = true
      cancelAnimationFrame(rafId)
      window.removeEventListener('scroll', onUserScroll)
    }
  }, [location.key, navigationType, isRestore, snapshot])
}
