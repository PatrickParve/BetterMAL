## Context

Three unrelated frontend defects, sharing only that each is a small, self-contained fix in a place the app already has the right machinery.

**The picker.** `PicturePickerOverlay` is one component, used by both `AnimeDetailPage` and `SeriesPage`. Its CSS puts every option in a `grid-template-columns: repeat(auto-fill, minmax(110px, 130px))` cell locked to `aspect-ratio: 2 / 3`, with `object-fit: cover` on the image — so every option is centre-cropped to a poster, and a landscape key visual loses its sides. Both pages that open this picker already render landscape artwork whole at its own proportions (`anime-detail` "Landscape artwork is shown at its own proportions on the detail page", `series-page` "Landscape artwork is shown whole on the series page"), via `useLandscapePicture`. The picker is the only surface left that crops.

**The series controls.** `SeriesPage.tsx` renders `series-page__artwork-controls` — the Choose picture and Choose title buttons, styled with `series-page__related-link`, the same class as the MyAnimeList/AniList/SeriesGraph anchors — inside `series-page__header-info`, directly under those links. `Rebuild` lives elsewhere, in `series-page__rebuild-row` inside the Series stats heading.

**The overlay.** `Modal` is the single wrapper every overlay in the app goes through — twelve call sites, verified — and it already owns the cross-cutting overlay behaviours: Escape, click-outside, and `useScrollLock`. It does not know about the router. Three overlays are mounted at the app root by providers (`EntryEditorProvider`, `AnimeRankProvider`, `CompletionPromptProvider`) and so survive any navigation; page-local overlays survive a navigation that keeps their page mounted, which is exactly what `/anime/:id → /anime/:otherId` and `/series/:animeId → /series/:otherId` do.

Constraints: frontend only; React 19 + react-router-dom 7, no state library; the whole app is already inside `BrowserRouter` (`main.tsx`).

## Goals / Non-Goals

**Goals:**
- Every picker option shown whole, at its own proportions, and smaller than today so more fit on screen.
- The series page's Choose picture / Choose title controls grouped with Rebuild.
- Every overlay dismissed when the app leaves the page it was opened over, as a property of the overlay mechanism rather than of each overlay.

**Non-Goals:**
- Any backend, API, DTO, or database change. Nothing about what the pickers offer or how a choice is stored, validated, or applied.
- Changing how the detail page or series page draws the picture it already displays.
- Confirmation before an overlay closes on navigation, or restoring it on return.
- Closing an overlay when a page's own query parameters change.
- Moving the anime detail page's Choose picture control.

## Decisions

### D1 — Picker options are laid out to a common height, not a common width

Replace the fixed-cell grid with a wrapping flex row in which every option is drawn to one height and takes whatever width its own proportions give it there: `height: var(--picker-option-h); width: auto` on the `<img>`, with the option button shrink-wrapping it.

This is the layout that satisfies "shown whole" and "landscape as landscape" at once. A common *height* is what keeps a mixed set tidy — rows line up along their top and bottom edges regardless of orientation, and a landscape option is simply a wider item in the row.

Alternatives considered:
- **Keep the 2:3 cell, swap `cover` for `contain`.** Shows the whole image, but every option keeps a portrait footprint: a 16:9 banner is letterboxed into a thin strip with empty bars above and below, drawn *smaller* than a portrait poster beside it. That inverts the point — the wide artwork is the artwork worth seeing.
- **Common width, variable height (a masonry / CSS-columns layout).** Rows go ragged, landscape options shrink to a sliver of the row height, and a columns layout fills top-to-bottom per column, which scrambles MAL's option order across a row.
- **Measure each image in JS and set a per-option `aspect-ratio`** (the `useLandscapePicture` approach, generalised). A franchise pool can run past twenty images; that is twenty callback refs and twenty state updates for information the browser already has and applies for free through `width: auto`.

### D2 — Pre-load footprint comes from `aspect-ratio: auto 2 / 3`, not from JS

An `<img>` with `height` set and `width: auto` computes to zero width until it has decoded, so a naive version of D1 collapses every option to a hairline and then pops. `aspect-ratio: auto 2 / 3` on the image fixes this with no script: for a replaced element the `auto` keyword defers to the natural ratio once there is one, and the stated `2 / 3` applies only while there is not. So each option reserves a portrait poster's footprint, then keeps it (the common case — most MAL artwork is portrait) or widens once a landscape picture arrives.

Alternative: a `min-width` floor on the option. It reserves space too, but it is a floor that stays in force after load and would stretch any genuinely narrow artwork. `aspect-ratio: auto <ratio>` expresses exactly "this is a placeholder ratio, drop it when you know better".

### D3 — One clamp guards a pathological aspect ratio

`max-width: 100%` plus `object-fit: contain` on the image. At the chosen height an ordinary banner is around 250px wide and well inside the modal, but nothing in the data promises that; a freak panorama is reduced to fit the row whole rather than overflowing the overlay or forcing a sideways scroll. `contain` only ever engages in that clamped case, since otherwise the box is already the image's own shape.

Option height: **140px**, against today's effective 165–195px tile. Portrait options land around 93px wide, so more fit per row and a large pool loses roughly a quarter of its vertical run. This is a judgement call, not a derived number; it is one CSS custom property on the grid, trivially retuned.

### D4 — Navigation-close lives in `Modal`, keyed on `location.pathname`

`Modal` subscribes to `useLocation()`, records the pathname it was mounted at, and calls `onClose()` when the current pathname differs from it.

`Modal` is the right home for this for the same reason it already owns Escape, click-outside, and scroll-lock: it is the one choke point every overlay in the app passes through, so one implementation covers the three app-root overlays *and* the page-local overlays that survive a same-route parameter change, and covers every overlay added later without its author having to remember.

Closing through `onClose()` rather than by unmounting is what keeps dismissal semantics intact: `AnimeRankOverlay` passes `onClose={() => void handleDismiss()}` and flushes a pending arrangement there, and `CompletionScoreOverlay` passes `onClose={() => onClose(null)}` — the `null` being "dismissed, nothing saved". Both keep working unchanged.

Alternatives considered:
- **Each app-root provider clears its own state on a location change.** Three copies of the same effect, a fourth provider added later that forgets it, and no coverage at all for `/anime/1 → /anime/2` with the detail page's own picker open.
- **`key` the routed content or the providers on the pathname.** Closes the overlays by remounting everything, and takes the pages' cached state and scroll restoration down with it.
- **A "close all overlays" broadcast from a new provider.** Every overlay would have to subscribe — which is the problem `Modal` already solves.

### D5 — `pathname` only; query parameters are not a page change

`pathname` is precisely "which page, on which subject": it changes on back/forward, on a navbar click, on a link, and on `/anime/1 → /anime/2`, and does not change when a filter, sort, scope, page number, or search term is written into the query string. That last part matters concretely — `SearchPage` writes the query into `searchParams` as it is typed, and `MyListPage`, `SeasonPage`, `YearPage`, `TopAnimePage`, `AiringPage`, `RecapPage` and `SeriesBrowserPage` all write filter state there. Keying on the full location would let those close an open overlay for no reason the user would recognise.

### D6 — Mount-time capture in a ref, and the close callback in a ref too

The effect must not fire on the initial render, so the pathname at mount is captured in a ref and compared. `onClose` is read through a ref updated on each render rather than listed in the effect's dependencies: several call sites pass a freshly-created arrow (`() => onClose(null)`), so depending on its identity would re-run the effect on every render for no benefit. The effect depends on `pathname` alone and fires exactly once per page change. Under StrictMode's double-mount the ref is re-initialised alongside the effect, so the comparison still matches and nothing closes spuriously.

### D7 — The three stats-row controls share one class

Choose picture and Choose title move into `series-page__rebuild-row` ahead of Rebuild, and the button styling in `.series-page__rebuild` is renamed to `.series-page__stats-action`, applied to all three. They stop using `series-page__related-link`, which exists to make an anchor look like a button and is shared with the three external links they are being separated from — keeping it would leave them looking like links in their new home. `series-page__artwork-controls` and its rules are deleted rather than left empty.

Rebuild stays last in the row so the "Some entries couldn't be loaded yet" / "too large to show in full" notices that follow it still read as qualifying it.

## Risks / Trade-offs

- **Options shift as images load: a landscape option widens from its reserved portrait footprint** → Accepted, and bounded by D2: the reservation is right for the majority of options and wrong only for landscape ones, which are the minority; the alternative costs a measurement pass over twenty-plus images. A revisit is served from cache and decodes before first paint, so the shift is a first-visit effect.
- **140px is a guess at "smaller so more fit"** → It is a single custom property on the grid container, so retuning it after looking at a real franchise pool is a one-line change.
- **`Modal` now requires router context** → Every `Modal` in the app renders inside `BrowserRouter`, which wraps `App` at the root in `main.tsx`; there is no overlay outside it and no plan for one. An overlay rendered outside the router would throw at `useLocation()` — loudly, at first render, not subtly.
- **A future overlay that wants to survive a navigation it triggers itself** → None exists today; every overlay containing links (related-anime, score board, edit history, unresolved episodes) wants exactly the new behaviour. If one ever needs the old one, an opt-out prop on `Modal` is the escape hatch — not added now.
- **Closing on navigation discards unsaved editor values** → Identical to what Escape and click-outside already do, so it opens no new way to lose work; the spec states it explicitly rather than adding a confirmation the app has nowhere else.

## Migration Plan

None. Frontend-only, no persisted state, no API surface, nothing to migrate or backfill. The change is a CSS rewrite of one component's option tile, one effect in one shared component, and a JSX move within one page. Rollback is a revert.

Build note: the default `node` on this machine is v16; the Vite build needs nvm's v22.

## Open Questions

None blocking. The option height (D3) and the resulting row density are the only judgement calls, and both are one CSS value.
