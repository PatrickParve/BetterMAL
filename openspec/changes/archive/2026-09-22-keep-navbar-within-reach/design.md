## Context

**The navbar scrolls away.** `<Navbar>` renders a plain `<header className="navbar">` as the first child of `#root`, above `<main className="page-content">`. It has no position, no background of its own and no z-index, so it leaves with the page's first screen.

**Clicking the current page's link is an ordinary navigation.** Every page control is a `NavLink` (`Navbar.tsx`'s `leftNavLinks`, the inline Profile link, and `SettingsLink.tsx`'s gear). React Router 7's link click handler (`useLinkClickHandler`) runs our own `onClick` first and navigates only if `defaultPrevented` is still false. It navigates with `replace` when the resolved target equals the current location string (`createPath(location) === createPath(path)`), and pushes otherwise. Either way a navigation mints a new `location.key`, and everything keyed on it treats the result as a fresh visit:

- `PageStateProvider` hands out a new, empty snapshot.
- `useRestorableState` resets every view control to its `initial`.
- `useScrollRestoration` does `window.scrollTo(0, 0)` instantly.
- `usePageData` keeps data it already holds for an unchanged key (its "same key, data in hand, not a restore" carry) and loads anything else.

So today, on a page whose address matches its link exactly (Home, a default My List), clicking the link jumps to the top and resets view controls, but shows the data at once. On a page whose address differs from its link (Year 2020 vs `/year?year=2026`, Top page 3 vs `/top`, any Recap period vs the default one), it pushes the default view.

**Season is worse.** Its link is bare `/season`. `SeasonPage`'s route guard sees no `year`/`season` and returns `<Navigate replace>` to the current season's address. That render returns the redirect instead of `<SeasonPageView>`, so the view unmounts and remounts and its reads start from nothing. That is the reload. It happens on every navbar click into Season, not only on the current-page click. Year avoids it because its link is built at render as `/year?year=<current>`.

**Constraints found in the code:**

- `#root` has `overflow-x: clip`. `clip`, unlike `hidden`, doesn't make it a scroll container, so `position: sticky` on a child of `#root` sticks to the viewport.
- `useScrollLock` locks with `overflow: hidden` on `<body>` and never moves `window.scrollY`. An open overlay produces no scroll events for the navbar to react to.
- **The Updates history overlay renders inside the navbar.** `UpdatesMenu` renders `<UpdatesHistoryOverlay>` → `<Modal>` in place, and `Modal` doesn't portal. Its `.modal-backdrop` is `position: fixed; z-index: 100` *inside* `<header class="navbar">`. A sticky element always forms a stacking context, and a transformed one also becomes the containing block for `position: fixed` descendants. Both would trap that overlay.
- Stacking today: page content tops out at 20 (the rank preview), page filter menus at 90, the connection notice at 90, action-failure notices at 95, every `Modal` at 100, and the portaled tooltips (`TruncatedTitle`, score-board) at 1000. The navbar's own dropdowns are 50 within it.
- The client scrolls the window itself in three places: `useScrollRestoration` (fresh visit to 0, and the restore retry loop) and `SeriesPage`'s two pin-to-top scrolls. The pin code says outright "No offset, since nothing on this page is sticky or fixed". That assumption stops being true.
- `useRestorableScroll` applies a strip's offset only when its DOM node attaches. A fresh visit that reuses a mounted strip (the same-address reset) leaves it where it was, although `page-state-restoration` says a fresh visit starts every strip at its beginning. The only strip outside an overlay that uses it is Profile's.

## Goals / Non-Goals

**Goals:**

- The navbar is always one short upward scroll away. It gets out of the way going down and is never in the way of content the client places or focus the keyboard moves.
- Clicking the current page's link while scrolled down is a pure scroll to the top: no navigation, no reset, no reload, for every navbar page control.
- A deliberate click at the top resets to the default view through the same fresh-visit machinery every other navbar click already uses, not a second mechanism.
- Season opens from the navbar without the redirect bounce.

**Non-Goals:**

- Changing navbar layout, order, sizes, current-page marking or hover treatment.
- Changing how a page reached from *another* page behaves, or Back/Forward restoration.
- Portaling `Modal` in general. Only the one overlay that lives inside the navbar moves (D2). Portaling every overlay would change hit-testing and click-outside assumptions for overlays that have nothing to do with this change.
- Changing the Season/Year route guards. A bare or invalid address is still replaced with the current season/year.
- Making the navbar reset remember non-URL view state for Back. See Risks.

## Decisions

### D1 — Sticky, hidden by a transform applied only while hidden

`.navbar` becomes `position: sticky; top: 0`, with an opaque `background: var(--bg)` (its existing bottom border already separates it from the page). A `navbar--hidden` class applies `transform: translateY(-100%)`, and the shown state has **no transform at all**: `transform: none`, never `translateY(0)`, and no `will-change: transform`. A `transition: transform 200ms ease` makes the slide, and `prefers-reduced-motion: reduce` drops the transition.

- **Sticky rather than fixed.** Sticky keeps the navbar's space in the flow, so nothing needs a compensating `padding-top`, hiding it never shifts content, and at scroll 0 it is simply where it is today.
- **Transform rather than animating `top`.** A transform is composited, so the slide stays smooth while the page itself is being scrolled. Animating `top` re-lays out the sticky element every frame during the very scroll that triggered it. The cost of a transform is the containing-block trap for fixed descendants. D2 removes the one such descendant, and the "no transform while shown" rule keeps the trap from coming back.
- **z-index: above every layer the page draws, including its open filter menus (90), and below the action-failure notices (95), every `Modal` (100) and the portaled tooltips (1000).** Page content, page menus included, scrolls *under* the header, as it does under any sticky header. Page overlays cover it, as the spec requires. The navbar's own dropdowns stay above the page because they sit inside the navbar's stacking context. The value and its reasoning are recorded in `Navbar.css` beside the band it sits in.

*Alternatives:* a `position: fixed` navbar plus `padding-top` on `.page-content` has to track the navbar's height at every width, since it wraps to two rows under 900px, and gains nothing. Hiding with `visibility`/`display` removes the navbar from the focus order, which is exactly what "tab into a hidden navbar shows it" depends on.

### D2 — The Updates history overlay renders through a portal

`UpdatesMenu` renders `<UpdatesHistoryOverlay>` via `createPortal(…, document.body)`, so its backdrop is positioned and stacked against the viewport again. It is then covered by the same z-index rule as every other `Modal`, and D1's navbar z-index no longer caps it. React events still bubble through the React tree, so nothing that listens on the menu's side loses them. `UpdatesMenu`'s `useClickOutside` treats a click inside the portaled overlay as "outside", but it only ever closes the dropdown, and the dropdown is already closed while the history is open. Focus return on close is unchanged.

*Alternative:* keeping it in place and relying on the navbar never being transformed while it's open. That holds today, because the overlay can only be opened from a shown navbar, but it quietly breaks the first time anyone styles the navbar differently, and it still leaves the overlay capped at the navbar's z-index.

### D3 — One module owns the navbar's visibility and every scroll the client makes

A new `frontend/src/state/navbarReveal.ts`, module-level like `pageStateStore.ts`, owns three things:

- **The shown/hidden state.** Read through `useSyncExternalStore` by `Navbar`, which puts the `navbar--hidden` class on the header.
- **Direction tracking.** One passive window `scroll` listener, installed when the navbar first subscribes. Within the navbar's measured height of the top it always shows. Beyond that, it accumulates movement in the current direction (resetting the accumulator when the direction flips) and flips the state once that run passes a small tolerance (≈8px). The tolerance is what keeps a momentum scroll's tail, or a one-pixel reversal, from flickering it.
- **`scrollWindowTo(top, { navbar: 'show' | 'hide', smooth? })`.** The one way the client scrolls the window. It sets the requested navbar state, opens a *client-scroll hold*, and calls `window.scrollTo`. While a hold is open, scroll events only move the tracker's baseline and never change the state. The hold ends when scroll events have been quiet for ≈150ms, re-armed by every event and by every further `scrollWindowTo`, or at once on my own input (`wheel`, `touchstart`, `pointerdown`, or a scroll key). `scrollend` isn't used because it is not available in every browser the app targets. The quiet timer covers the same ground.

Callers:

| Site | Call |
|---|---|
| `useScrollRestoration`, fresh visit | `scrollWindowTo(0, { navbar: 'show' })` |
| `useScrollRestoration`, each restore attempt | `scrollWindowTo(target, { navbar: 'show' })`. Every attempt re-arms the hold, so a restore that waits on data never lapses into "my scrolling" between attempts |
| `SeriesPage`, group pin and main-line pin | `scrollWindowTo(target, { navbar: 'hide' })`. The comment claiming nothing on the page is sticky is rewritten |
| Navbar current-page click (D5) | `scrollWindowTo(0, { navbar: 'show', smooth: !reducedMotion })` |

"Arriving at a page shows the navbar" falls out of the first two rows: every navigation that scrolls goes through them. A same-page `keepScroll` navigation (Airing's week step) doesn't scroll and leaves the navbar alone, which is right because it isn't an arrival.

The module also measures the navbar with a `ResizeObserver` and publishes it as `--navbar-height` on `document.documentElement`, for the "within its own height" rule and for D8.

*Why a hold rather than telling user scrolling apart from client scrolling some other way:* scroll events carry nothing that identifies their cause. `useScrollRestoration` already learned this the hard way and cancels its loop on input events rather than on scroll events. Driving direction from input events (wheel deltas, key directions) instead would miss scrollbar drags, find-in-page jumps and anything else that moves the page without one of those events, and would misfire on a wheel over a scrollable menu that never moves the window at all. Scroll events drive the tracker. The hold only brackets the few scrolls the client makes itself, and those are exactly the ones routed through the helper.

### D4 — "In use" is decided in CSS

The hidden transform applies only as `.navbar--hidden:not(:focus-within):not(:has(.search-bar__dropdown, .updates-menu__dropdown))`. Focus inside the navbar, or either of its dropdowns being open, keeps it shown whatever the tracker says. When that stops being true it slides away if the page has been scrolled down meanwhile.

- `:focus-within` is what makes "tab into a hidden navbar shows it" work without any JS.
- `:has()` covers the dropdowns separately, because Safari doesn't focus a `<button>` on click. An Updates dropdown opened with the mouse leaves no focus inside the navbar.
- The history overlay (D2) no longer lives in the navbar, and needs no clause: the scroll lock stops the page from scrolling while it's open.

*Alternative:* having `SearchBar` and `UpdatesMenu` report their open state into the module. That's more wiring for the same result, and it duplicates state those components already express in the DOM.

### D5 — One `NavbarPageLink` carries the current-page rule

The left links, Profile and the Settings gear all render through a small `NavbarPageLink` wrapper around `NavLink`, so the rule lives in one place. `SettingsLink` keeps its icon, dot, progress sliver and accessible name, and swaps its `NavLink` for the wrapper.

The wrapper decides "this is the current page" with `useMatch({ path: <pathname of to>, end })`. That is the same pathname-only match `NavLink` already uses for its marking, so the rule and the border can't disagree: Series (with `end`) is not current on `/series/:animeId`, and Recap is current on every `/recap` period. Its `onClick`:

1. Ignore anything but a plain primary click: button 0, no modifier keys. React Router already leaves those to the browser, so new-tab/window opens are untouched.
2. Not the current page → return, and `NavLink` navigates as today.
3. The page is scrolled down (`window.scrollY > 1`), **or** a navbar scroll-to-top is still running or finished less than ≈500ms ago → `event.preventDefault()`, signal "the user is scrolling" to the restore loop (below), and call `scrollWindowTo(0, { navbar: 'show', smooth })` with `smooth` off under reduced motion. The grace window is what makes a double-click only scroll, even on a short page where the smooth scroll lands inside a double-click's interval.
4. Otherwise (current page, at the top, no recent scroll-to-top) → return without preventing, and `NavLink` navigates to its default target (D6).

**Cancelling a restore still settling.** `useScrollRestoration`'s retry loop can run for up to 1.5s after a Back, and today only `wheel`, `touchstart` and scroll keys cancel it. A mouse click on a link is none of those, so the loop would drag the page back down under the navbar's scroll-to-top. The wrapper dispatches a named window event (`bettermal:user-scroll`) that the loop adds to its cancel listeners. That keeps the loop's rule, "only my own scrolling stops a restore", and makes this click count as mine.

Smooth scrolling that I interrupt by wheel, touch or keys is cancelled by the browser natively. The hold ends on that same input, so the tracker picks up from wherever I stopped.

### D6 — The reset is `NavLink`'s own navigation

At the top, the wrapper does nothing special, and React Router's existing rule gives exactly the spec's behaviour. If the default view's address differs from the current one, it pushes, and Back returns to the view I left, restored with its data, view state and position like any Back. If the address is the same, it replaces, so there is no duplicate history entry. Either way the new `location.key` makes it a fresh visit, so view controls reset through `useRestorableState`, and `usePageData`'s existing same-key carry shows data already held for the default view with no loading state. A different view (2020 → 2026) loads as any fresh visit does.

One gap is closed so "default view" really means default: **`useRestorableScroll` returns a strip to its start on a fresh visit that reuses its mounted node**. It watches the location key and, when the key changes and the visit is not a restore, zeroes the offset. Only Profile's strip reaches that path outside an overlay, and Profile has no same-page URL changes, so nothing else changes behaviour.

### D7 — The Season link addresses the current season

`Navbar` builds the Season link at render as `/season?year=<y>&season=<s>` from `currentSeasonTarget()`, beside the existing `recapLink` and `yearLink` and for the same reason: built at render, so a tab left open across a season boundary still targets the current one. The parameter order matches `SeasonPage`'s own `replacementUrl` (year, then season), so the link's string equals the address the page sits at, and D6's "same address → replace" applies. The guard's redirect for a bare `/season` stays for typed and old addresses.

### D8 — Focus and scroll-into-view clear the navbar

`index.css` gives `html` a `scroll-padding-top: var(--navbar-height, 0px)`. The browser applies it to focus-induced scrolling, `scrollIntoView` and fragment jumps on the root scroller, so an element brought into view from above lands below the navbar rather than under it. It doesn't affect `window.scrollTo` with explicit coordinates, so restoration and the pins (D3) are untouched. Inner scrollers such as the rank overlay's list don't read the root's scroll padding. When the navbar is hidden the padding leaves a navbar-height gap above a focused element, which is harmless.

## Risks / Trade-offs

- **The transform becomes a containing block for fixed descendants again** → D2 removes the only one, and D1 keeps the shown state transform-free. The `Navbar.css` comment on the hidden rule says why both matter, so a later edit that adds `will-change` or a resting `translateY(0)` is visibly wrong.
- **A reset to a view with the same address can't be undone with Back** → My List's filters and search text live in restorable state, not the URL, so a click at the top of a filtered `/my-list` replaces the entry and those selections are gone. Always pushing would make Back undo it, but repeated clicks at the top would stack identical entries for Back to step through. Accepted: the reset needs a deliberate click already at the top, and a double-click can't trigger it (D5's grace window).
- **The tracker's tolerance and the hold's quiet window are tuned by feel** → 8px and 150ms are starting values. Both live as named constants in `navbarReveal.ts`. The hold can also end early on real input, so a long quiet window never swallows my own scrolling.
- **A restore that waits on slow data longer than the quiet window** → each retry attempt re-arms the hold (D3). Once the loop gives up (at 1.5s) or reaches its target, tracking resumes from wherever the page is.
- **`:has()` support** → available in every current browser the app targets. Where it's missing, the rule falls back to hiding with only focus to keep it shown. That degrades to "the Updates dropdown can scroll away with the navbar", not to anything broken.
- **Page menus now scroll under the navbar** → a filter menu open near the top of the window is overlapped by a navbar that slides in. That is standard sticky-header behaviour, and the menu is page content like any other.

## Migration Plan

Frontend only: no data, API or configuration change. Rolling back is reverting the change. Nothing is persisted that a rollback would strand.

## Open Questions

None blocking. Two values are settled during implementation by trying them in the app: the tracker's tolerance (D3) and the double-click grace window (D5).
