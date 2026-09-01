## 1. More section opens collapsed

- [x] 1.1 In `SeriesPage.tsx`, flip the collapsed default: read `collapsedGroups[key] ?? true` instead of `?? false` in the `extrasGroupView` map (design decision 1). Leave the `useRestorableState('moreCollapsedGroups', {})` initial value as `{}`.
- [x] 1.2 Walk every other reader/writer of `collapsedGroups` and confirm it still means what it says under the flipped default: `handleExtrasGroupHeadingClick` (writes explicit `true`/`false` — unaffected), `toggleAllExtrasGroups` (writes an explicit value for every key — unaffected).
- [x] 1.3 Change `toggleMineOnly` to set `mineOnly` and clear `unfilteredGroups` only, dropping its `setCollapsedGroups({})` line (design decision 2), and update its comment: the control governs what an expanded group shows and never changes a group's collapsed state.
- [x] 1.4 Verify `nothingHidden` still drives the all-groups label correctly from the new opening state — a freshly opened series has every group collapsed with entries hidden, so the control must read "Expand"/"Expand all".
- [x] 1.5 Update the comment above the `mineOnly`/`collapsedGroups`/`unfilteredGroups` state declarations, which currently documents "all expanded by default".

## 2. A selected media type opens the groups holding it

- [x] 2.1 Move `toggleMediaType` down beside `toggleAllExtrasGroups`, below the early returns, so it can read `extrasGroups` (design decision 3).
- [x] 2.2 When `toggleMediaType` *adds* a type, also write `collapsedGroups[key] = false` for every group holding at least one entry of that type. Removing a type SHALL change no collapsed state, and neither direction SHALL touch `unfilteredGroups` — only a group heading grants a filter exemption.
- [x] 2.3 Confirm the existing `.filter(({ typeAdmittedCount }) => !typeFilterActive || typeAdmittedCount > 0)` rule still hides groups the selected type does not admit, and that a group opened this way shows its type-admitted entries narrowed by the "in my list" filter, with its "+N more" control when it hides some.

## 3. Watch-order numbers removed

- [x] 3.1 Remove the `.series-timeline__card-header` block and its `.series-timeline__card-number` span from `TimelineCard` in `SeriesTimeline.tsx`, and drop the now-unused `number` prop from `TimelineCard`'s props and its call site.
- [x] 3.2 Remove `.series-timeline__card-header` and `.series-timeline__card-number` from `SeriesTimeline.css`, including the comment that explains the number's placement relative to the picker.
- [x] 3.3 Rewrite the slot picker's `aria-label`, which currently reads `Choose version for watch order position ${number}` — name the route by its alternatives instead of by a number that no longer exists.
- [x] 3.4 Remove the unused `rank` prop and its `#{rank}` span from `SeriesEntryRow.tsx`, and `.series-entry-row__rank` from `SeriesEntryRow.css` (design decision 9). Confirm by search that nothing passes `rank`.

## 4. The route picker moves above the route's first card

- [x] 4.1 Add a `resolvedPick: Record<number, number>` prop to `SeriesTimeline` and pass `resolvedPick` from `SeriesPage`'s existing `resolveSeriesPick` result.
- [x] 4.2 In `SeriesTimeline`, compute the picker-owning column per slot: the first entry of `entries` (already in watch order) whose `branchHeadAnimeId === resolvedPick[slot.slotKey]` — the alternative itself included, since `SeriesVersionSlots` puts an alternative in its own branch (design decision 8). Build a `Map<animeId, SlotPicker>` from it.
- [x] 4.3 Wrap each card in a `.series-timeline__col`. When `slots.length > 0`, render a `.series-timeline__picker-rail` above the card in **every** column — carrying the picker in the owning column and empty elsewhere. Render no rail at all when the series has no slot.
- [x] 4.4 Remove the picker from inside `TimelineCard` and drop its `picker` prop.
- [x] 4.5 In `SeriesTimeline.css`, add `.series-timeline__col` (column flex) and `.series-timeline__picker-rail` with the `width: 0; min-width: 100%` idiom so a rail never widens its column past the card, and a fixed height so every card's top edge stays aligned. Move `flex: 0 0 auto` handling to the column if the card no longer needs it.
- [x] 4.6 In `SeriesPage.css`, change `.series-page__slot-picker` from `flex-wrap: wrap` to `nowrap`, give `.series-page__slot-picker-button` `flex: 1 1 0; min-width: 0`, and re-tune the padding now that the strip sits above the row rather than inside a card.
- [x] 4.7 Check both shapes in the app: Demon Slayer (buttons above the Mugen Ressha card, which is where its route starts) and Fate/stay night (buttons above the Unlimited Blade Works *prologue*, the route's first card), and confirm switching a route moves the buttons to the newly picked route's first card.
- [x] 4.8 Confirm the row still scrolls horizontally without a visible scrollbar, that landscape cards (`--card-w: 384px`) get a full-width rail, and that a series with no slot reserves no space above its row.

## 5. Client-side stats derivation

- [x] 5.1 Create `frontend/src/utils/seriesStats.ts`. Move `effectiveWatchedEpisodes` there from `SeriesPage.tsx` and add `episodeSeconds(entry)` (mirrors `WatchMath.EpisodeSeconds`, `averageEpisodeDurationSeconds ?? 24 × 60`) and `rewatchEpisodesIncludingCurrentRun(entry)` (mirrors `WatchMath.RewatchEpisodesIncludingCurrentRun`, published-total baseline and all). Comment each with the backend method it mirrors.
- [x] 5.2 Add `realExtras(extras)` returning `extras.filter(e => !e.isRelatedEntry)` — the member basis every server figure uses (design decision 6).
- [x] 5.3 Add `deriveSeriesStats(base, mainLineVisible, realExtras)` returning `base` with `myWatchedEpisodes`, `myWatchedSeconds`, `myRewatchedSeconds`, `entriesCompleted`, `extrasCompleted` and `mainLineCompletedByMe` replaced by client computations, per the table in design decision 5. Every other field passes through untouched.
- [x] 5.4 In `SeriesPage.tsx`, apply it: `const stats = deriveSeriesStats(statsForPick(series, resolvedPick), mainLineVisible, extras)`. Update `statsForPick`'s comment, which currently explains why the default pick reads `series.stats` for freshness — that reason is now handled by the derivation.

## 6. Tie lists follow the edit, not the snapshot

- [x] 6.1 Read `highestMalScoreAnimeIds`, `myHighestScoreAnimeIds` and `mostRewatchedAnimeIds` from `series.stats` rather than from the pick-resolved `stats`, since `BuildStats` computes all three over the whole unfiltered main line and every `statsByPick` entry carries identical values (design decision 4). Comment why.
- [x] 6.2 Pass `series.stats.myHighestScoreAnimeIds` — not `stats.myHighestScoreAnimeIds` — as `handleReorderFavourite`'s `currentOrder`, so a reorder made on a non-default route reads and writes the same list.
- [x] 6.3 Add a `mostRewatchedAnimeIds` recomputation to `patchSeriesEntry`, alongside the existing `myHighestScoreAnimeIds` one: a rewatch count is editable, so the stat goes stale without it. Tie order is watch order (main line then real extras), matching `BuildStats`' `TiedTopIds` with no tie-break key.
- [x] 6.4 Fix the member basis of the existing in-place recomputations: `recomputeScores` and `recomputeMyHighestIds` currently take `series.extras` whole, including related entries the server excludes from every average and stat. Pass the real extras only (design decision 6).
- [x] 6.5 Verify on first paint, before any edit, that the derived and re-scoped figures equal what the server sent — a difference means the client mirror or the member basis is wrong.

## 7. Time watched and time left become one stat

- [x] 7.1 Replace the two `Time watched` / `Time left` stat cells in `SeriesPage.tsx` with one `Time` cell whose `<dd>` holds a `Watched:` row and a `Left:` row, reusing the `series-page__entries-completed` two-row markup pattern rather than inventing a second idiom (design decision 7).
- [x] 7.2 Keep `showTimeWatched` and `showTimeLeft` exactly as they are; render each row on its own flag, and render the cell only when at least one of them is true.
- [x] 7.3 Add the CSS for the new cell's rows and labels in `SeriesPage.css`, following the existing entries-completed rules.

## 8. Verify

- [x] 8.1 `nvm use 22 && npm run build` and `npm run lint` in `frontend/` clean (default node is v16 and will not build Vite).
- [x] 8.2 Open a series with many extras: every group collapsed with its count, "in my list" reading as on, all-groups control reading "Expand all". Select a type and confirm the groups holding it open and render their tiles.
- [x] 8.3 Confirm the "in my list" control now only filters — pressing it in either direction leaves every group's collapsed state alone.
- [x] 8.4 Edit a member's score, status and rewatch count from a timeline card and from a More tile, on the default route and on a non-default route, and confirm my favourite, most rewatched, entries completed, the Time stat, the progress bar and the header badge all move without a reload. Switch routes away and back and confirm nothing reverts.
- [x] 8.5 Reorder tied favourites on a non-default route and confirm the order shows immediately and survives a reload.
- [x] 8.6 Check the Time stat in each state: part-watched (both rows), finished (watched row alone), never started (left row alone), unknown runtime (both rows).
- [x] 8.7 Confirm back/forward restores the section as left, and that a fresh visit (reload) opens with every group collapsed and every slot on its default alternative.
