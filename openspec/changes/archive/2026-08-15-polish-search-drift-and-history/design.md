## Context

Five presentation defects, described in `proposal.md`. All five are frontend-only; three are pure CSS and two (the overlay's close control, the detail page's score lines) are small markup changes. Current state worth knowing before changing anything:

- **The root sets `font: clamp(16px, …, 18px)/145%` in `index.css`.** A percentage line-height computes to a *length* on `:root` (~23–26px) and inherits as that length, not as a ratio. So `.series-badge` — 10px text — still sits in a ~23px line box, even though `.series-badge__pill` sets `line-height: 14px` on the pill itself. This is why the dropdown's series row is ~48px of text (title ~23 + 2 gap + badge line ~23) against the anime row's 40px thumbnail, and why the last badge fix — which shrank the pill for the results grid — did not fix the dropdown.
- **The badge sits in two different places.** On the results page it is passed as `children` to `AnimeCard`, *not* into `.anime-card__meta`; that geometry was tuned in `polish-score-and-badge-ui` and currently matches the anime cards. In the dropdown it sits in `.search-bar__result-text` under the title. Only the dropdown is broken.
- **`.search-bar` is two boxes joined at a seam**: the input has `border-right: none` and radius on its left corners, the button has its own border and right radius. The input's focus style is `outline: 2px solid var(--accent); outline-offset: 2px`, drawn *outside* the input's border box — which is exactly where the button is.
- **Nothing in the app sets `overscroll-behavior`**, and the horizontally scrolling regions (`.carousel__track`, `.series-timeline__scroll`, `.top-anime-strip`, `.rewatched-strip`, `.top-series-strip`) are plain `overflow-x: auto`. A trackpad flick that runs past a strip's end chains to the document, which rubber-bands and — depending on browser and history — can start a back/forward swipe. Nothing in the app uses `position: sticky`, so an `overflow-x` on the shell is safe.
- **`EditHistoryOverlay`** renders a bare `<h2>` followed by filters, the list, and an `.edit-history__buttons` footer holding a labelled Close button. Its rows are `min-height: 78px` with a 1px border (80px outer) and a 6px gap; the list caps at `max-height: 55vh`, a number unrelated to that row height, so where it lands mid-row depends on the window.
- **`Modal`** already owns Esc and click-outside dismissal and gives the dialog `max-height: 85vh; overflow-y: auto`. The overlay's list scrolls inside that.
- **The detail page's scores are `ScoreChip`s inside `.detail-box` panels.** The first panel holds `<p>Rank: …</p>`, `<p>Popularity: …</p>`, then a `ScoreChip role="mal" label="MAL score"` wrapping a `ScoreValue`; the second holds a `ScoreChip role="mine" label="My score"` followed by `<p>` lines for rewatch count and completed date. So each panel is plain labelled lines plus one tinted, bordered block — a box inside a box. `AnimeDetailPage.css` carries `.anime-detail-page__score-boxes .score-chip { min-width: 0 }`, added in `polish-score-and-badge-ui` purely to stop the shared chip's 130px floor from setting the panel's width.
- **The colour-only form already exists app-wide.** `index.css` defines `.score--mal` and `.score--mine` — "the value alone, colour-keyed and tabular, with no chip around it" — used by `MyListRow`, `TopAnimePage`, and the profile's divergence lines. `ScoreValue`'s reveal button was changed to `color: inherit` in that same earlier change, so it picks up whichever of those classes surrounds it.
- **`ScoreChip` has five other callers**: the series page's four average chips (default size, labelled) and the compact pairs on `SeriesTimeline`, `SeriesExtraTile`, and the profile page's top-series tiles. None of them is a labelled line among labelled lines; all of them are figures standing alone.

## Goals / Non-Goals

**Goals:**

- The dropdown is one fixed row height, by construction rather than by both branches happening to measure the same.
- The search field reads and behaves as one control, with focus drawn inside its own box.
- Sideways gestures never move the page, while the strips keep scrolling and keep their drag-to-scroll and restored offsets.
- The history overlay closes from a corner ✕ and opens on exactly five whole rows, at any window height.
- The detail page's two scores read as coloured numbers on labelled lines, using the colour-only form the app already has, with hide/reveal untouched.
- Each fix stays in the file that owns the surface; no shared component is re-tuned for one caller.

**Non-Goals:**

- No change to the results-page grid, the badge as it appears there, or `SeriesBadge`'s own sizing — that geometry is correct today and this change must not disturb it.
- No change to search behaviour: ranking, the local+live merge, the 5-row budget, debouncing, Enter/magnifier submission, or the query-in-URL sync.
- No new modal chrome: `Modal` keeps its current API and dismissal; the ✕ belongs to the history overlay, not to every dialog.
- No redesign of the history rows, the filters, or the fetch.
- No app-wide line-height change. The root's `145%` stays; only the two dropdown line boxes are pinned.
- No change to `ScoreChip` or to any of its other callers, and no removal of the chip form from the app — the detail page stops using it, that is all.
- No change to the detail page's two `.detail-box` panels themselves (settled with the user): they keep their border, their padding, and their other lines. Only the chips inside them go.
- No change to score *visibility* anywhere: the hide toggle, the per-score reveal, the reserved slot width, and the completed-scores setting all keep working as they do.

## Decisions

### 1. The dropdown row gets a fixed height, and the series stack is sized to fit inside it

`.search-bar__result` gets `height: 52px` (the 40px thumbnail plus its 6px vertical padding, which is what an anime row measures today) with `box-sizing: border-box`. Both row kinds then have the same height whatever they contain, so the dropdown's height is a function of row count alone and cannot shift as matches change mid-typing — which is the actual complaint, not the 8px itself.

Inside that, the series stack must fit in the 40px content box: `.search-bar__result-title` gets `line-height: 20px` and the badge line 16px, plus the existing 2px gap — 38px. The title is already `white-space: nowrap` + ellipsis, so it can never take a second line and break the fixed height.

*Alternatives:* let the row auto-size and merely equalise the two line boxes — rejected, it makes row height depend on inherited typography that changes with viewport width (the root font is `clamp()`-fluid), so the two kinds would drift apart again at some width. Give anime rows an invisible second line — rejected, fake content to fix a layout number. Shrink the thumbnail — rejected, it makes every dropdown row worse to fix one.

### 2. The badge's tight line-height is scoped to the dropdown, not put on `SeriesBadge`

The pin goes in `SearchBar.css` as `.search-bar__result .series-badge { line-height: 16px }`, not into `SeriesBadge.css`. On the results page the badge sits under a card title where the inherited ~23px line box is what makes a series card match an anime card's meta line; tightening the badge globally would make series cards *shorter* than the anime cards beside them, trading one geometry violation for its mirror image. `SeriesBadge.css` keeps owning what the badge *is* (pill scale, colours, spacing); `SearchBar.css` owns how tall a dropdown row is.

*Alternatives:* set `line-height: 1` on `.series-badge` in its own file — rejected for the reason above. Add a `--dropdown` modifier prop to the component — rejected, a CSS-only difference does not need a component API.

### 3. The search field becomes one bordered wrapper with an inset magnifier, and focus is drawn inset

`.search-bar` itself takes the border, background, and 8px radius. The input inside goes borderless and transparent (`border: none; background: none; outline: none`), and the button loses its own border and left seam, sitting inside the field's right edge. Focus is expressed on `.search-bar:focus-within` as `border-color: var(--accent)` plus `box-shadow: inset 0 0 0 1px var(--accent)` — an *inset* ring, so it renders inside the field's own border box and cannot reach the magnifier or the navbar controls beside it, at any of the root's fluid font sizes. The magnifier keeps a separate, smaller inset ring for its own keyboard focus (`outline-offset: -2px`, which it already has), scoped to the button box inside the field.

Suppressing the input's default outline is acceptable here *because* the wrapper renders a focus indicator in its place — `:focus-within` fires for exactly the same interactions, and the indicator is a border-colour change plus a ring, not colour alone.

*Alternatives:* keep the two boxes and just flip the input's `outline-offset` to negative — rejected, it fixes the overlap but leaves the seam, and the user asked for a restyle. An outer (non-inset) ring on the wrapper — workable, but an inset ring is what makes "stays within its own bounds" true by construction rather than by measuring gaps in the navbar. `:has()`-based styling — unnecessary, `:focus-within` is exactly this.

### 4. Horizontal drift is stopped at both ends: the document refuses it, the strips do not hand it over

Two rules, because either alone leaves a hole:

- `html, body { overscroll-behavior-x: none }` in `index.css` — kills the document's own horizontal rubber-band and the browser's swipe-to-navigate gesture. Declared on `html` explicitly rather than relying on body-to-viewport propagation, which only applies when `html` says nothing.
- `overscroll-behavior-x: contain` on each horizontally scrolling region — `.carousel__track`, `.series-timeline__scroll`, `.top-anime-strip`, `.rewatched-strip`, `.top-series-strip` — so a flick that runs past a strip's last tile stops there instead of chaining outward. `contain`, not `none`: the strip keeps its own end-of-scroll bounce, it just does not pass the gesture on.

`#root` additionally gets `overflow-x: clip` as a backstop against any child that overflows the shell. `clip` rather than `hidden` because `hidden` makes the element a scroll container (and would force the vertical axis into one too); `clip` does not, and nothing in the app uses `position: sticky`, so there is no sticky context to break.

*Alternatives:* a JS `wheel` listener calling `preventDefault` on horizontal deltas — rejected, it fights the platform, needs a passive-listener opt-out, and would break the strips' own scrolling. `touch-action: pan-y` — wrong tool: it governs touch, not the trackpad gestures this is about, and would disable legitimate touch panning of the strips.

### 5. The overlay's ✕ lives in a header row, and the footer button is deleted

`<h2>` and the close button go into an `.edit-history__header` flex row (`justify-content: space-between; align-items: center`), so the ✕ sits in the top-right corner aligned with the title. The button is a real `<button type="button">` with `aria-label="Close history"` holding an inline ✕ SVG in the same shape as `SearchBar`'s `SearchIcon` — the app's existing idiom for icon controls — and takes the hover/focus treatment of the other icon controls (`.navbar__settings`). The `.edit-history__buttons` block and its CSS are removed, not hidden: the requirement says one close control, and two would be redundant with Esc and click-outside already present.

It is deliberately *not* added to `Modal`. `Modal` is shared with the entry editor, the completion prompt, and the top-anime selection overlay, each of which has its own footer and its own idea of what dismissing means (the completion prompt returns a value); giving every dialog a corner ✕ is a separate decision this change should not make by accident.

*Alternatives:* keep the footer Close *and* add the ✕ — rejected, the spec asks for the replacement, and two controls for one action in a dialog that also closes on Esc and click-outside is noise. Absolutely position the ✕ over the modal's padding — rejected, it would overlap the title at narrow widths, where the header row simply keeps them apart.

### 6. The list height is derived from the row height via one shared custom property

`.edit-history` declares `--history-row-h: 78px`; `.edit-history__row` uses it for `min-height`, and the list uses `max-height: calc(5 * (var(--history-row-h) + 2px) + 4 * 6px)` — five outer row heights (content plus the row's 1px top and bottom borders) plus the four gaps between them, i.e. 424px. One number, two consumers, so the two cannot drift the way `78px` and `55vh` did.

`max-height`, not `height`: a history of two rows shows two rows and no reserved space, matching how the profile page's own lists behave. The list keeps `scroll-y`, so the gutter stays reserved beside it and the remaining rows scroll.

This replaces the viewport-relative cap entirely — the whole point is that five whole rows do not depend on window height. The modal's own `max-height: 85vh` still protects short windows: below roughly 650px of viewport the dialog scrolls as a whole, as it does today.

*Alternatives:* a container-query row height like `.activity-feed`'s (`100cqh`-derived) — rejected, that pattern exists because the feed must *fill* a box whose height comes from its neighbours; the overlay has no such constraint, so deriving the container from the row is the simpler direction. Round to `max-height: 424px` directly — rejected, it is the same number with the reasoning deleted.

### 7. The detail page's scores become labelled lines using the existing colour-only classes

Each `ScoreChip` in `AnimeDetailPage.tsx` is replaced by a `<p>` in the same form as the `Rank:` and `Popularity:` lines around it — `MAL score: <span class="score--mal"><ScoreValue …/></span>` and `My score: <span class="score--mine">…</span>`. `.score--mal` / `.score--mine` are the app-wide colour-only classes from `index.css`, already the form used by My List, Top anime, and the profile's divergence lines; this is the existing "coloured value" density, not a new one, so the detail page joins a pattern rather than inventing a sixth score treatment. The label stays in ordinary text, as it does on every other line in those panels.

The colour class goes on a `<span>` *wrapping* `ScoreValue`, not inside it. That matters for the hidden case: `ScoreValue` renders its reveal button with `color: inherit` (decision 2 of `polish-score-and-badge-ui`), so a wrapping `.score--mal` makes the eye MAL-blue exactly as the chip's `.score-chip__value` did. Wrapping also preserves the reserved-width slot `ScoreValue` sets on itself, so revealing a score still shifts nothing.

`.anime-detail-page__score-boxes .score-chip { min-width: 0 }` is deleted with the chips — it existed only to defeat `.score-chip`'s 130px floor on this page. `ScoreChip` and its five other callers are untouched.

*Alternatives:* keep `ScoreChip` and add a `size="bare"` variant — rejected, it would be a chip component whose job is to not be a chip, and the app already has the bare form as two CSS classes. Put `.score--mal` directly on the `<p>` — rejected, it would colour the label too; the requirement is that the number carries the colour. Drop the panels as well — rejected by the user's choice: the panels stay, only the chips go.

## Risks / Trade-offs

- **A fixed 52px dropdown row could clip a title at a large root font size** → The title is a single nowrap ellipsised line at `line-height: 20px` inside a 40px content box, so there is 20px of headroom; the fluid root only spans 16–18px. Verify at a 2400px-wide window, where the root font is at its 18px cap.
- **Pinning the dropdown's title line-height decouples it from the app's typography** → Accepted, and deliberately scoped to `.search-bar__result` so nothing else inherits the decision. It is the same trade the row height itself makes.
- **Removing the input's native outline could leave focus invisible if the `:focus-within` rule is ever dropped** → The two live in the same rule block in `SearchBar.css` with a comment tying them together; and the field is exercised by keyboard in verification (tab into input, tab to magnifier).
- **`overflow-x: clip` on `#root` could hide a real overflow bug instead of surfacing it** → It is a backstop, not the fix; the strips' containment is what stops the drift. Before adding it, confirm no page actually has a horizontal scrollbar today, so `clip` is not silently swallowing something.
- **`overscroll-behavior-x: none` on the document also disables browser back/forward swipe gestures inside the app** → That is the request. In-app back navigation stays available via the browser's own back button, the keyboard shortcut, and the app's links.
- **Strip containment must not disturb restored scroll offsets** → `overscroll-behavior` only affects what happens at a scroll boundary; it does not touch `scrollLeft`. Re-verify the `page-state-restoration` behaviour (strips restore their offset on back-navigation) after the change anyway, since it is the one behaviour adjacent to this rule.
- **A 424px list plus header, filters, and modal padding may exceed 85vh on a short laptop window** → Then the modal scrolls as one, and the ✕ scrolls with it. Acceptable: the list's own scroll handles every ordinary window, and the overlay still closes on Esc and click-outside. Check at ~700px viewport height.
- **Losing the chips makes the two detail-page panels smaller and possibly lopsided** → They are already `flex: 0 0 auto`, so each shrinks to its own content; the "my score" panel may end up noticeably narrower than the rank panel. Check the pair still reads as a deliberate block beside the title in all three states — scored entry, unscored entry (no second panel at all), and an entry with no rewatch count or completion date.
- **A blue number in a plain line is a weaker signal than a tinted chip was** → That is the requested trade. The colour roles are unchanged and the label still names each score, so nothing becomes ambiguous; verify in both light and dark themes that the blue and purple are legible against the panel background at the line's font size.
- **The hidden-MAL-score case is the one that can silently regress** → It depends on the wrapping span carrying `.score--mal` and on `ScoreValue`'s `color: inherit`. Test the detail page with the hide toggle on, with "always show completed scores" on against a completed anime, and with a reveal followed by navigating away and back.

## Open Questions

None. Two judgement calls are settled: how far "restyle the search" goes (decision 3 — one bordered field, inset magnifier, inset focus ring, everything else unchanged), and how far the detail page's de-boxing goes (decision 7 — the chips go, the two panels stay, confirmed with the user).
