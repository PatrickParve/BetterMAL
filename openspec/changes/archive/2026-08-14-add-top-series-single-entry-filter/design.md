## Context

Top series loads once per profile visit (`GET /profile/top-series` → `TopSeriesSectionDto`) and does all of its narrowing and ordering client-side: `rankTopSeries` in `ProfilePage.tsx` filters out series with no value under the selected basis and sorts the rest. The ranking basis lives in `useRestorableState('topSeriesBasis', 'mine')`, so it resets on a fresh visit and is restored on back-navigation.

Every item already carries the size of its main line. `SeriesRankingIndex.EligibleSeries` computes both averages over `members.Where(m => m.IsMainLine)`, and `SeriesAverages.Mal`/`.Mine` return `all.Count` as `totalCount` — so `malMain.totalCount` and `mineMain.totalCount` are both exactly the number of main-line members. (`entryCount` is *not* it: that is `members.Count`, main line plus extras.)

This change is small and entirely in the profile page, but it touches three interacting pieces of that section — a second narrowing filter, a new control, and a third empty state — which is what makes the interaction rules worth fixing before coding.

## Goals / Non-Goals

**Goals:**

- A control in the Top series header that hides series whose main line is a single entry.
- Today's behaviour preserved byte-for-byte until the control is pressed.
- The new filter and the existing ranking basis compose without either resetting the other, and the section explains itself when the combination leaves nothing to show.

**Non-Goals:**

- No backend, DTO, or endpoint change.
- No persisted user setting — the control is page state like the basis, not a Settings entry.
- No change to ordering, to what a tile shows, or to any other surface (series page, series search, Most rewatched).
- No filtering by *total* member count, and no threshold beyond "more than one main-line entry" (no "3+ seasons" control).

## Decisions

### 1. Filter client-side from the already-loaded array

The section's data is loaded once and both narrowings are pure functions of it, so the toggle re-derives the displayed list rather than refetching — the same choice the ranking basis already made, and what keeps switching instant and non-collapsing. A server-side `?multiEntryOnly=` parameter would add a round-trip, a loading gap, and the "hold the last section on screen" workaround the other two strips need, for no gain.

### 2. Add `MainLineAiredCount` to the DTO rather than reading `malMain.totalCount`

The first cut of this change read the count off `malMain.totalCount` (both averages publish `totalCount` = main-line member count, so it was already on the wire and kept the change frontend-only). That undercounts what "multi-entry" should mean: `totalCount` includes a main-line member that's announced but hasn't aired a single episode, so a franchise whose second season is confirmed but not yet out already read as two entries — the opposite of what the filter is for.

Fixing that needs per-member airing status, which the averages don't carry (an average doesn't care whether its inputs have aired). `SeriesRankingIndex.EligibleSeries()` already has that status on every member projection (it uses `AiringStatus == "currently_airing"` for the MAL-reveal rule), so it computes `mainLineAiredCount = mainLine.Count(m => m.AiringStatus != "not_yet_aired")` alongside the two averages and carries it through `SeriesRankingResult` → `TopSeriesItemDto` as `MainLineAiredCount`. The frontend's `mainLineEntryCount(item)` helper reads that field instead, with the same "why" comment moved to point at it.

This does mean `TopSeriesItemDto` and `/profile/top-series` are no longer untouched (revises the proposal's Impact section) — a backend record field, a mapping line, and one new `SeriesRankingLookupTests` case, in exchange for the filter actually reading "has this really happened yet" rather than "is this on the schedule."

### 3. Filter before ranking, as a separate step

`rankTopSeries(items, basis)` stays as it is; the toggle applies as its own `filter` ahead of it. Order is unaffected either way (removing elements never reorders the survivors), so the deciding factor is that ranking and "which series belong in this strip at all" stay separately readable and separately testable.

### 4. A toggle button in the header row, not a third tab

The control goes in `profile-box__header-row` beside the `<h2>`, reusing `profile-box__control` (the "Full history" / "Edit order" button style) with `aria-pressed` and an `--active` modifier for the pressed look. The `profile-media-tabs` row underneath is a `role="tablist"` of mutually exclusive choices for the basis; a boolean filter is not one of those choices, and adding it there would imply picking it deselects "My score". `aria-pressed` is what makes the state readable to assistive tech, and the `--active` style is what satisfies the spec's "the control shows its state".

Label: **"Multi-entry only"** — stable across both states, with the pressed styling carrying the state. A label that flips between "Hide single-entry" and "Show single-entry" forces the user to work out whether it describes the current state or the action.

### 5. Empty-state precedence: basis first, then filter

Three branches already exist (nothing loaded/no items → build message; nothing rankable under the basis → basis message). The filter adds a fourth, and precedence matters because both narrowings can empty the strip at once. The page computes the basis-ranked list first and the filtered list from it:

- `topSeries.items.length === 0` → the existing "series are still being discovered" message with the Settings link.
- basis-ranked list empty → the existing per-basis message. This wins over the filter message even when the filter is on, because the user's basis is the reason nothing is rankable and pointing at the filter would be a false lead.
- basis-ranked non-empty but filtered list empty → the new message, naming the filter as the cause and offering no Settings link (the data is there; it is hidden).

### 6. State key `topSeriesMultiOnly`, default `false`

`useRestorableState('topSeriesMultiOnly', false)` gives the spec's reset-on-fresh-visit and restore-on-back for free, and independence from the other controls follows from each having its own key. The state is stored as "multi-entry only is on" rather than "single-entry shown" so the default is `false` — a fresh visit and an un-restorable snapshot both land on today's behaviour.

## Risks / Trade-offs

- **A franchise whose later seasons have not been built yet looks single-entry and gets hidden** → Series coverage grows in the background, so a partially-built series can have a main line of one. The filter defaults off, the strip is re-derived from fresh data on every visit, and the section already discloses that coverage grows; nothing is lost permanently.
- **Series vanishing with no explanation when the filter is on** → The pressed styling on the control plus the dedicated empty-state message make the cause visible; the filter is never on unless the user turned it on this visit.
- **The `malMain.totalCount` coupling is implicit** (decision 2) → Confined to one commented helper, so a future change to how the averages are scoped has one place to fix rather than several call sites.
- **Two narrowings on one strip can compound confusingly** (a series present under one basis, hidden under the filter) → Accepted: each control states its own effect, and the empty-state precedence in decision 5 never blames the wrong one.
