## Context

Five presentation changes across two surfaces, all sitting on machinery that already exists:

- **The top 10** is `RecapPage.renderTopTen`: it narrows `recap.items` by media type, sorts by the selected basis (`myScore` or `malScore`, unscored last, title as tie-break), slices ten, and renders one `<ol class="recap-top-ten-list">` of identical `.recap-top-ten-row`s — a 28px rank cell, a 36×50 poster, an ellipsised title, and a score cell. Ranks 1 and 9 are visually indistinguishable apart from their numeral.
- **The stat block** is `RecapPage.renderStats`: eight tiles in a two-column grid inside the lead grid's `minmax(14rem, 20rem)` aside. Each tile is a value + label in a `--code-bg` box; a tile with a target renders as `<Link class="… --link">`, a zero-count followable tile as a `<div>` with the same `--link` class, an aggregate tile as a bare `<div>`. That three-way distinction is spec'd ("Drilling into a recap stat") and must survive.
- **The segmented controls** are three groups — `.recap-page__mode-tabs`, `.recap-page__filter-toggle`, `.recap-page__basis-toggle` — all built from the same `.recap-page__tab` class. Today `--active` sets a flat `--accent` fill with `#fff` text, and `.recap-page__tab--active:hover` is declared with *identical* declarations, so hovering a selected tab is a no-op. There is no `:focus-visible` rule on `.recap-page__tab` at all.
- **The season-page control** is `.recap-page__season-link` — an accent-coloured, underline-on-hover `<Link>` sitting in `.recap-page__controls` beside `--control-h`-tall selects and 32px round stepper arrows.
- **The score switch** is `.navbar__score-switch`: a 100×`--control-h` (34px) pill, `content-box` sized, with a 34×34 `border-box` knob at `top: 0; left: 0` and `box-shadow: var(--shadow)` — a shadow whose primary layer is offset `0 10px 15px -3px`. The knob's circle is exactly as tall as the pill's padding box, so its own 1px border traces the pill's border on the caps, and its shadow casts 10px downward over the pill's bottom border.
- **Motion**: `grep -rn "@keyframes" frontend/src` returns nothing. The app's only reduced-motion block is `Navbar.css`'s. Every existing hover is a `transition` on `background-color`/`border-color`/`box-shadow` at `0.12s ease`.

## Goals / Non-Goals

**Goals:**

- The best anime of a period is unmistakably the best one, at a glance, from across the room.
- The recap gains personality — colour, depth, and a little movement — without any of it costing a layout reflow or breaking under `prefers-reduced-motion`.
- Every state of every recap control (unselected, unselected-hovered, selected, selected-hovered, disabled, keyboard-focused) is visually distinct, and each one is reachable and legible by keyboard.
- The score switch's knob and its shadow stay inside the track, in both states.
- Nothing about *which* anime, *which* order, or *which* scores are shown changes; hidden MAL scores stay hidden.

**Non-Goals:**

- Changing what the top 10 ranks on, how ties break, or how many entries it shows. The podium is a presentation split of the same ten.
- A podium anywhere else (profile favourites, season page, top anime). The medal tokens land in `index.css` so they *can* be reused, but this change uses them in one place.
- Any change to `ScoreVisibilityContext`, its persistence, its per-score reveal, or the `--control-h` token.
- Restyling the rankings, the hot takes, the rating distribution, or the "See all" overlay.
- Any backend, DTO, or request change.

## Decisions

### 1. The podium is a second list, not the same list restyled

`renderTopTen` splits its ten into `topTen.slice(0, 3)` and `topTen.slice(3)`, rendering `<ol class="recap-podium">` followed by `<ol class="recap-top-ten-list" start={4}>`. The rows keep their current markup, class names, and hover treatment untouched.

`start={4}` is what keeps the two lists one ranking rather than two: the second list's own `<li>`s continue the numbering semantically for assistive technology, matching the `#4`… numerals the rows already print. (The rank numeral stays an explicit `<span>` as it is today; `start` fixes only the implicit list semantics.)

*Alternatives considered.* Keeping one `<ol>` and promoting the first three children with `:nth-child(-n+3)` would avoid the split, but a podium card and a row differ in element structure, not just in CSS — the card needs a badge, a two-line title, and a chip that the row has no place for. Rendering the podium from a separate `recap.items` pass would risk the two lists disagreeing about rank when scores tie; slicing one already-sorted array cannot.

### 2. Placement by `grid-column`, so visual order and rank order can differ safely

The podium is a three-column grid, `grid-template-columns: minmax(0, 1fr) minmax(0, 1.24fr) minmax(0, 1fr)`, `align-items: end`. The cards stay in rank order in the DOM (#1, #2, #3) and are placed explicitly: #1 → column 2, #2 → column 1, #3 → column 3. `align-items: end` bottom-aligns all three, so #1's greater height is what raises it — no negative margins, no `position: relative` nudges, and the three bottom edges stay on one line at every width.

DOM order is deliberately rank order rather than visual order: a screen reader and the tab key both travel #1 → #2 → #3, which is the order the ranking means. The accepted trade-off is that tab order moves centre → left → right visually. `order:` on flex would produce the same visual result with the same caveat while *also* being ignored by nothing — this is the standard spatial-vs-sequential tension, and rank order is the right sequence to preserve.

Below 720px the grid collapses to one column and every card takes the #1 size, so the podium degrades to three full-width cards in rank order — where DOM order and visual order agree.

### 3. Medal colours are three tokens in `index.css`, beside the status tokens

`--medal-gold`, `--medal-silver`, `--medal-bronze` plus `-bg` and `-border` variants for each, with dark-mode values in the existing `@media (prefers-color-scheme: dark)` block — exactly the shape `--status-*` and `--mal`/`--mine` already use. The card reads its own colours through one local alias (`--medal`, `--medal-bg`, `--medal-border`) set by `.recap-podium__card--gold|silver|bronze`, so every rank-coloured surface inside the card (badge, rail, glow, sheen) is written once against `var(--medal)` rather than three times.

Silver is the hard one: a literal silver is nearly the light theme's `--border` and nearly invisible against a white card. The tokens use a cool slate-blue that reads as "silver" through the family rather than through the literal hue, and the light-theme values are darkened enough to clear text contrast on their own tint.

*Alternative considered.* Hard-coding the three colours in `RecapPage.css` would keep `index.css` free of a page's palette, but the app's convention is that a colour with meaning is a token in `index.css` (that is exactly why `--mal`/`--mine` are aliases rather than raw values), and the profile page's favourites are an obvious future second consumer.

### 4. Motion is compositor-only, and one query switches it all off

Three effects, all on `transform`/`opacity` only:

- **Entrance** — a `recap-podium-rise` keyframe (translateY(12px) + opacity 0 → 0, 1) over 420ms `cubic-bezier(0.22, 1, 0.36, 1)`, staggered by rank via `animation-delay` (#1 0ms, #2 80ms, #3 160ms) so the podium assembles rather than appearing. Keyed on the card element, which React remounts whenever the period, filter, basis, or type changes — so the podium re-animates on every genuinely new set, and never on an unrelated re-render.
- **Hover** — `transform: translateY(-4px) scale(1.015)` plus a medal-tinted glow, 160ms.
- **#1's sheen** — a `recap-podium-sheen` keyframe translating a narrow `linear-gradient` highlight across the card's medal band on a slow loop (~5s), inside `overflow: hidden`, `pointer-events: none`, `aria-hidden`.

`transform` and `opacity` are the two properties that never trigger layout, so the "hover does not reflow" guarantee holds even though the card visibly moves — this is why the spec delta says *layout reflow* rather than *position*. One `@media (prefers-reduced-motion: reduce)` block sets `animation: none` and `transition: none` for all of them and drops the hover transform, leaving the colour change as the whole hover treatment.

*Alternative considered.* Animating `box-shadow`/`top` would be simpler to write and would reflow or repaint the whole card each frame; it is also what the "no reflow" spec language was written to forbid.

### 5. Stat tiles gain a leading rail, and lift on hover

Each tile becomes `position: relative` with a 3px full-height rail on its inline-start edge drawn as a `::before` (not a `border-left`, which would change the tile's box and reflow the grid). The rail is `--accent` on a followable tile and `--border` on an aggregate tile, so the spec'd "followable vs aggregate" distinction is legible *at rest* rather than only on hover — today it only appears under the pointer.

Hover adds `translateY(-2px)` on followable tiles and keeps the existing quieter background-only change on aggregates. The value line grows to 26px with `font-variant-numeric: tabular-nums` so two tiles side by side have their digits on the same rhythm, and the label goes uppercase-with-tracking to separate it from the figure.

The tiles keep their identical box, padding, and grid — the rail is inside the padding box and the lift is a transform — so the block still never reflows.

### 6. One `--tab` state table, applied to all three control groups

`.recap-page__tab` gets five explicit states rather than the two-and-a-half it has now:

| State | Treatment |
|---|---|
| resting | `--bg`, `--border`, `--text` (unchanged) |
| hover, unselected | `--accent-bg` tint, `--accent-border`, `--text-h` |
| selected | `--accent` fill, `#fff`, plus a soft `0 0 0 3px --accent-bg` halo ring |
| hover, selected | fill lightens one step via `color-mix(in srgb, var(--accent) 84%, white)`, halo widens to 5px, `translateY(-1px)` |
| focus-visible | `outline: 2px solid var(--accent); outline-offset: 2px` (new — there is none today) |
| disabled | current `opacity: 0.4` + `not-allowed`, and explicitly no hover/lift |

The halo is `box-shadow`, so it does not affect the control's box and the cluster's shared `--control-h` height is untouched — which matters, because these tabs sit in the same clusters as `--control-h` selects and the new season button.

`color-mix` is used rather than a fourth token: it derives the hover fill from `--accent` itself, so it stays correct in both themes and if the accent ever changes. It is supported in every browser this app already assumes for `:has`-free modern CSS, and degrades to the unhovered fill if unsupported — a hover that does nothing, which is exactly today's behaviour, not a broken control.

### 7. The season control becomes a `<Link>` styled as a button, not a `<button>`

It stays a `react-router` `<Link>` — it navigates, so it must keep being a real anchor for middle-click, ⌘-click, and "copy link address" — and takes a new `.recap-page__season-button` class giving it `--control-h`, the pill radius, the border/hover/focus treatment the app's controls share, and a leading calendar-ish glyph. The label shortens to "Browse the season" (the period is already named in the `<h1>` and in the controls beside it, so repeating "Fall 2019" three times on one line is noise) with a trailing `›`.

*Alternative considered.* An actual `<button>` with `useNavigate` would look identical and break every browser affordance an anchor gives for free.

### 8. The knob is inset in the track, and its shadow is tightened

The knob shrinks to 26px, is centred vertically with `top: 50%; transform: translate(0, -50%)`, and sits at `left: 3px` — leaving a 3px gap to the pill's inner edge all round, which is what stops it painting over the border. Its `box-shadow: var(--shadow)` (offset 10px down, 15px blur) is replaced with a tight `0 1px 2px rgb(0 0 0 / 0.18)` that cannot reach the border from 3px inside it.

Because the knob now composes translation with the centring translate, the checked position becomes `transform: translate(calc(100% - 26px - 6px), -50%)` — expressed against the track's own width so the two end gaps stay equal if the pill's width ever changes, rather than the current hard-coded `translateX(66px)`. The `.navbar__score-switch-track` label's reserved width changes from `calc(100% - 34px)` to `calc(100% - 32px)` to match the knob's new footprint (26px + two 3px gaps).

The pill keeps `height: var(--control-h)` and its `content-box` sizing, so the navbar's control row is unchanged — 34px content + 2px border, as before, and the switch stays exactly 100px wide in both states.

*Alternative considered.* Keeping the 34px flush knob and only removing the shadow would fix the smudge but not the knob border tracing the pill border on the caps, which is the other half of what "covering the border" describes. Insetting fixes both and is what every other switch in the wild does.

## Risks / Trade-offs

- **Podium tab order is centre → left → right** → Accepted deliberately (decision 2): rank order is the meaningful sequence, and it is the one screen readers and the keyboard follow. Below the collapse breakpoint the two agree.
- **The podium is much taller than three rows, inside the lead grid's left column** → The card poster is sized in `aspect-ratio: 2 / 3` off a capped column width, and #1's raise is a fixed `+28px` of card height rather than a percentage, so the podium's total height is bounded and predictable. Verified against the 1024px lead-grid collapse, where the podium sits full-width.
- **A period with fewer than three scored anime** → The grid places whatever it has: two cards leave column 3 empty (so #1 stays centre and #2 stays left, which still reads correctly), one card sits alone in the centre column. No placeholder cards. The rows list simply does not render when there is no rank 4.
- **A card's title is longer than the card** → Two-line clamp with `-webkit-line-clamp` plus a `title` attribute carrying the full name, as the rows already do. The card's height is fixed by the poster and reserves both title lines, so a one-line and a two-line title produce cards of identical height.
- **`color-mix` on the selected-hover fill** → Degrades to no visible hover change, which is today's behaviour; no state becomes unreadable.
- **The sheen loop runs forever on an idle page** → It animates one `transform` on one composited pseudo-element; it is off under reduced motion, and scoped to the single #1 card.
- **Medal colours against the accent purple** → Gold/silver/bronze are used only on the podium, and the accent stays the language for "followable/selected" everywhere else, so the two palettes never have to mean the same thing in the same place.

## Open Questions

None — the podium layout was chosen by the user (raised #1 with #2 and #3 flanking) and every remaining choice is settled above.
