## 1. Shared rewatch math

- [x] 1.1 Add `RewatchEpisodesIncludingCurrentRun(int rewatchCount, int? totalEpisodes, int episodesWatched, WatchStatus? status)` to `Services/Watching/WatchMath.cs` — `RewatchOnlyEpisodes(...) + (status == Rewatching ? episodesWatched : 0)` (design D1). Document why the two terms cannot double-count: entering Rewatching resets episodes-watched, and the rewatch count only rises when a run finishes.
- [x] 1.2 Document the known fallback gap in the same doc comment: for an entry with **no published total**, `RewatchOnlyEpisodes` uses episodes-watched as the per-run baseline, which for an in-progress rewatch is a partial run. Both the old and new figures are lower bounds; not fixed here.
- [x] 1.3 Add `WatchMathTests` cases: completed runs only; completed runs plus an in-progress run; a first rewatch in progress with `RewatchCount == 0`; a non-Rewatching status ignoring episodes-watched; no published total.

## 2. Profile — rewatch time includes the run in progress

- [x] 2.1 Rewrite `SeriesRankingIndex.MemberRewatchSeconds` to call 1.1 and drop the `RewatchCount <= 0` early return, returning 0 only when the computed episode figure is 0 — so a member never in my list and a member never rewatched both still contribute nothing.
- [x] 2.2 Confirm `SeriesRankingMemberProjection.EntryStatus` is already carried through `SeriesRankingLookup` (it is) and pass it to 1.1; no projection change expected.
- [x] 2.3 Leave `BuildRewatchedSection` (the per-media-type scopes) untouched — those rank by the integer `RewatchCount`, per the spec's closing paragraph and the proposal's non-goals.
- [x] 2.4 Extend `ProfileServiceRewatchedSeriesTests` with: an in-progress rewatch adding to a series' total; a first rewatch in progress making a series eligible where it previously had no entry at all; a Completed member with zero rewatches still contributing nothing; the All scope's badge unchanged by an in-progress rewatch.

## 3. Series figures — a Rewatching entry counts as fully watched

- [x] 3.1 Add one shared backend helper for **effective watched episodes** — `max(episodesWatched, airedEpisodes)` when the entry's status is `Rewatching`, else `episodesWatched`, falling back to `episodesWatched` when `airedEpisodes` is null (design D2). Put it where both `SeriesRankingIndex` and `SeriesService` can reach it.
- [x] 3.2 `SeriesRankingIndex.ProgressBadge`: use 3.1 for every watched sum, and widen rule (1) to accept `WatchStatus.Rewatching` alongside `Completed`. Leave the aired sums and the unknown-broadcast-count guard alone.
- [x] 3.3 `SeriesRankingIndex`: use 3.1 for `SeriesListItemDto.mainLineWatchedEpisodes`, which the browser's My-progress sort divides.
- [x] 3.4 `SeriesService`: use 3.1 for the series DTO's main-line watched-episode figure and `myWatchedSeconds`; count a `Rewatching` entry as completed in the entries-completed stat. Leave the episode total, the aired figure, the runtime, and the most-rewatched stat unchanged.
- [x] 3.5 Mirror 3.2 in `frontend/src/pages/SeriesPage.tsx`'s `completionBadge` — the same effective-watched rule and the same rule (1) widening. `types.ts` says the pair must change together; keep the comment accurate.
- [x] 3.6 Mirror 3.4 in `SeriesPage.tsx`'s header progress bar, its named watched figure, and time watched / time left, so the badge and the bar beside it read the same entry the same way.
- [x] 3.7 Tests: extend `SeriesListProgressBadgeTests` (rewatch does not read as behind; rewatch does not read as unwatched; rewatch after a drop counts as watching past it; a finished franchise being rewatched keeps `Completed`) and `SeriesServiceBuildStatsTests` (progress bar stays full; entries-completed counts a rewatching entry; episode total unmoved).

## 4. Series composition — `pv` extras and the `other`-edge probe

- [x] 4.1 `Services/Series/SeriesRelations.cs`: replace `IsTraversableMusicEdge` with `IsTraversableOtherEdge(string? oneEnd, string? otherEnd)` over a named `CompanionMediaTypes` set — a concrete `HashSet<string> { "music", "pv" }`, for the EF-translation reason `TraversalSet` already documents. Keep the exactly-one-end rule. Note in the doc comment that `cm` is deliberately excluded.
- [x] 4.2 `SeriesGraphBuilder`: swap the outgoing `other`-edge filter onto 4.1, and replace the inline `(r.Anime.MediaType == "music") != (metadata.MediaType == "music")` in the **incoming**-edge LINQ query with `CompanionMediaTypes.Contains(r.Anime.MediaType) != CompanionMediaTypes.Contains(metadata.MediaType)`. Verify it translates rather than falling back to client evaluation.
- [x] 4.3 `SeriesGraphBuilder`: add the probe pass (design D5c). Budget 4 on a visit build, 10 on a rebuild, tracked separately from `fetchBudget`/`fetchesUsed`. For each `other` far end with **no cached row**, spend one probe: `RefreshOneAsync`, reload, then decide the edge from the now-cached media type; admit it as a member when it lands in the companion set. Treat `AnimeMetadataNotFoundException` as the member path does — skip, do not mark partial.
- [x] 4.4 Mark the series **partial** when the probe budget is exhausted with unprobed `other` far ends remaining, so the existing "partial rebuilds on next visit" mechanism finishes the job. Confirm probes cannot repeat (the probe caches the row), so this converges.
- [x] 4.5 Verify probes never draw on the member fetch budget and member fetches never draw on the probe budget.
- [x] 4.6 Add `"pv"` to the main-line ineligibility check alongside `special`/`music` in `SeriesGraphBuilder`'s classification.
- [x] 4.7 `Services/Series/SeriesMediaTypeOrder.cs`: insert `pv` between `music` and `tv` — Movie 0, OVA 1, ONA 2, Special 3, Music 4, PV 5, TV 6, Other 7. Update the class doc comment's stated order.
- [x] 4.8 Bump `ClassificationRevisedAt` so every stored series rebuilds on its next read and picks up `pv` members and the new group order.
- [x] 4.9 Confirm no frontend work is needed for the label: `mediaTypeLabel` already maps `pv` → `PV`.
- [x] 4.10 Tests in `SeriesGraphBuilderTests`: a `pv` far end joins the series; two `pv` entries do not chain; a `pv`↔`music` edge is not traversed; a commercial is still excluded; an uncached far end is probed once and the verdict is cached; a probed commercial costs nothing on the next build; the probe budget marks the series partial; `pv` is never main line. Add a `SeriesServiceClassificationRebuildTests` case for the revision bump.

## 5. My list — multi-select status filter

- [x] 5.1 `MyListPage.tsx`: change `StatusFilter` to `WatchStatus[]`, with `[]` meaning All (design D6), and rename the restorable-state key from `statusFilter` to `statusFilters` so a snapshot written before this change cannot feed a string into array code.
- [x] 5.2 Change `focusSeed` to return a one-element array (or `[]`), keeping every existing deep link landing on exactly the status it names today.
- [x] 5.3 Selection handlers: a status tab toggles its own membership; the All tab sets `[]`; deselecting the last selected status yields `[]` rather than an empty list.
- [x] 5.4 Update the derivation: the status predicate becomes `statusFilters.length === 0 || statusFilters.includes(item.entry.status)`, and the grouped branch's `GROUP_ORDER.filter(...)` becomes the same test — preserving the app's standard group order, not click order. Update the `useMemo` dependency list.
- [x] 5.5 Replace the tab markup's `role="tablist"` / `role="tab"` / `aria-selected` with plain toggle buttons carrying `aria-pressed`, including the All tab; update the container's accessible label. Keep the per-status colour classes and the active-state styling, driven by membership rather than equality.
- [x] 5.6 Check every other reader of `statusFilter` in the file (the empty-state copy, the results count, the recap scope chip) still reads correctly with an array, and that the "Recap a period" button — a sibling in the same row, not a filter — is untouched by the markup change.

## 6. Series page — More-section state and scroll

- [x] 6.1 `SeriesPage.tsx`: move `mineOnly`, `collapsedGroups`, and `unfilteredGroups` from `useState` to `useRestorableState` under keys `moreMineOnly`, `moreCollapsedGroups`, `moreUnfilteredGroups`. Replace the "reset on navigation rather than persisting" comment with the reason they now restore.
- [x] 6.2 Confirm the `Set` and `Record` values survive: `pageStateStore` holds live references and never serialises. Confirm keying by `location.key` already prevents two series' pages sharing state.
- [x] 6.3 Confirm a restored group key that matches no rendered group is simply never read (no error, no orphaned UI), and that a fresh visit still seeds filter-on / nothing-collapsed / nothing-exempted.
- [x] 6.4 Add scroll-on-open (design D4): when `handleExtrasGroupHeadingClick` **opens** a group, record which group, and in a layout effect keyed on that group scroll the window to that heading's `getBoundingClientRect().top + window.scrollY`. Clear the pending target after scrolling.
- [x] 6.5 Use `window.scrollTo` rather than `scrollIntoView`, and let the browser clamp when the target exceeds the maximum scroll offset — that clamp *is* the "get as far down as possible" behaviour. No offset: nothing in the app is sticky or fixed at page level.
- [x] 6.6 Do not scroll on collapse, and do not scroll from `toggleMineOnly` or `toggleAllExtrasGroups` — those act on every group, so there is no group to scroll to.

## 7. Airing page — week navigation replaces history

- [x] 7.1 `AiringPage.tsx`: pass `{ replace: true, state: { keepScroll: true } }` to `setSearchParams` in `goToWeek`, so every week change — buttons and both jump selectors — replaces the current history entry.
- [x] 7.2 Confirm `keepScroll` is doing real work here: a replace mints a fresh `location.key`, which `useScrollRestoration` would otherwise treat as a fresh visit and answer with `scrollTo(0, 0)`. The flag also seeds the new entry's snapshot with the current scroll position.
- [x] 7.3 Verify by hand: navbar → Airing → several weeks forward → one Back leaves the Airing page; the URL still names the displayed week; opening that URL directly shows that week; opening an anime from a slot and going back still restores the same week.

## 8. Verification

- [x] 8.1 Build the backend via the `sdk:10.0` Docker image (the local SDK is 9.0) and run the full test suite.
- [x] 8.2 Build the frontend with nvm's Node v22 (the default v16 cannot run Vite).
- [x] 8.3 Open a real series with `pv` extras (Jujutsu Kaisen) and confirm the PV group appears with its count, that repeat visits spend no further probes, and that the series stops being partial.
- [x] 8.4 Check the profile's "Most rewatched" Series scope against an entry currently marked Rewatching, and confirm the series page badge for that entry's franchise no longer reads `N behind`.
- [x] 8.5 Run `openspec validate polish-rewatch-more-and-filters --strict`.
