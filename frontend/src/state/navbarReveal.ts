import { useSyncExternalStore } from 'react'

// One module owning the navbar's shown/hidden state and every scroll the
// client makes on its own behalf (design D3). A scroll event carries
// nothing that says whether the page moved because I scrolled it or because
// the client did (a restore, a SeriesPage pin, the navbar's own
// scroll-to-top), so telling those apart lives here once, behind
// scrollWindowTo, rather than at every place that would otherwise call
// window.scrollTo directly.

// How far the direction tracker's own accumulator has to travel, in one
// unbroken direction, before it flips the navbar. What keeps a momentum
// scroll's last pixel of rubber-band, or a one-pixel wobble, from flickering
// it. Starting value — settled by trying it in the app (design.md Open
// Questions).
const REVEAL_TOLERANCE_PX = 8

// How long scroll events have to stay quiet before a client-initiated
// scroll (scrollWindowTo) counts as finished, re-armed by every scroll event
// and by every further call. scrollend isn't used because it isn't
// available in every browser the app targets (design D3). Starting value.
const HOLD_QUIET_MS = 150

// Keys that move the page, the same list useScrollRestoration's own retry
// loop cancels on — one of these ends a client-scroll hold at once rather
// than waiting out the quiet window.
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

let hidden = false
const hiddenListeners = new Set<() => void>()

function setHidden(next: boolean): void {
  if (hidden === next) return
  hidden = next
  hiddenListeners.forEach((listener) => listener())
}

// The direction tracker's own state. accumulated resets whenever the run's
// direction flips, so a reversal has to earn the same tolerance the
// original direction did, not just undo it one pixel at a time.
let lastScrollY = 0
let trackedDirection: 'up' | 'down' | null = null
let accumulated = 0

// Published by measureNavbar (below) from the element Navbar.tsx attaches.
// Read here for the tracker's own "always shown near the top" rule, and
// mirrored onto --navbar-height for index.css's scroll-padding-top
// (design D8) — one measurement, two readers.
let navbarHeightPx = 0

// A client-scroll hold brackets one scrollWindowTo call: while it's open,
// scroll events only move the tracker's baseline (handleScroll below) and
// never flip the shown state, so a restore or a pin is never read as my own
// scrolling. holdTop and holdEndedAt are also how isScrollToTopSettling
// answers task 4.3's double-click grace window, without that window's own
// constant living in this module.
let holdActive = false
let holdTimer: ReturnType<typeof setTimeout> | null = null
let holdTop = 0
let holdEndedAt = 0

function armHold(): void {
  if (holdTimer !== null) clearTimeout(holdTimer)
  holdTimer = setTimeout(endHold, HOLD_QUIET_MS)
}

function endHold(): void {
  holdActive = false
  if (holdTimer !== null) {
    clearTimeout(holdTimer)
    holdTimer = null
  }
  holdEndedAt = performance.now()
  // Tracking resumes from wherever the page actually settled, not from the
  // jump the hold suppressed direction-tracking across.
  lastScrollY = window.scrollY
  trackedDirection = null
  accumulated = 0
}

function handleScroll(): void {
  const y = window.scrollY
  const delta = y - lastScrollY
  lastScrollY = y

  if (holdActive) {
    armHold()
    return
  }

  if (y <= navbarHeightPx) {
    setHidden(false)
    trackedDirection = null
    accumulated = 0
    return
  }

  if (delta === 0) return

  const direction = delta > 0 ? 'down' : 'up'
  if (direction !== trackedDirection) {
    trackedDirection = direction
    accumulated = 0
  }
  accumulated += Math.abs(delta)

  if (accumulated >= REVEAL_TOLERANCE_PX) {
    setHidden(direction === 'down')
  }
}

function handleUserInput(event: Event): void {
  if (!holdActive) return
  if (event.type === 'keydown' && !SCROLL_KEYS.has((event as KeyboardEvent).key)) return
  endHold()
}

function attachTrackingListeners(): void {
  lastScrollY = window.scrollY
  window.addEventListener('scroll', handleScroll, { passive: true })
  window.addEventListener('wheel', handleUserInput, { passive: true })
  window.addEventListener('touchstart', handleUserInput, { passive: true })
  window.addEventListener('pointerdown', handleUserInput)
  window.addEventListener('keydown', handleUserInput)
}

function detachTrackingListeners(): void {
  window.removeEventListener('scroll', handleScroll)
  window.removeEventListener('wheel', handleUserInput)
  window.removeEventListener('touchstart', handleUserInput)
  window.removeEventListener('pointerdown', handleUserInput)
  window.removeEventListener('keydown', handleUserInput)
}

// Installed when the navbar first subscribes and torn down once nothing is
// listening any more, rather than at module load — mirrors useScrollLock's
// own refcounted attach/detach for the same reason: nothing here should run
// before there's a navbar on screen to react to it.
function subscribe(listener: () => void): () => void {
  if (hiddenListeners.size === 0) attachTrackingListeners()
  hiddenListeners.add(listener)
  return () => {
    hiddenListeners.delete(listener)
    if (hiddenListeners.size === 0) detachTrackingListeners()
  }
}

function getSnapshot(): boolean {
  return hidden
}

// Read by Navbar to put navbar--hidden on the header (design D1/D3).
export function useNavbarHidden(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot)
}

// The one way the client scrolls the window (design D3): every fresh-visit
// and restore scroll in useScrollRestoration, both of SeriesPage's pins, and
// the navbar's own current-page-link click go through this instead of
// window.scrollTo directly, so none of them are ever read as my scrolling.
export function scrollWindowTo(top: number, options: { navbar: 'show' | 'hide'; smooth?: boolean }): void {
  setHidden(options.navbar === 'hide')
  holdActive = true
  holdTop = top
  armHold()
  window.scrollTo({ top, behavior: options.smooth ? 'smooth' : 'auto' })
}

// Task 4.3's double-click grace window reads this instead of watching scroll
// events itself: a click while a navbar scroll-to-top (top === 0) is still
// running, or within graceMs of it settling, should only scroll and never
// reset. Gated on top === 0 so a SeriesPage pin — scrollWindowTo's other
// caller, which never targets 0 — can't extend the window a click is judged
// against.
export function isScrollToTopSettling(graceMs: number): boolean {
  if (holdTop !== 0) return false
  if (holdActive) return true
  return performance.now() - holdEndedAt < graceMs
}

let resizeObserver: ResizeObserver | null = null
let observedElement: HTMLElement | null = null

function publishNavbarHeight(height: number): void {
  navbarHeightPx = height
  document.documentElement.style.setProperty('--navbar-height', `${height}px`)
}

// Measures the navbar with a ResizeObserver and publishes its height (design
// D3/D8). Called as Navbar.tsx's <header> ref callback, so it receives the
// element on mount and null on unmount like any React ref callback.
export function measureNavbar(element: HTMLElement | null): void {
  if (resizeObserver && observedElement) resizeObserver.unobserve(observedElement)
  observedElement = element
  if (!element) return

  if (!resizeObserver) {
    // Re-reads getBoundingClientRect rather than trusting the observer's own
    // contentRect, which excludes padding and border — what content below
    // the navbar and the "within the navbar's own height" rule both need is
    // its actual rendered height, not just its content box.
    resizeObserver = new ResizeObserver(() => {
      if (observedElement) publishNavbarHeight(observedElement.getBoundingClientRect().height)
    })
  }
  resizeObserver.observe(element)
  publishNavbarHeight(element.getBoundingClientRect().height)
}
