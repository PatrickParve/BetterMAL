## 1. Backend: one per-series watch total

- [x] 1.1 In `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs`, add `private static long SeriesWatchedSeconds(IEnumerable<SeriesRankingMemberProjection> members) => members.Sum(MemberWatchedSeconds);` beside `MemberWatchedSeconds`. Comment it with design D4: the one franchise total both `TimeSpentSeries()` and `ListedSeries()` read, summed over every member (main line, extras and held version neighbours), never over the default-combination `scopedMainLine`, so the two surfaces cannot drift apart.
- [x] 1.2 Switch `TimeSpentSeries()`'s own `members.Sum(MemberWatchedSeconds)` to call `SeriesWatchedSeconds(members)`.

## 2. Backend: "Most time spent" requires the main series watched

- [x] 2.1 In `TimeSpentSeries()`, before summing, skip the series unless `members.Any(m => m.IsMainLine && MemberWatchedSeconds(m) > 0)`. The existing `totalSeconds <= 0` skip becomes redundant; remove it rather than keep a dead guard.
- [x] 2.2 Rewrite `TimeSpentSeries()`'s summary comment. Replace the D8 claim that "a total above zero is exactly…" with the new listing rule (this change's design D1/D2): some main-line member, any alternative including a non-default one, contributes watch time; extras and version neighbours never satisfy the gate; and the gate decides membership only, so extras still count in full toward a listed series' total. Keep the existing note that `EligibleSeries()`'s coverage rule and `ListedSeries()`'s version-neighbour rule do not apply.
- [x] 2.3 Update the doc comments that restate the old rule: `TimeSpentSeriesItemDto`/`TimeSpentSeriesSectionDto` in `backend/AnimeTracker.Api/Services/Profile/ProfileDto.cs` ("Every series with above-zero total watch time") and the `GetTimeSpentSeries` action's summary in `backend/AnimeTracker.Api/Controllers/ProfileController.cs`. They should state "every franchise with at least one main-line member watched". Also update the matching comment on `TimeSpentSeriesSectionDto` in `frontend/src/api/types.ts`.

## 3. Backend: the Series list carries the total

- [x] 3.1 In `backend/AnimeTracker.Api/Services/Series/SeriesListDto.cs`, append `long WatchedSeconds` as the last positional parameter of `SeriesListItemDto`. Extend its summary comment with one sentence: the franchise total "Most time spent" ranks by, ungated, and the Time spent sort's key (design D3/D5).
- [x] 3.2 In `SeriesRankingIndex.ListedSeries()`, compute `var watchedSeconds = SeriesWatchedSeconds(members);` over `members`, not `scopedMainLine`. Pass it into the `SeriesListItemDto` construction. Add a short comment that this figure deliberately covers every member, unlike the episode figures above it (design D4, series-browser "a sort key and not a card figure").
- [x] 3.3 Fix every other `SeriesListItemDto` construction site the compiler flags (tests and fixtures), passing `0` where the figure is irrelevant.
- [x] 3.4 Leave `SeriesListService`'s default order untouched.

## 4. Backend tests

- [x] 4.1 In `backend/AnimeTracker.Api.Tests/Services/Profile/ProfileServiceTimeSpentSeriesTests.cs`, rewrite `AnExtraCounts` so the main-line member is also watched (e.g. 1 episode × 1500s). Assert that the total is the main-line contribution plus the extra's 7200s. The extra still counts once the series qualifies.
- [x] 4.2 Add `AFranchiseWithOnlyAnExtraWatchedIsOmitted`: main line not in my list, one extra completed. Assert that `Items` is empty. This is the old `AnExtraCounts` fixture, now expected to be omitted.
- [x] 4.3 Add `APlanToWatchMainLineWithAWatchedExtraIsOmitted`: main line in my list as PlanToWatch with 0 episodes, and an extra completed. Assert that `Items` is empty.
- [x] 4.4 Add `ADroppedMainLineEntryWithOneEpisodeQualifies`: one main-line member Dropped at 1 of 12 episodes. Assert a single item totalling 1 × duration.
- [x] 4.5 Add `ARewatchingMainLineEntryWithNoNewEpisodesQualifies`: a main-line member Rewatching with `EpisodesWatched = 0`, `RewatchCount = 0` and `TotalEpisodes = 12`. Assert that it is listed with 12 × duration, the first viewing counted as one full run.
- [x] 4.6 Add `ANonDefaultAlternativeQualifies`, constructing design D2's edge case. The trunk root 100 is main line, has `BranchHeadAnimeId = null` and is not in my list. Alternatives 101 and 102 are both main line with `VersionSlotKey = 101`, and each has `BranchHeadAnimeId` set to its own id (a branch includes its alternative, per `SeriesVersionSlots.VersionSlot`). 101 has `MalScore = 9.0` and is not in my list. 102 has `MalScore = 7.0` and `TotalEpisodes = 12`, and is in my list as Rewatching with `EpisodesWatched = 0`. Rule 1 sees nothing watched, so rule 2 makes 101 the default. Assert that the series is listed with 102's full first run. The `AddMember` helper will need optional `malScore`/`versionSlotKey`/`branchHeadAnimeId` parameters.
- [x] 4.7 Add `AVersionNeighbourAloneDoesNotQualify`: a series whose main-line member is not in my list, holding a `NeighbourTelling` member with `IsMainLine = false` that is completed. Assert that `Items` is empty (design D2's pinning test).
- [x] 4.8 Confirm that the existing tests still pass unchanged: `AMainLineMembersFirstViewingCounts`, `RewatchesAddOnTop`, `AMemberNotInMyListContributesNothing`, `APartiallyWatchedCurrentlyAiringMemberContributesItsWatchedEpisodes`, `ARewatchingMemberContributesOneFullRunPlusInProgressEpisodes`, `AFranchiseWithNothingWatchedIsOmittedRatherThanListedAtZero`, `AFranchiseWithOneWatchedEntryOutOfManyIsListed`, `OrderedByTotalDescendingThenTitle` and `ASingleEntryFranchisesWatchedSecondsEqualsItsContributionToTheDaysStat`. Each already has a watched main-line member.
- [x] 4.9 In `backend/AnimeTracker.Api.Tests/Services/Series/SeriesListFiguresTests.cs`, add `WatchedSeconds_SumsEveryMemberIncludingExtrasAndRewatches`: a main-line member with a rewatch plus a watched extra. Assert the combined total. Extend the file's `AddAnime`/`AddEntry` helpers with optional `averageEpisodeDurationSeconds`/`rewatchCount` parameters as needed.
- [x] 4.10 Add `WatchedSeconds_ExtraOnlySeriesStillCarriesItsTotal`: main line not in my list and an extra completed. Assert that the series is listed with the extra's total, not zero (design D3).
- [x] 4.11 Add `WatchedSeconds_NothingWatchedIsZero`: every in-list member is PlanToWatch at 0 episodes. Assert `0`.
- [x] 4.12 Add `WatchedSeconds_CoversANonDefaultAlternative`: the same slot shape as 4.6, with alternative 101 at 12 episodes watched and 102 at 3, so rule 1 makes 101 the default. Assert that `WatchedSeconds` includes 102's 3 episodes, while `MainLineWatchedEpisodes` (default combination) does not.
- [x] 4.13 Add `WatchedSeconds_EqualsTheProfilesTimeSpentTotal`: build one qualifying series with a main line, an extra and a rewatch on one fixture. Call both `SeriesRankingIndex.ListedSeries(...)` and `TimeSpentSeries()` on the same loaded index and assert equal totals (design D4's pinning test).
- [x] 4.14 Run `dotnet test` in `backend/` and confirm the whole suite passes.

## 5. Frontend: Time spent sort

- [x] 5.1 In `frontend/src/api/types.ts`, add `watchedSeconds: number` as the last field of `SeriesListItemDto`, with a one-line comment: the franchise total "Most time spent" ranks by, ungated, used only by the Time spent sort.
- [x] 5.2 In `frontend/src/utils/anime.ts`, add `'timeSpent'` to `SeriesSortKey` and update the "seven sort orders" comment to eight. Add `timeSpent: seriesNullsLast((item) => item.watchedSeconds, 'descending')` to `SERIES_SORT_COMPARATORS`, commented per design D5: never null, so zero-total series fall to the end by value and are ordered by `sortSeries`' title tie-break.
- [x] 5.3 In `frontend/src/pages/SeriesBrowserPage.tsx`, append `{ value: 'timeSpent', label: 'Time spent' }` to `SORT_OPTIONS` after `myProgress`. Leave `DEFAULT_SORT` as `'myScore'`.

## 6. Frontend: profile empty state

- [x] 6.1 In `frontend/src/pages/ProfilePage.tsx`, change the "Most time spent" empty message from "No series have any watch time yet." to "No series have any of their main series watched yet.", keeping the Settings-page link sentence after it unchanged (design D7).

## 7. Verify

- [x] 7.1 Build the frontend with nvm's Node 22 (`npm run build` in `frontend/`) and confirm that type-checking and the Vite build succeed.
- [x] 7.2 In the running app, open `/series?sort=timeSpent`. Confirm that the control reads "Time spent", the longest-watched franchises come first, plan-to-watch-only series sit at the end in title order, and back-navigation from a series keeps the sort.
- [x] 7.3 On the profile, confirm that a franchise with only an extra watched (e.g. a movie-only franchise) no longer appears in "Most time spent", and that it does appear, with its time, under the Series page's Time spent sort.
- [x] 7.4 Spot-check two franchises present on both surfaces. The profile badge's total and the Series page's relative order agree.
