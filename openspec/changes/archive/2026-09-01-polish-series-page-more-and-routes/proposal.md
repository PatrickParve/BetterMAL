## Why

The series page's More section opens with every relation group expanded, so a franchise with a dozen groups buries the page before the reader has asked for anything, and the media-type filter can't rescue it: selecting "Music" reveals which groups hold music but never opens them to show it. Meanwhile the stats box lies after an edit — favourites, most-rewatched and the time figures keep the values the server last sent, and on any route other than the default they keep them permanently, because the page reads a frozen per-pick snapshot the in-place edit path never touches. The watch-order numbers restate what left-to-right position already says, the route picker is crammed inside a 192px card at the slot position rather than marking where the route begins, and the two time figures sit as unrelated stat cells when they are two halves of the same question.

## What Changes

- **More opens collapsed.** Every relation group starts collapsed on a fresh visit instead of expanded; the "in my list" filter still starts on, and restoring the section (back/forward) is unaffected.
- **A selected media type opens the groups holding it.** While at least one type is selected, the type filter overrides each group's collapsed state so the matching entries are actually rendered, not just counted in a heading. It still composes with the "in my list" filter, so a group left showing nothing is not rendered at all.
- **Watch-order numbers are removed from the main-series cards.** The ordering itself is unchanged — story order, computed with each version slot contracted to one position — but the page no longer prints a number badge on each card; card sequence carries it. **BREAKING** for the spec's "numbered from 1" rendering claim, not for any stored data.
- **The route picker moves above the route's first card.** Where a main line holds a version slot, the picker renders in its own strip above the earliest visible card of the picked branch — Fate/stay night's buttons sit above the Unlimited Blade Works *prologue*, not above UBW itself — instead of inside the alternative's card.
- **The stats box stays live.** Every figure it shows — favourites, most rewatched, highest MAL score, entries completed, time watched, time left, and the progress bar and readout — recomputes from the page's own entry arrays after an in-place edit, and follows the picked route rather than reading the server's last per-pick snapshot. Reordering tied favourites works on any route, not only the default one.
- **Time watched and time left merge into one "Time" stat**, as two labelled rows (`Watched:` / `Left:`) in the shape "Entries completed" already uses. Each row's own show/hide rule is unchanged, and the stat disappears entirely when neither row qualifies.

## Capabilities

### New Capabilities

None — this change modifies existing series-page and series-versions behaviour.

### Modified Capabilities

- `series-page`: the More section's default group state and its restore default; the media-type filter's precedence over a collapsed group; the removal of rendered watch-order numbering; the stats box's freshness after an edit and across a route switch; time watched and time left presented as one stat.
- `series-versions`: a version slot's picker is placed above the first card of the route it selects, and the slot's shared watch-order position is no longer described as a rendered number.

## Impact

- `frontend/src/pages/SeriesPage.tsx` — More-section defaults, type-filter/collapse precedence, client-side stats recomputation replacing the `statsByPick` read for edited values, favourite reorder on a non-default pick, merged time stat.
- `frontend/src/components/SeriesTimeline.tsx` — number badge removed, picker hoisted out of the card and anchored above the route's first visible card.
- `frontend/src/pages/SeriesPage.css`, `frontend/src/components/SeriesTimeline.css` — stat-cell rows for the merged time stat; picker strip above the row and the card-header rules it replaces.
- No backend, API-contract, or database change: `SeriesStatsDto`, `SeriesStatsByPickDto` and `BuildStats` are untouched, and the client's recomputation mirrors them rather than replacing them.
