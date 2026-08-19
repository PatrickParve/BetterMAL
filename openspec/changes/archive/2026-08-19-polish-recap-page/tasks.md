## 1. Share the opinion gate (backend)

- [x] 1.1 Move `OpinionDivergenceThresholdSd`, `OpinionDivergenceMyDislikeCeiling`, `OpinionDivergenceMyLikeFloor`, and `OpinionDivergenceMalLikeFloorAndDislikeCeiling` out of `ProfileService` into `Services/Profile/ScoreDivergence.cs` as public constants, carrying their existing explanatory comments across verbatim (design decision 1).
- [x] 1.2 Add `ScoreDivergence.IsTheyLikedItIDidnt(entry, divergence)`, `IsILikedItTheyDidnt(entry, divergence)`, and `IsOpinionDivergent(entry, divergence)` (the union of the two), each expressing exactly the comparisons `ProfileService.BuildOpinionDivergence` uses today.
- [x] 1.3 Rewrite `ProfileService.BuildOpinionDivergence`'s two `.Where(...)` clauses to call the new predicates instead of restating the conditions inline; leave its ordering and projection untouched.
- [x] 1.4 Run `ProfileServiceOpinionDivergenceTests` unchanged — it must pass without edits, proving the move was mechanical (design risk 3).

## 2. Gate the recap's hot takes (backend)

- [x] 2.1 In `RecapStatsBuilder.BuildHotTakes`, filter the both-scored candidates through `ScoreDivergence.IsOpinionDivergent` before the existing `OrderByDescending(Math.Abs(divergence))`, leaving the ordering, the title tie-break, and the cap of five as they are (design decision 2).
- [x] 2.2 Update the `D4: no label gates, no SD threshold` comment above the method to record the new rule and why the two surfaces now share it.
- [x] 2.3 Update `RecapStatsBuilderTests`: rewrite `HotTakesCapAtFiveOrderedByAbsoluteDivergence` and the degrade-to-four/two/one cases so their fixtures clear the gate, and confirm `HotTakesAreEmptyWhenNoneEligible` still holds.
- [x] 2.4 Add `RecapStatsBuilderTests` cases for the new exclusions: an entry above the SD threshold but inside the neutral 6–7 score band, an entry I scored 9 that MAL also scored above 7.5, and an entry whose divergence falls just short of one SD.
- [x] 2.5 Add a `RecapStatsBuilderTests` case asserting that an anime the profile page names in a divergence list is eligible as a hot take when it falls inside the period (spec scenario "Agreement with the profile page").

## 3. Posters on every ranked season and year (backend)

- [x] 3.1 Drop the `index == 0 ? … : []` guard in `RecapRankingBuilder.BuildSeasonRanking` and `BuildYearRanking` so `TopPosters(c.Scored)` runs for every row; leave `BuildSeasonTimeRanking` / `BuildYearTimeRanking` guarded (design decision 3).
- [x] 3.2 Correct the now-false doc comments: `TopPosters` is empty for every row but the leader on `RecapSeasonRankingDto` and `RecapYearRankingDto`, and the summary on `RecapRankingPosterDto` in `RecapDto.cs`. Leave `RecapTimeRankingDto`'s version, which stays true.
- [x] 3.3 Update `RecapRankingBuilderTests`' leader-only-poster assertions to assert posters on every ranked row for the score rankings, and add a case holding the time rankings to leader-only.
- [x] 3.4 Add a `ProfileServiceFavouriteSeasonsAndYearsTests` case asserting every ranked favourite row carries posters, so the profile surface is covered by its own test rather than only by inheritance.
- [x] 3.5 Correct the matching `topPosters is empty for every row but the leader` comment in `frontend/src/api/types.ts`.

## 4. Shared season arithmetic (frontend)

- [x] 4.1 Move `shiftSeason` and `seasonPointIndex` from `pages/SeasonPage.tsx` into `utils/anime.ts` verbatim, typed against the season-name union already used there, and import them back into `SeasonPage` (design decision 7).
- [x] 4.2 Floor `SeasonPage`'s `yearOptions` at `Math.min(EARLIEST_YEAR, year)`, mirroring its existing upward widening, so a URL-addressed year before 1989 has a valid `<select>` value (design decision 8).
- [x] 4.3 Verify by hand that the season page's arrows, quick-jump, and ceiling behaviour are unchanged, including stepping across a year boundary and both disabled bounds.

## 5. Recap title and stat block (frontend)

- [x] 5.1 Compose the recap `<h1>` as `${periodLabel} recap` and delete the standalone `<h2 className="recap-page__period-label">` and its CSS rule (design decision 9).
- [x] 5.2 Confirm the title still renders for an empty period, above the "nothing to recap" message.
- [x] 5.3 Wrap `renderStats` output in a `<section className="recap-page__section">` with an `<h2>Stats</h2>`, so it picks up the existing section-heading style with no new rule.
- [x] 5.4 Flip `.recap-page__lead` to `grid-template-columns: 1fr minmax(14rem, 20rem)` and reorder the JSX so the top 10 is written before the stats, keeping the 1024px collapse (design decision 5).
- [x] 5.5 Add a `:hover` state to `.recap-page__stat` using the accent background, accent border, and `var(--shadow)`, with the app's `.12s` transition, and confirm the tile neither resizes nor shifts.

## 6. Row hover (frontend)

- [x] 6.1 Add the accent `:hover, :focus-within` treatment plus transition to `.recap-top-ten-row` and `.recap-hot-take` in `RecapPage.css` (design decision 6).
- [x] 6.2 Complete `.recap-ranking-row__link`'s existing partial hover in `RankingSection.css` into the full treatment, adding `:focus-within` and the transition.
- [x] 6.3 Apply the same treatment to `.ranking-overlay__link` in `RankingOverlay.css`, so overlay rows match their inline counterparts.
- [x] 6.4 Check the three row types against an anime card on my list and a `.top-anime-card` side by side — the highlight must read as the same treatment, not a near-match.

## 7. Ranking columns (frontend)

- [x] 7.1 Replace the two `.recap-page__ranking-pair` blocks in `RecapPage.tsx` with one `.recap-page__rankings` grid holding a season column (season ranking, then seasons-by-time-watched) and a year column (year ranking, then years-by-time-watched), each column rendered only when at least one of its rankings has rows (design decision 4).
- [x] 7.2 Replace `.recap-page__ranking-pair`'s CSS with `.recap-page__rankings` (two-column grid, `:has(> :only-child)` collapse) and `.recap-page__ranking-column` (flex column with the page's section gap); delete the old rule.
- [x] 7.3 Verify a yearly recap under **What aired** shows the season column alone with no empty year column beside it, and that a multi-year recap shows all four rankings in two columns.
- [x] 7.4 Verify the 1024px breakpoint stacks the rankings season-first with no `order` property involved.

## 8. Period stepper and season link (frontend)

- [x] 8.1 Add previous/next buttons beside `renderPeriodControls`'s selects, rendered in season and yearly modes only, stepping one season (via `shiftSeason`) or one year and writing the change through `updateParams` so it lands in the URL (design decision 7).
- [x] 8.2 Disable each arrow at the bounds of the recap's own year range — winter of `min(EARLIEST_YEAR, selected)` through fall of `max(currentYear, selected)` in season mode, and the same years in yearly mode.
- [x] 8.3 Style the arrows on `.season-page__nav button`'s pattern (round, icon-only, disabled state), and give them `aria-label`s naming the unit they step.
- [x] 8.4 Verify stepping preserves mode, time filter, ranking basis, and media-type narrowing, and that the stepped-to period survives a reload.
- [x] 8.5 Add a season-mode-only `<Link to={/season?year=…&season=…}>` control to the recap's controls row, labelled for the season browser it opens.
- [x] 8.6 Verify the link from an old season (for example fall 1975) opens the season browser with that year shown in its quick-jump, exercising task 4.2.

## 9. Verification

- [x] 9.1 Build the backend against the .NET 10 SDK Docker image and run the full test suite; every `Recap*` and `Profile*` suite must pass.
- [x] 9.2 Build the frontend with the Node 22 toolchain and confirm no type errors from the `utils/anime.ts` move.
- [x] 9.3 Walk each recap mode end to end — season, yearly, multi-year — checking the title, stats placement and heading, hover on all four row types, ranking columns, posters on every ranked row, arrows, and (in season mode) the season link.
- [x] 9.4 Open the profile page and confirm Favourite seasons and Favourite years show posters on every row inline and in the "See all" overlay, and that the opinion-divergence lists are unchanged.
- [x] 9.5 Find a period whose entries all sit inside the neutral band and confirm the recap shows the "no hot takes for this period" note rather than five near-agreements.
- [x] 9.6 Run `openspec validate polish-recap-page --strict` and resolve anything it reports.

## 10. Yearly ranking layout follow-up

- [x] 10.1 In a yearly recap, lay the season ranking and seasons-by-time-watched side by side in `.recap-page__rankings`'s two columns instead of stacked in one column, reusing the existing grid and `:has(> :only-child)` collapse; the multi-year season-column/year-column grouping is unaffected (design decision 4 follow-up).
- [x] 10.2 Drop posters from every row of `BuildSeasonTimeRanking`, including the leader — the narrower side-by-side column made them cramped next to the score ranking. `BuildYearTimeRanking` is unaffected and keeps leader-only posters (design decision 3 follow-up).
- [x] 10.3 Update `RecapTimeRankingDto`'s doc comment (`RecapDto.cs` and `frontend/src/api/types.ts`) to describe the two levels' poster rules separately rather than with one shared sentence.
- [x] 10.4 Update `RecapRankingBuilderTests`: rewrite the season-time-ranking poster test to assert no row carries posters, and add a `BuildYearTimeRanking` poster test since it no longer shares coverage with the season-level builder.
- [x] 10.5 Verify by hand: a yearly recap shows the two season-level rankings side by side with no posters on the time-watched one; a multi-year recap's season/year column grouping and years-by-time-watched's leader posters are unchanged.
