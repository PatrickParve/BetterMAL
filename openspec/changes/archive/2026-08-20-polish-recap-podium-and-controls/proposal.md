## Why

The recap is the page that answers "what was this period *like*?", but it renders that answer as uniformly as a spreadsheet: the year's best anime is a 36×50 thumbnail in a row identical to the ninth-best, the stat block is eight grey boxes, and the controls that shape the whole page give no feedback at all once selected — hovering the already-active **Yearly** tab looks exactly like not hovering it, so the cluster feels dead under the pointer. The season recap's one outbound link is bare underlined text sitting beside three properly-sized controls, and the navigation bar's score switch has its knob flush against the pill's edge with a downward-cast shadow, so the eye reads as sitting low and smudging the border it should sit inside.

## What Changes

- **The top 3 become a podium.** The recap's top 10 splits into a three-card podium and a list of ranks 4–10. #1 sits centre and raised, #2 and #3 flank it lower — the silhouette of a podium — each card carrying full poster art, its title, its score as the app's existing score chip, and a medal-coloured rank badge (gold, silver, bronze). The cards are placed by rank, so #1 is still first in reading and keyboard order.
- **The podium moves.** Cards rise into place when a period loads, lift and light up under the pointer, and #1 carries a slow sheen across its medal band. Every one of these is a compositor-only transform/opacity effect that reflows nothing, and all of them are switched off under `prefers-reduced-motion`.
- **Stat tiles get a shape of their own.** Each tile gains a medal-free but accent-keyed identity: a coloured rail down its leading edge, larger tabular figures, and a hover that lifts the tile rather than only tinting it. Followable tiles stay visibly distinct from aggregate ones, as they are today.
- **Selected controls read as selected, and respond when hovered.** The mode tabs, the time filter, and the ranking-basis toggle get a richer selected treatment (accent fill with a soft accent halo) *and* — the gap today — a distinct hover state while already selected, so the pointer always gets an answer. Unselected hover, disabled, and keyboard focus each get their own unambiguous state; the tabs currently have no visible focus ring at all.
- **The season-page link becomes a button.** A season recap's "browse this season" control renders as a real button matching the height of the controls beside it, with the same hover and focus treatment the app's other controls use.
- **The score switch's knob stops covering its track.** The knob is inset within the pill and vertically centred, with a tighter shadow, so neither it nor its shadow paints over the pill's border in either state.

## Capabilities

### New Capabilities

None — every change refines behaviour already covered by existing specs.

### Modified Capabilities

- `list-recaps`: the top three of the top 10 are presented as a podium of cards rather than as rows, with rank identity, poster art, and motion; the remaining ranks continue as rows and continue their numbering; stat tiles gain a rank/role rail and a lift on hover (the "no reflow" guarantee is restated as "no layout reflow", which compositor-only transforms satisfy); the recap's segmented controls gain defined selected, selected-hover, unselected-hover, disabled, and focus states; the season-page control is a button rather than a text link.
- `score-visibility`: the global hide switch's knob SHALL sit within its track without obscuring the track's border, in either state and including any shadow it casts.

## Impact

- **Frontend only.** `RecapPage.tsx` (podium markup split out of `renderTopTen`, season-page button, stat tile markup) and `RecapPage.css` carry most of the work; `Navbar.css` carries the knob fix; `index.css` gains three medal colour tokens (plus dark-mode values) beside the existing status tokens.
- **No backend, DTO, or API change.** The podium is a presentation split of data the recap response already returns; no new fields, no new request.
- **Reuses existing components** — `ScoreChip` for the podium scores and `ScoreValue` for hidden MAL scores, so score visibility and the reveal control keep working untouched.
- **First animation in the app.** No `@keyframes` exists in `frontend/src` today, so this change also establishes the motion vocabulary (durations, easing, and a single reduced-motion guard) that later work can follow.
- **Spec-covered behaviour that must not regress**: "Recap rows highlight on hover" (ranks 4–10, hot takes, rankings), "Top 10 of the period" (ten items, tie order, unscored last), "Top 10 ranking basis control" including hidden MAL scores, and "The stat block is headed and responds to hover".
