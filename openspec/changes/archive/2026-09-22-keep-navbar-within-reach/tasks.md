## 1. Move the Updates history overlay out of the navbar

- [x] 1.1 In `frontend/src/components/Updates/UpdatesMenu.tsx`, render `<UpdatesHistoryOverlay>` through `createPortal(…, document.body)`, with a comment saying why: the navbar becomes sticky, which would trap a fixed-position backdrop inside its stacking context (design D2).
- [x] 1.2 Confirm the history overlay still opens from the dropdown, covers the whole window, closes on Escape, backdrop click and route change, and returns focus as before. Confirm a click inside it no longer does anything to the (already closed) dropdown.

## 2. Navbar visibility and client scrolling module

- [x] 2.1 Create `frontend/src/state/navbarReveal.ts` holding the shown/hidden state with `subscribe`/`getSnapshot` for `useSyncExternalStore`, plus a `useNavbarHidden()` hook (design D3).
- [x] 2.2 Add the direction tracker: one passive window `scroll` listener installed on first subscribe. It always shows within the navbar's measured height of the top, accumulates movement per direction (resetting when the direction flips), and flips state past a named tolerance constant (start at 8px).
- [x] 2.3 Add the client-scroll hold and `scrollWindowTo(top, { navbar, smooth })`. While held, scroll events only move the baseline. The hold ends after a named quiet window (start at 150ms) re-armed by each scroll event and each call, or at once on `wheel`, `touchstart`, `pointerdown` or a scroll key. The helper also records when a navbar-originated scroll-to-top started and settled, for task 4.3's grace window.
- [x] 2.4 Measure the navbar with a `ResizeObserver` and publish its height as `--navbar-height` on `document.documentElement`. The tracker's near-the-top rule reads the same measurement.

## 3. Sticky navbar

- [x] 3.1 In `Navbar.tsx`, read `useNavbarHidden()`, put `navbar--hidden` on the `<header>`, and attach the element the module measures.
- [x] 3.2 In `Navbar.css`, make `.navbar` `position: sticky; top: 0` with `background: var(--bg)` and a z-index above the page's filter menus (90) and below the action-failure notices (95) and `Modal` (100). Record the band it sits in, in a comment (design D1).
- [x] 3.3 Add the hidden rule `.navbar--hidden:not(:focus-within):not(:has(.search-bar__dropdown, .updates-menu__dropdown)) { transform: translateY(-100%) }` and a `transform` transition. Leave the shown state transform-free, with a comment on why no resting transform or `will-change` may be added (design D1/D4). Drop the transition under `prefers-reduced-motion: reduce`.
- [x] 3.4 In `index.css`, add `scroll-padding-top: var(--navbar-height, 0px)` on `html`, with a comment that it keeps focus and scroll-into-view clear of the navbar and doesn't affect explicit `window.scrollTo` (design D8).

## 4. The current page's link returns to the top

- [x] 4.1 Create `frontend/src/components/Navbar/NavbarPageLink.tsx` wrapping `NavLink`. It decides "current page" with `useMatch({ path: <pathname of to>, end })` so it agrees with `NavLink`'s own marking, and passes `className`, `aria-label` and children through (design D5).
- [x] 4.2 Implement its `onClick`: plain primary clicks only. When not on the current page, do nothing. When on the current page and scrolled down (`scrollY > 1`), `preventDefault`, dispatch `bettermal:user-scroll`, and call `scrollWindowTo(0, { navbar: 'show', smooth })`, with smooth off under reduced motion. At the top, let `NavLink` navigate.
- [x] 4.3 Add the double-click grace window. A click while a navbar scroll-to-top is still running, or within a named constant (start at 500ms) of it settling, is prevented and only scrolls.
- [x] 4.4 Render the eight left links and Profile in `Navbar.tsx` through `NavbarPageLink`. Swap `SettingsLink.tsx`'s `NavLink` for it, keeping the gear's icon, dot, progress sliver and accessible name.
- [x] 4.5 In `hooks/useScrollRestoration.ts`, add `bettermal:user-scroll` to the restore loop's cancel listeners, beside `wheel`, `touchstart` and `keydown`, and remove it in the cleanup.

## 5. Route every client window scroll through the helper

- [x] 5.1 In `useScrollRestoration.ts`, replace the fresh-visit `window.scrollTo(0, 0)` and the restore loop's `window.scrollTo(0, target)` with `scrollWindowTo(…, { navbar: 'show' })`. The `keepScroll` branch stays scroll-free.
- [x] 5.2 In `pages/SeriesPage.tsx`, replace both pin-to-top `window.scrollTo` calls with `scrollWindowTo(target, { navbar: 'hide' })`. Rewrite the "nothing on this page is sticky or fixed" comment to say why the navbar is hidden for a pin.
- [x] 5.3 Grep the frontend for any remaining `window.scrollTo`/`window.scrollBy` outside `navbarReveal.ts` and route or justify each one.

## 6. Default view and the Season link

- [x] 6.1 In `Navbar.tsx`, build the Season link at render as `/season?year=<y>&season=<s>` from `currentSeasonTarget()`, beside `recapLink`/`yearLink`, with the same built-at-render comment. Keep the parameter order identical to `SeasonPage`'s `replacementUrl` (design D7).
- [x] 6.2 In `hooks/useRestorableScroll.ts`, return a still-mounted strip to its start when the location key changes on a fresh visit (not a restore). Comment it as the "fresh visit starts every strip at its beginning" rule for a node the new entry reuses (design D6).
- [x] 6.3 Update the `Navbar.tsx` comments on `recapLink`/`yearLink` and on the link list, which currently describe `NavLink` matching and link targets, so they mention the current-page rule and the Season link.

## 7. Verification

- [x] 7.1 `npm run lint` and `npm run build` in `frontend/` pass (Node 22 via nvm, not the default v16).
- [x] 7.2 Navbar reveal in the app. Scrolling down a long page hides it past its height with no content shift, and a short upward scroll brings it back from deep in the page. It is always shown at the top and doesn't flicker at the end of a momentum scroll. It stays while the search dropdown or Updates dropdown is open, and Tab into it shows it. Reduced motion toggles it without sliding. Check at a wide window and below 900px, where it wraps to two rows.
- [x] 7.3 Arrivals and client scrolls. Back to a list left far down restores the position with the navbar shown. On a series page, opening a More group and switching the main-line pick pins the heading/box at the window's top with the navbar hidden, not covered. Shift-Tab to an element above the viewport lands it below the navbar. **Verified: More-group pin, main-line-pick pin, and Shift-Tab landing below the navbar all correct. Note: Back navigation to a scrolled-down list does not restore scroll position — confirmed pre-existing (reproduces identically with the pre-change plain `window.scrollTo` code) and out of scope per design.md's Non-Goals ("Back/Forward restoration").**
- [x] 7.4 Overlays. A page `Modal` (entry editor, rank overlay) covers the navbar. The Updates history overlay covers the whole window, not just the navbar's strip. Notices still sit above page content.
- [x] 7.5 Current-page click, scrolled down, on every page control. Home, My List (filtered, extra rows revealed), Series, Recap (a non-default period), Top (page 3), Season (a past season), Year (2020), Airing (a later week), Profile and Settings each scroll smoothly to the top with the address, view controls, period/page and revealed rows unchanged and no loading state. A double-click only scrolls. Scrolling during the trip stops it. Cmd/Ctrl-click opens the default view in a new tab and leaves this page alone.
- [x] 7.6 Current-page click at the top. Year 2020 → current year, and Back returns to 2020 restored. Season on the current season with default controls → nothing visibly changes, and Back doesn't step through a duplicate. A filtered My List at the top → default filters with no loading state. Profile at the top with its strip scrolled → the strip back at its start.
- [x] 7.7 Season from another page opens directly on the current season with a single load, with no blank frame or second loading state.
- [x] 7.8 Run `openspec validate keep-navbar-within-reach` and confirm both delta specs validate.
