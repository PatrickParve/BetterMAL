## 1. Currently watching arrows (design D1)

- [x] 1.1 In `CurrentlyWatchingCarousel.tsx`, replace the `overflowing` state with a bounds state holding `overflowing`, `atStart` and `atEnd`, all computed in one `updateBounds()` from the track's `scrollLeft`, `scrollWidth` and `clientWidth`; keep the existing `+1` tolerance for overflow and use `scrollLeft <= 1` / `scrollLeft >= scrollWidth - clientWidth - 1` for the two ends.
- [x] 1.2 Call `updateBounds()` from the existing `ResizeObserver`, from a new `scroll` listener on the track, and on the existing `items` dependency; remove both listeners in the effect's cleanup alongside the observer.
- [x] 1.3 Pass `disabled={atStart}` to the left arrow and `disabled={atEnd}` to the right arrow, leaving the `overflowing &&` render condition on both so the pair still appears and disappears together.
- [x] 1.4 In `CurrentlyWatchingCarousel.css`, change `.carousel__arrow:hover` to `.carousel__arrow:enabled:hover`, and add a `:disabled` rule giving `cursor: default` and reduced opacity so a disabled arrow is distinguishable at rest.
- [x] 1.5 Verify in the app: a row of 6+ cards opens with the left arrow disabled; clicking right to the end disables the right arrow and enables the left; a trackpad scroll to either end does the same; a disabled arrow shows no hover treatment; a row of 5 or fewer shows no arrows at all.

## 2. Series page — the "in my list" control (design D2)

- [x] 2.1 In `SeriesPage.tsx`, derive `anyExtraIsMine` from `series.extras.some((e) => e.entry != null)`, placed beside `myMediaTypes` which already walks the same array.
- [x] 2.2 Derive the filter's effective state as `mineOnly && anyExtraIsMine` and use that derived value everywhere `mineOnly` currently decides what a group shows — `extrasGroupView`'s `visibleItems`/`showsAll`, `filterActive`, and the media-type buttons' `unavailable` test — so a restored `mineOnly` cannot narrow a section whose control is absent.
- [x] 2.3 Wrap the "In my list" button in `anyExtraIsMine &&` so it is not rendered at all when nothing in More is mine; leave the expand/collapse-all control, the media-type filter row and the group rendering untouched.
- [x] 2.4 Verify in the app: a series with no extras of mine shows no "In my list" button while its groups still expand, collapse and show every tile; a series with exactly one extra of mine shows the button and it filters as before.

## 3. Series stats — Studios and Genres on the visible main line (design D3)

- [x] 3.1 In `SeriesService.BuildStats`, compute `studios` and `genres` from `visibleMainLineAnime` instead of `allAnime`.
- [x] 3.2 Remove the now-unused `allAnime` parameter from `BuildStats`' signature and drop the argument at both call sites (`SeriesService.cs:173` and `:177`).
- [x] 3.3 Correct the `SeriesStatsDto` doc comment sentence that says the "highest-scored/most-rewatched/favourite/studios/genres figures span every member" — the first three still do, studios and genres now follow the picked main line.
- [x] 3.4 Update `SeriesServiceBuildStatsTests.cs` for the changed signature, and add cases covering: an extra's studio and an extra's genre excluded from the lists, and the lists moving with a version-slot pick.

## 4. Latest updates — 30-day window with a 20-row floor (design D4)

- [x] 4.1 Add `GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct)` to `IActivityLogRepository` and implement it in `ActivityLogRepository` with the same `Include(l => l.Anime)` and `Timestamp desc, Id desc` ordering the other reads use, bounded by `Take(maxRows)`; document in the interface why the ordering is not optional (the completion/score merge depends on it).
- [x] 4.2 In `ProfileService`, give `BuildActivityFeed` a `cutoff` instant and a `floor` count, and change its termination test to stop once the log under consideration is older than `cutoff` **and** the feed already holds `floor` items. Leave the merge, the non-increase filter, the field-group selection and the per-(anime, group) collapse exactly as they are.
- [x] 4.3 Replace `RecentActivityCount` with a `RecentActivityFloor = 20` constant, and add `RecentActivityWindowDays = 30` and a `RecentActivityMaxRows = 5000` safety cap; comment the cap as a safety valve rather than a product rule.
- [x] 4.4 In `GetProfileAsync`, read the window with `GetSinceAsync(DateTimeOffset.UtcNow.AddDays(-RecentActivityWindowDays), RecentActivityMaxRows, ct)` and build the feed from it; when the result holds fewer than `RecentActivityFloor` items, re-read with the existing `GetRecentAsync(RecentActivityFetchWindow, ct)` and rebuild from that larger window with the same cutoff and floor.
- [x] 4.5 Extend `ProfileServiceActivityFeedTests.cs`: a busy 30 days yields every collapsed row rather than 20; a 30-day window holding 3 rows tops up to 20 from older logs in one unbroken most-recent-first order; a log shorter than 20 rows returns what it has; the collapsing rules behave identically inside and outside the window. Use the fake repository's existing shape, extending it with the new method.
- [x] 4.6 Confirm no frontend change is needed — `ProfilePage.tsx` maps `profile.recentActivity` whole with no slice — then verify in the app that the box still shows five whole rows at rest and scrolls through the longer feed.

## 5. Remove the `Upcoming` status (design D5)

- [x] 5.1 In `SeriesStatusRules.Compute`, delete the `Upcoming` branch and the now-unused `anyFinished` local, leaving `anyUpcoming ? "Ongoing" : "Finished"` as the tail; update the class doc comment's "four-step precedence" wording.
- [x] 5.2 In `SeriesServiceComputeStatusTests.cs`, change `NothingAiredYetReadsUpcoming` to assert `Ongoing` and rename it accordingly; check `MainLineAiringWithNothingFinishedReadsAiringNotUpcoming` still reads correctly and rename it if its name no longer describes the distinction it tests.
- [x] 5.3 In `frontend/src/api/types.ts`, drop `'Upcoming'` from the `SeriesStatus` union.
- [x] 5.4 In `SeriesStatusPill.tsx`, drop the `Upcoming` entry from `SERIES_STATUS_CLASS`; in `SeriesStatusPill.css`, delete the `.series-status-pill--upcoming` rule and update the `min-width` comment that names "Upcoming" as its widest label.
- [x] 5.5 In `utils/anime.ts`, drop `'upcoming'` from `SeriesStatusFilterValue`, from `SERIES_STATUS_FILTER_OPTIONS` and from `STATUS_FILTER_VALUES`, and renumber `SERIES_STATUS_ORDER` to `{ Airing: 0, Ongoing: 1, Finished: 2 }`.
- [x] 5.6 Confirm `SeriesBrowserPage.tsx` needs no edit of its own — it renders `SERIES_STATUS_FILTER_OPTIONS` and parses the URL through `parseListParam`, which already drops an unknown value — and verify that a URL carrying `?status=upcoming` loads as an unfiltered list rather than an error or an empty one.
- [x] 5.7 Verify in the app: the Series page's Status filter row shows three buttons; sorting by Status orders airing, then ongoing, then finished; no pill anywhere renders without a variant class.

## 6. Checks

- [x] 6.1 Run `dotnet build` and `dotnet test` from `backend/`; fix anything the removed `BuildStats` parameter or the new repository method broke in call sites or fakes.
- [x] 6.2 Run the frontend typecheck and lint (`npm run lint`, `tsc`) under nvm's node v22; the `SeriesStatus` union change surfaces any missed consumer as a type error.
- [x] 6.3 Re-read `CODE_GUIDE.md` for statements this change falsifies — the series status values and the profile activity feed's bounds are the likely ones — and update them.
- [x] 6.4 Run `openspec validate trim-dead-controls-and-widen-updates` and confirm it still passes.
