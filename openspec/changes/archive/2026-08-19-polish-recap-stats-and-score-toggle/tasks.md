## 1. Uniform ranking row height (frontend)

- [x] 1.1 In `components/RankingSection.css`, introduce a `--ranking-poster-h: 40px` token on `.recap-ranking-list` (or the nearest shared ancestor of the rows) and have `.recap-ranking-row__poster`'s `height` read from it instead of its literal `40px` (design decision 1).
- [x] 1.2 Add `min-height: var(--ranking-poster-h)` to `.recap-ranking-row__link`, relying on its content-box sizing so the row resolves to the same outer height a poster row already has — no literal total, no `box-sizing` change.
- [x] 1.3 Mirror both edits in `components/RankingOverlay.css` for `.ranking-overlay__link` and `.ranking-overlay__posters img, .ranking-overlay__poster-placeholder`, which carry the same `8px 10px` padding and `28×40` posters.
- [x] 1.4 Verify on a yearly recap under **What aired** that the season ranking and "Seasons by time watched" rows measure the same height side by side, and that no illustrated row changed size (compare against the profile page's Favourite seasons before/after).
- [x] 1.5 Verify the years-by-time-watched overlay, whose leading row alone carries posters, renders every row at one height.

## 2. Currently-watching stat (backend)

- [x] 2.1 Add `CurrentlyWatching` (int) to `RecapStatsDto` in `Services/Recap/RecapDto.cs`, positioned after `Dropped` so the record's field order mirrors the tile order.
- [x] 2.2 Change `RecapStatsBuilder.Build` to take the period's aired-attributed entries as a third argument, and count `CurrentlyWatching` as those entries with `Status == WatchStatus.Watching` — never from `included` (design decision 2). Comment the air-date scoping and why a filter-scoped count would always be zero under **What I watched**.
- [x] 2.3 In `RecapService.GetRecapAsync`, keep the `RecapEntrySelector.Select(wholeList, period, RecapTimeFilter.Aired)` list it currently reduces to `airedCount` and pass it into `RecapStatsBuilder.Build`, taking `airedCount` from the same list.
- [x] 2.4 Add `RecapStatsBuilderTests` cases: a Watching entry aired inside the period is counted under **What aired**; the same entry is still counted under **What I watched** (spec scenario "Currently watching survives the watch-history filter"); a Watching entry aired outside the period is not counted; a Watching entry whose anime has no `AiredFrom` is counted nowhere; and Completed/Dropped/On-hold entries do not inflate the count.
- [x] 2.5 Add a `RecapServiceTests` case asserting a multi-year recap's currently-watching count spans every year of the range.

## 3. Currently-watching tile (frontend)

- [x] 3.1 Add `currentlyWatching: number` to `RecapStatsDto` in `api/types.ts`, in the same position as the backend record's field.
- [x] 3.2 Insert `{ label: 'Currently watching', value: String(stats.currentlyWatching) }` into `RecapPage.renderStats`'s `tiles` array directly after **Dropped**, so the three status counts sit together (spec: "The three status counts … SHALL be presented together").
- [x] 3.3 Check the tile in all three modes and under both time filters against the two-column `.recap-page__stats` grid — eight tiles must still fill the grid evenly without the label wrapping mid-word at the narrow column width.

## 4. Shared score-distribution component (frontend)

- [x] 4.1 Create `components/ScoreDistribution.tsx` + `ScoreDistribution.css`, taking `buckets: ScoreDistributionBucketDto[]`, `meanScore?: number | null`, and a `compact?: boolean` size modifier (design decision 5). It renders the buckets highest score first, the bar proportional to the largest count, and the count and share as two cells.
- [x] 4.2 Move `formatShare` out of `ProfilePage.tsx` into the new component unchanged, keeping its `<1%` behaviour and its null return when nothing is rated.
- [x] 4.3 Split the row's trailing cell into `.score-distribution__count` and `.score-distribution__share`, each fixed-width, right-aligned, `font-variant-numeric: tabular-nums`; drop the parentheses around the share. Carry the existing comment explaining why these columns are fixed rather than content-sized, extended to cover both.
- [x] 4.4 Move the `.score-distribution*` rules out of `ProfilePage.css` into `ScoreDistribution.css` verbatim apart from 4.3's changes, and add the `compact` modifier (smaller type, shorter bar track, tighter row gap) used only by the recap.
- [x] 4.5 Replace the distribution markup in `ProfilePage.tsx` with `<ScoreDistribution buckets={profile.scoreDistribution.buckets} meanScore={profile.scoreDistribution.meanScore} />`, keeping it inside its existing `profile-box` with its "Rating distribution" heading.
- [x] 4.6 Verify on the profile page that counts align down one column and percentages down another, that every bar track is the same width regardless of the values beside it, and that the mean line is unchanged.

## 5. Recap rating distribution (frontend)

- [x] 5.1 In `RecapPage.tsx`, derive the period's buckets from `recap.items` — ten zero-filled buckets for scores 1–10 counted from non-null `myScore`, matching `ProfileService.BuildScoreDistribution`'s shape — computed over the full included set, **not** the media-type-narrowed `narrowed` array (design decisions 3 and 4). Memoise on `recap`.
- [x] 5.2 Render it as a `recap-page__section` headed "Rating distribution", using `<ScoreDistribution buckets={…} compact />` with no `meanScore` prop, so the recap carries no mean line of its own (design decision 4).
- [x] 5.3 Wrap the stat block and the distribution in a single column element inside `.recap-page__lead`'s right-hand cell, so the lead grid keeps exactly two children and the 1024px collapse still stacks top-10 → stats → distribution in that order.
- [x] 5.4 Verify the distribution recomputes when the time filter changes, does not change when the top-10 media-type select changes, and renders all-empty tracks with no invalid share for a period whose anime are all unscored.
- [x] 5.5 Verify the compact block reads cleanly at the lead column's `minmax(14rem, 20rem)` width and below the 1024px breakpoint.

## 6. Extended ranking tie-break (backend)

- [x] 6.1 In `RecapRankingBuilder`, add a private helper building a group's score histogram — counts indexed 1–10 from its scored entries' `MyScore` — and a comparison implementing design decision 6's four steps in order: full-precision weighted score descending, scored count descending, histogram compared from 10 down to 1, then recency descending.
- [x] 6.2 Order on the unrounded `WeightedAverage` result and keep `Math.Round(…, 2)` exactly where it is today — applied when projecting the DTO, after ordering. Compare the doubles with `==` and add **no** epsilon; comment why that is sound here (`R` and `C` are exact integer-sum averages, so equal inputs give bit-identical results) and why an epsilon would wrongly let a tie-break overrule a real difference in score.
- [x] 6.3 Rewrite `BuildSeasonRanking`'s `OrderByDescending(...).ThenByDescending(...).ThenBy(SeasonCalendar.GetSeasonPointIndex(...))` chain to use the shared comparison, with the season point index as the recency key — descending, reversing today's oldest-first fallback.
- [x] 6.4 Do the same for `BuildYearRanking` with the year as the recency key.
- [x] 6.5 Leave `BuildSeasonTimeRanking` and `BuildYearTimeRanking` exactly as they are (design non-goal), and note in a comment beside them that the score rankings' extended tie-break deliberately does not apply to them.
- [x] 6.6 Update `RecapRankingBuilderTests`: fix any assertion that depends on the oldest-first fallback, then add cases for exactly equal scores with unequal scored counts, equal score and count separated at 10, equal score and count separated further down at 8, and fully identical histograms falling back to the newer group — at both the season and the year level. Construct the equal-score fixtures from equal counts and equal score sums so the tie is exact rather than approximate.
- [x] 6.7 Add a `RecapRankingBuilderTests` case pinning full-precision ordering: two groups whose weighted scores differ only past the second decimal — both displaying the same rounded value — are ordered by that difference, with the lower-scoring group left behind even when it has the larger scored count.
- [x] 6.8 Update `ProfileServiceFavouriteSeasonsAndYearsTests` for the reversed fallback and add one case asserting the profile's favourites order identically to a recap covering the same groups (spec scenario "Equal scores resolved by coverage then by score").

## 7. Score-visibility switch (frontend)

- [x] 7.1 Replace `Navbar.tsx`'s `.navbar__toggle` button with a `role="switch"` button whose `aria-checked` tracks scores **shown** (`!hidden`) and whose `aria-label` stays the action it performs — "Show scores" when hidden, "Hide scores" when shown (design decision 7).
- [x] 7.2 Give it a track containing a fixed `Scores` label plus an absolutely positioned knob, both `aria-hidden`, so the accessible name comes from the label alone and the control's width never changes with state.
- [x] 7.3 Add an `EyeIcon` component beside the existing `GearIcon`: an eye outline with a pupil plus a slash path animated by `stroke-dasharray`/`stroke-dashoffset`, driven by a class on the switch rather than by swapping `d` values.
- [x] 7.4 In `Navbar.css`, style `.navbar__score-switch` — pill track at `var(--control-h)`, accent fill and knob-right when checked, neutral and knob-left when not — and animate the knob with a `transform: translateX()` transition plus the slash's dash offset and the pupil's scale.
- [x] 7.5 Give the track the app's navigation-control hover treatment (`var(--accent-bg)` + `var(--accent-border)`) and a `:focus-visible` ring, satisfying `navigation-and-search`'s hover requirement for this control.
- [x] 7.6 Add a `@media (prefers-reduced-motion: reduce)` block dropping the slide, the slash draw, and the pupil scale to instant state changes.
- [x] 7.7 Delete the now-unused `.navbar__toggle` rules.
- [x] 7.8 Verify: the navbar does not reflow on toggle; the switch is reachable and operable by keyboard with a visible focus ring; the icon alone identifies the state; a reload restores both the hidden state and the matching knob position; and the control still sits between the search field and Profile at both wide and wrapped widths.

## 8. Build and verify

- [x] 8.1 Compile-check the backend against the .NET 10 SDK image (`rsync` the `backend/` tree to `/private/tmp/bm-build`, then `docker run --rm -v /private/tmp/bm-build:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 bash -c "dotnet build AnimeTracker.Api/AnimeTracker.Api.csproj -c Release"`) — the local SDK is 9.0 and Docker cannot bind-mount `~/Documents`.
- [x] 8.2 Run the backend test suite in the same image and confirm the Recap and Profile suites pass, including every case added in groups 2 and 6.
- [x] 8.3 Build the frontend with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` from `frontend/` (the default node is v16 and fails under Vite), and run `npm run lint`.
- [x] 8.4 Walk the running app once end to end: a season recap, a yearly recap, and a multi-year recap — stats with the new tile, the distribution below them, matched ranking row heights, and the switch toggling scores across pages.
