## 1. The ranking DTOs carry per-score poster buckets (design D2)

- [x] 1.1 In `backend/AnimeTracker.Api/Services/Recap/RecapDto.cs`, add `public record RecapRankingScorePostersDto(int Score, List<RecapRankingPosterDto> Posters)` beside `RecapRankingPosterDto`. Document it as one score's best three anime of a group, in my ranking's order.
- [x] 1.2 Replace `List<RecapRankingPosterDto> TopPosters` with `List<RecapRankingScorePostersDto> PostersByScore` on `RecapSeasonRankingDto` and `RecapYearRankingDto`, keeping the field in the same position so the record's shape is otherwise unchanged.
- [x] 1.3 Rewrite both records' doc comments: the buckets are sparse (only scores the group actually holds), ordered from 10 down, each capped at three; the **All** posters are the first three read from the top of that list and are deliberately *not* shipped separately, because reading them from the buckets is what keeps the filtered and unfiltered posters from ever disagreeing (design D2). Note that `ScoreCounts` is unchanged and still carries *full* counts, which a three-capped bucket cannot supply.
- [x] 1.4 Leave `RecapTimeRankingDto.TopPosters` exactly as it is, and say in its comment that it is deliberately not bucketed: the time rankings rank on seconds watched and pick their leader's posters by episodes watched.

## 2. Posters are picked by my ranking (design D1)

- [x] 2.1 In `Services/Recap/RecapRankingBuilder.cs`, add an `AnimeRankingSnapshot rankingSnapshot` parameter to `BuildSeasonRanking` and `BuildYearRanking`. Make it required rather than defaulting to `AnimeRankingSnapshot.Empty` — a silent fallback would quietly restore title ordering at any call site that forgot it.
- [x] 2.2 Replace `TopPosters(List<UserAnimeEntry> scored)` with `PostersByScore(List<UserAnimeEntry> scored, AnimeRankingSnapshot snapshot)`: group the scored entries by `MyScore`, order the groups by score descending, and within each group order by `snapshot.RankOf(e.AnimeId)` ascending with nulls last, then title `OrdinalIgnoreCase`, then `AnimeId`; take `PosterCount` (3) per group.
- [x] 2.3 Comment why the old `OrderByDescending(MyScore).ThenBy(Title)` is gone: `AnimeRankingKey` compares score before anything else, so ordering by rank alone is already score-descending, and the `ThenBy(Title)` was the app's only place answering "which of these two anime I scored the same do I prefer" alphabetically instead of from the one ranking. Name the nulls-last case explicitly (a scored Plan-to-watch or not-yet-aired anime, which `RankBandResolver` leaves outside the ranking) and point at `RecapPage.compareByRankThenTitle` as the same convention on the client.
- [x] 2.4 Leave `BuildSeasonTimeRanking`, `BuildYearTimeRanking`, and `TopPostersByTime` untouched, along with `CompareGroups`, `BuildHistogram`, `WeightedAverage`, and `ScoredMean` — the ranking order itself does not change.

## 3. Both callers pass the snapshot they already hold (design D1, D6)

- [x] 3.1 In `Services/Recap/RecapService.cs`, thread the existing `rankingSnapshot` through `BuildRankings` into the two score-ranking calls. Note in the comment that it is the whole-list snapshot, not a period-scoped one — a poster's placement is its place in my whole ranking, matching how `MyRank` is already resolved for `ToRow`.
- [x] 3.2 In `Services/Profile/ProfileService.cs`, build the `AnimeRankingSnapshot` once in `GetProfileAsync` from `entries` and `orderedAnimeIds`, and pass it to both `BuildTopAnimeSection` and `BuildFavouriteSeasonsAndYears`.
- [x] 3.3 Change `BuildTopAnimeSection(entries, orderedAnimeIds, mediaType)` to `BuildTopAnimeSection(entries, snapshot, mediaType)` and have `GetTopAnimeSectionAsync` build its own snapshot from its own read, so neither path orders the whole list twice.
- [x] 3.4 Change `BuildFavouriteSeasonsAndYears(entries)` to take the snapshot and pass it into both `RecapRankingBuilder` calls. Its "byte-identical to a recap's score" comment still holds and should stay.

## 4. Backend tests follow the rename and cover the new rules

- [x] 4.1 In `AnimeTracker.Api.Tests/Services/Recap/RecapRankingBuilderTests.cs`, update every `BuildSeasonRanking`/`BuildYearRanking` call site to pass a snapshot — `AnimeRankingSnapshot.Empty` for the tests that assert on order, counts, or weighted scores and say nothing about posters.
- [x] 4.2 Rewrite `EveryRankedRowCarriesPosters` against `PostersByScore`: every ranked row has at least one bucket, no bucket holds more than three, and reading three from the top of the buckets yields the group's three best.
- [x] 4.3 Add a test that my ranking breaks a within-score tie: a group holding four anime at one score, with a stored order placing them in an order that disagrees with their titles, yields exactly the three best-ranked, in ranking order.
- [x] 4.4 Add a test that an anime outside the ranking (scored but Plan-to-watch) sorts after every ranked anime carrying its score, and is dropped when three ranked ones are available.
- [x] 4.5 Add a test that the buckets are sparse and score-descending: a group holding scores 10, 8, and 8 yields a bucket for 10 and a bucket for 8, in that order, and none for the scores it does not hold.
- [x] 4.6 Confirm the two time-ranking poster tests (`...SeasonTimeRow...`, `OnlyTheLeadingYearTimeRowCarriesPostersChosenByEpisodesWatched`) still compile and pass unchanged.
- [x] 4.7 In `AnimeTracker.Api.Tests/Services/Profile/ProfileServiceFavouriteSeasonsAndYearsTests.cs`, follow `TopPosters` to `PostersByScore`, and check that the profile's favourites posters agree with what a recap covering the same year returns for the same group.
- [x] 4.8 Run the backend test suite (per the .NET 10 SDK image note in the repo's build memory — the local SDK is 9.0).

## 5. Frontend types and the shared poster rule (design D2, D3)

- [x] 5.1 In `frontend/src/api/types.ts`, add `export type RecapRankingScorePostersDto = { score: number; posters: RecapRankingPosterDto[] }`, and replace `topPosters` with `postersByScore: RecapRankingScorePostersDto[]` on `RecapSeasonRankingDto` and `RecapYearRankingDto`. Carry over the DTO comment's sparse/score-descending/three-capped note. Leave `RecapTimeRankingDto.topPosters` alone.
- [x] 5.2 In `frontend/src/components/RankingSection.tsx`, add `postersFor(row: { postersByScore: RecapRankingScorePostersDto[] }, selectedScore?: number)`: with a score, that score's bucket (or an empty list when the group holds none); without one, the first three across the buckets read from the top. Comment that the second branch is the group's three best by ranking precisely because the buckets are score-ordered and each holds its own score's best, so **All** needs no separate list (design D2).
- [x] 5.3 Call `postersFor` from `describeSeasonRanking` and `describeYearRanking` in place of `row.topPosters`, passing the `selectedScore` each already takes for its `meta` line.
- [x] 5.4 Extend both functions' doc comment with the rule that they must never be passed to `.map` point-free: their optional second parameter would receive the array index and mislabel every row (design "A live defect"). Every call site is an explicit arrow.
- [x] 5.5 Confirm `RankingOverlay.tsx` and `renderPosters` need no change — both consume `RankingOverlayRow.posters` and neither knows a filter exists.

## 6. The score filter moves into `RankingSection` (design D4)

- [x] 6.1 Move `offeredScores` and `rankByScoreCount` out of `ProfilePage.tsx` into `RankingSection.tsx`, beside `scoreCountAt`, and export them. Carry their comments across verbatim — especially the one recording that the stable sort is what makes a tie fall back to the backend's own order.
- [x] 6.2 Move the `— with most Ns` overlay-title suffix into `RankingSection`: it holds both `title` and `scoreFilter.selected`, so it composes the title before calling `onSeeAll`. Both pages' `onSeeAll` then reduce to their plain setter.
- [x] 6.3 In `ProfilePage.tsx`, import the two moved functions from `RankingSection.tsx`, delete the local copies, and simplify both `onSeeAll` props to `setRankingOverlay`. Keep the two `useRestorableState` selections and the offered-score validity check that falls back to `null`.
- [x] 6.4 Confirm `ProfilePage`'s two `RankingSection` calls are otherwise unchanged, and that the profile page still opens on **All**, re-ranks, and restores a selection on back-navigation exactly as it does today.

## 7. The recap page's rankings gain the filter (design D5)

- [x] 7.1 In `RecapPage.tsx`, read `seasonScore` and `yearScore` from `searchParams`, parsing each as an integer in 1-10 and treating anything else as `null`.
- [x] 7.2 Derive `offeredSeasonScores`/`offeredYearScores` from the displayed recap's two score rankings via the shared `offeredScores`, and gate each parsed selection on its ranking actually offering it — an unoffered selection reads as `null` (**All**) without being removed from the URL, so stepping back to a period that offers it restores the selection.
- [x] 7.3 Give `renderSeasonRankingSection` and `renderYearRankingSection` the `scoreFilter` prop: the offered scores, the effective selection, and an `onSelect` writing `updateParams({ seasonScore: … })` / `{ yearScore: … }` with `{ keepScroll: true }` (null clears the parameter). Match the basis toggle and type select, which pass `keepScroll` for the same reason.
- [x] 7.4 Replace `rows={recap.seasonRanking.map(describeSeasonRanking)}` and the year equivalent with explicit arrows that pass the effective selection, and apply `rankByScoreCount` when one is selected — the same two-branch shape `ProfilePage` uses. **This is the fix for the mislabelled rows**: the point-free `.map` was handing each row's index to `selectedScore`.
- [x] 7.5 Leave `renderSeasonTimeRankingSection` and `renderYearTimeRankingSection` without a `scoreFilter`, and leave their `describeTimeRanking` calls as they are (that function takes no second parameter, so its `.map` is safe).
- [x] 7.6 Confirm the rankings' own gating is untouched: no score ranking under **What I watched** or on a season recap, no year ranking off a multi-year recap — so the control comes and goes with its ranking and needs no gate of its own.
- [x] 7.7 Check the two-column grid placement (`gridColumn`/`gridRow` on each section) still lines the season and year score rankings up on row 1 with the control row present in both — and that a column whose ranking offers a filter while the other does not stays aligned.

## 8. Presentation (design D7, D8)

- [x] 8.1 In `frontend/src/components/RankingSection.css`, give `.ranking-score-filter__button` a `min-width` sized to the widest label (**All**) and reduce the horizontal padding so short labels are not padded past it. Verify `1`, `10`, and `All` come out the same width at the control's 13px size, and that **All** is not clipped.
- [x] 8.2 Comment that the `min-width` sits on the base rule rather than on any state, so the "no state changes a button's size" requirement still holds, and that a floor was chosen over a fixed width so a label can never be clipped.
- [x] 8.3 In `frontend/src/pages/RecapPage.css`, add a `max-width` to `.recap-top-ten-row__title` expressed in `ch` so it tracks the row's own `calc(1em + 1px)` size. Keep the existing `overflow`/`text-overflow`/`white-space` and the row's native `title` tooltip.
- [x] 8.4 Comment why capping the title moves nothing else: the score is a separate flex item pinned at the row's trailing edge and the link keeps `flex: 1 1 auto`, so the rank, poster, score column, and row height are all unchanged — which the "A top 6-10 row's score is inset from the row's edge" requirement demands of any change here.
- [x] 8.5 Confirm at a narrow width that the title still shrinks below the cap rather than the cap widening the row or forcing horizontal scroll.

## 9. Verification

- [x] 9.1 Build the frontend (`PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` in `frontend/`, per the repo's Node memory) and run `npm run lint`.
- [x] 9.2 On the profile page: both favourites rankings show three posters per row; a within-score tie is ordered by my ranking, best-ranked leftmost; selecting a score moves each row's posters to that score; **All** restores the group's best; the "See all" overlay matches inline under both.
- [x] 9.3 On a multi-year recap under **What aired**: the Season and Year rankings each show the filter; their rows read `N scored · W` on **All** rather than `undefined × 0 · N scored`; selecting a score re-ranks, re-labels, and re-illustrates; the page does not jump to the top.
- [x] 9.4 Reload a recap with `seasonScore` in the URL and confirm the selection comes back; step the period to one lacking that score and confirm the ranking falls back to **All**; step back and confirm the selection returns.
- [x] 9.5 Confirm the two time-watched rankings are unchanged — no filter, no posters on seasons-by-time-watched, leader-only posters on years-by-time-watched.
- [x] 9.6 Confirm the score buttons are one width across `All` and `10`…`1`, and that a long title on a top 6-10 row now stops at the cap with the score still in its column.
