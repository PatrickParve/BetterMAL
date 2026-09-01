## Context

Six unrelated-looking complaints about the series page turn out to sit on three pieces of machinery.

**The More section's view state** (`SeriesPage.tsx`, `collapsedGroups` / `mineOnly` / `unfilteredGroups` / `selectedMediaTypes`) treats an absent group key as *expanded*, so the section opens fully expanded; and `visibleItems` short-circuits to `[]` for a collapsed group before the type filter is ever consulted, so selecting "Music" narrows the counts in the headings without opening anything.

**The timeline** (`SeriesTimeline.tsx`) renders a `.series-timeline__card-header` number badge per card and hangs the version-slot picker inside the card of the alternative it picks — which for Fate/stay night is the *second* card of the route, the prologue having been ordered before the slot.

**The stats box** reads `statsForPick(series, resolvedPick)`, which returns `series.stats` at the default pick and a frozen `series.statsByPick[i].stats` on any other. The in-place edit path (`patchSeriesEntry`) only ever writes `series.stats`, and only its `scores` and `myHighestScoreAnimeIds` fields. So an edit made on a non-default route moves nothing at all, and even on the default route `entriesCompleted`, the time figures, the progress bar and `mostRewatchedAnimeIds` stay at the values the server last sent. The favourites complaint is the visible face of this; the 2026-08-18 change already recorded the staleness as known and unfixed.

Backend behaviour is correct in all three areas — `BuildStats` computes exactly the right figures per pick, `SeriesVersionSlots` already records each alternative's branch. Everything here is client-side.

## Goals / Non-Goals

**Goals:**
- More opens collapsed, and the media-type buttons open the groups that hold the selected type.
- No watch-order number badges on main-line cards.
- The route picker sits above the first card of the route it selects, without knocking the row out of alignment.
- Every figure in the stats box reflects the page's current member data, on every route.
- Time watched and time left presented as one stat with two labelled rows.

**Non-Goals:**
- No backend change. `SeriesStatsDto`, `SeriesStatsByPickDto`, `BuildStats`, `SetFavouriteOrderAsync` and the series read endpoint are untouched.
- Not removing `statsByPick` from the wire. It stays the source for figures no edit can change and the basis the client derivation is checked against.
- Not changing what a version slot *is*, how branches are derived, or the default alternative rule.
- Not changing the "in my list" filter's composition with the media-type filter — a selected type still shows only the entries the list filter admits.

## Decisions

### 1. Collapsed is the absent-key meaning, not a seeded map

`collapsedGroups` stays `Record<string, boolean>` and keeps defaulting to `{}`; the read flips from `collapsedGroups[key] ?? false` to `collapsedGroups[key] ?? true`.

**Why not seed the map with every group key collapsed:** group keys are `${relationGroup}-${index}`, derived during render from the extras array. Seeding would need the series before the state hook runs, and would have to be re-seeded whenever a rebuild changes the groups. Flipping the default keeps the existing graceful-staleness property — a restored key naming a group that no longer exists is simply never read — and keeps `{}` meaning "nothing overridden" in both directions.

**Consequence to check at each call site:** `toggleAllExtrasGroups` writes explicit `true`/`false` for every key, so it is unaffected. `handleExtrasGroupHeadingClick` writes explicit values, unaffected. `toggleMineOnly`'s `setCollapsedGroups({})` is affected — see decision 2.

### 2. The "in my list" control stops touching collapse

`toggleMineOnly` currently resets `collapsedGroups` to `{}` in both directions, which under the old default expanded everything. Under the new default the same line would *collapse* everything, so turning the filter off would show nothing — the opposite of what the control means.

The control now sets `mineOnly` and clears `unfilteredGroups` only. Collapse belongs to the group headings and the expand/collapse-all control; the filter decides what an expanded group shows.

**Alternative considered:** keep the coupling and redefine the control as "return the section to its opening state" — which is what the current spec text literally says. Rejected: with collapsed-by-default that makes "in my list" a destructive reset button, and it leaves no way to re-apply the filter without losing every group you had opened.

### 3. Selecting a media type *opens* groups rather than overriding their collapse

`toggleMediaType`, when it adds a type, also writes `collapsedGroups[key] = false` for every group holding at least one entry of that type. It grants no `unfilteredGroups` exemption — only a heading does that.

**Why a real state change over a render-time override:** an override (`effectivelyCollapsed = isCollapsed && !typeFilterActive`) leaves the group headings as controls whose collapse branch does nothing visible while a type is selected, and forces a second concept — "shown but collapsed" — through the caret, `aria-expanded`, the hidden-count control and the all-groups control. Opening the groups outright is one line, leaves every other control behaving exactly as it does today, and matches what was asked for ("it should ... open it").

**Consequence:** deselecting a type leaves the groups it opened open. That is the same thing that happens when you open a group by hand, and undoing it is the all-groups control's job.

**Placement:** `toggleMediaType` moves down beside `toggleAllExtrasGroups`, below the early returns, since it now needs `extrasGroups` to know which keys to open.

### 4. The three tie lists are pick-independent and read from `series.stats`

`BuildStats` computes `highestMalScoreAnimeIds`, `myHighestScoreAnimeIds` and `mostRewatchedAnimeIds` from `mainLineMembers.Concat(extraMembers)` — the **whole, unfiltered** main line, deliberately, per the picked-route requirement's D6. Every `statsByPick` entry therefore carries identical values for all three.

So the page reads all three from `series.stats` directly, never from `statsForPick`. That single change makes the favourite list and the reorder buttons work on a non-default route, because `patchSeriesEntry` and `handleReorderFavourite` already write there.

`patchSeriesEntry` gains a `mostRewatchedAnimeIds` recomputation alongside the existing `myHighestScoreAnimeIds` one — a rewatch count *is* editable, unlike `malScore`, so leaving it out would keep "Most rewatched" stale. `highestMalScoreAnimeIds` needs none and keeps its existing comment saying why.

### 5. Pick-scoped, edit-sensitive figures are derived on every render, not patched

A new `frontend/src/utils/seriesStats.ts` exports `deriveSeriesStats(base, mainLineVisible, extras)`, returning `base` with the edit-sensitive fields replaced by client computations over the entry arrays:

| Field | Derived from |
| --- | --- |
| `myWatchedEpisodes` | Σ `effectiveWatchedEpisodes` over `mainLineVisible` |
| `myWatchedSeconds` | Σ `effectiveWatchedEpisodes × episodeSeconds` |
| `myRewatchedSeconds` | Σ `rewatchEpisodesIncludingCurrentRun × episodeSeconds` |
| `entriesCompleted` | count of `mainLineVisible` marked Completed or Rewatching |
| `extrasCompleted` | count of real extras marked Completed |
| `mainLineCompletedByMe` | `SeriesAverages.MainLineSettledByMe`'s rule over `mainLineVisible` |

Everything else — episode and runtime totals, aired figures, member counts, longest gap, studios, genres, and the three tie lists of decision 4 — passes through from `base`, which stays `statsForPick(series, resolvedPick)`.

**Why derive unconditionally rather than patch on edit:** a derivation with no "has been edited" flag produces the server's own numbers on first paint, so any divergence is a bug visible immediately rather than one that only appears after an edit. It also needs no per-pick patching: `statsByPick` has up to 24 entries, and patching each on every edit would mean re-deriving the visible member set per combination anyway.

**Mirrors, not re-inventions.** `effectiveWatchedEpisodes` already exists in `SeriesPage.tsx` and moves into the new module. `episodeSeconds(e) = e.averageEpisodeDurationSeconds ?? 24 × 60` mirrors `WatchMath.EpisodeSeconds`; `rewatchEpisodesIncludingCurrentRun` mirrors `WatchMath`'s method of that name including its published-total baseline. Each carries a comment naming the backend method it mirrors, as the existing client mirrors of `BuildStats` do.

### 6. The derivation excludes related entries, fixing an existing mismatch

`series.extras` is the *display* list: real extra members merged with related entries (`isRelatedEntry: true`), which the server excludes from every average and stat. `recomputeScores`/`recomputeMyHighestIds` in `SeriesPage.tsx` today pass `series.extras` whole, so an in-place edit silently re-bases the "Everything" averages onto a member set the server never used.

Every client computation added or touched here filters to `series.extras.filter(e => !e.isRelatedEntry)`, including the existing `recomputeScores` and `recomputeMyHighestIds`. This is why decision 5's derivation is checkable: on first paint the derived figures must equal the server's, which they cannot if the member basis differs.

### 7. Time watched and time left become one stat cell

The two `<div><dt>…</dt><dd>…</dd></div>` cells collapse into one `Time` cell whose `<dd>` holds up to two labelled rows, reusing the `series-page__entries-completed` markup pattern (`__…-row` + `__…-label`) rather than inventing a second two-row idiom. `showTimeWatched` / `showTimeLeft` keep their exact current expressions; the cell renders when either is true.

### 8. The picker rail is a per-column strip above the row, sized by CSS grid semantics

`SeriesTimeline` wraps each card in a `.series-timeline__col` and, **only when `slots.length > 0`**, renders a `.series-timeline__picker-rail` above the card in every column — holding the picker in the column that owns it and nothing in the rest. All rails sit in the same flex line, so every card's top edge stays aligned; a series with no slot renders no rails and reserves no space.

The rail must not widen its column past the card, so it takes the `width: 0; min-width: 100%` idiom — zero max-content contribution, full resolved column width — rather than a hard-coded 192px, which would be wrong for a landscape card's `--card-w: 384px`.

The picker itself changes from `flex-wrap: wrap` to `nowrap` with `flex: 1 1 0; min-width: 0`, so it is always exactly one line high and a fixed rail height is safe. Titles already ellipsize and already carry a `title` attribute with the full name.

**Which column owns the picker.** `SeriesVersionSlots` maps each alternative to its branch *including the alternative itself*, so an entry is on slot *S*'s picked route exactly when `entry.branchHeadAnimeId === resolvedPick[S]`. The owning column is the first entry of the rendered (already watch-ordered) array satisfying that — the prologue for Fate's UBW route, the alternative itself for Demon Slayer. One predicate covers both cases, and the alternative is always visible so an owner always exists.

`SeriesTimeline` needs the resolved pick to evaluate that predicate; it currently derives the picker from `entry.versionSlotKey` alone. It gains a `resolvedPick: Record<number, number>` prop, which `SeriesPage` already computes.

### 9. The number badge and the dead `rank` prop go together

`.series-timeline__card-header` / `__card-number` are removed from `SeriesTimeline.tsx` and its CSS. `SeriesEntryRow`'s `rank` prop and `.series-entry-row__rank` are removed too: `SeriesEntryRow` has had no caller since the timeline replaced the numbered list, and `rank` is the same idea this change deletes.

## Risks / Trade-offs

- **The client derivation drifts from `BuildStats`** → each derived figure is a direct mirror of a named backend method, commented as such, and equals the server's value on first paint, so drift shows up as a figure that changes when nothing was edited. The fields most at risk (runtime and episode totals, the aired figure) are deliberately *not* derived.
- **Deselecting a media type leaves groups open** → accepted (decision 3); the all-groups control collapses them, and the alternative was a group heading that silently does nothing while a type is selected.
- **A slot with many alternatives ellipsizes its buttons harder** now that the rail is one line and no longer wraps → the rail spans the card's full width and each button keeps its `title` attribute; no observed series has more than two alternatives in a slot.
- **The "in my list" control no longer resets the section** → the expand/collapse-all control already does exactly that, and the reset was only ever a side effect of collapse and the filter sharing a default.
- **A series page opens showing no extras at all** → intended, and the headings still state every group and its count, so what is there is visible without the tiles.

## Migration Plan

Frontend-only; no data, schema, or API change, so a deploy is a static bundle swap and a rollback is the previous bundle. Restored `pageStateStore` snapshots from a tab open across the deploy hold a `collapsedGroups` map whose absent keys now mean collapsed — a section that was left expanded may come back collapsed once. No error, and one press of the all-groups control restores it.

## Open Questions

None. The three that were open — picker anchoring, whether a selected type overrides the "in my list" filter, and how far the stats refresh reaches — were settled with the requester before this document: route's first card, compose with the list filter, every stat in the box.
