## 1. Filter logic

- [x] 1.1 In `frontend/src/pages/ProfilePage.tsx`, add a `mainLineEntryCount(item: TopSeriesItemDto): number` helper beside `topSeriesBasisValue`, returning `item.mainLineAiredCount` (revised by task 6 below — originally `item.malMain.totalCount`), with a comment recording why that field is the right count to read.
- [x] 1.2 Add a `filterMultiEntry(items: TopSeriesItemDto[]): TopSeriesItemDto[]` helper that keeps items with `mainLineEntryCount(item) > 1`, as a step separate from `rankTopSeries` (design.md decision 3). Leave `rankTopSeries` unchanged.

## 2. Toggle state and derived lists

- [x] 2.1 Add `const [topSeriesMultiOnly, setTopSeriesMultiOnly] = useRestorableState<boolean>('topSeriesMultiOnly', false)` next to the existing `topSeriesBasis` state (design.md decision 6), so it resets on a fresh visit and restores on back-navigation.
- [x] 2.2 Keep `rankedTopSeries` as the basis-ranked list, and derive `displayedTopSeries = topSeriesMultiOnly ? filterMultiEntry(rankedTopSeries) : rankedTopSeries`. Both lists are needed for the empty-state precedence in task 4.

## 3. The control

- [x] 3.1 In the Top series `profile-box__header-row`, beside the `<h2>Top series</h2>`, add a `type="button"` toggle labelled `Multi-entry only` using the existing `profile-box__control` class, with `aria-pressed={topSeriesMultiOnly}` and an `onClick` that flips the state (design.md decision 4). Do not add it to the `profile-media-tabs` row.
- [x] 3.2 Give the pressed state a visible style: add a `profile-box__control--active` modifier in `frontend/src/pages/ProfilePage.css` (accent border and heading-strength text, matching how `profile-media-tabs__tab--active` reads against its inactive siblings) and apply it when the toggle is on.
- [x] 3.3 Render the control whenever the section renders, including its empty states, so the filter can always be switched back off.

## 4. Empty states

- [x] 4.1 Rework the Top series render branches to the precedence in design.md decision 5: loading/no items → the existing "series are still being discovered" message with the Settings link; `rankedTopSeries.length === 0` → the existing per-basis message; `displayedTopSeries.length === 0` → a new message stating the multi-entry filter left nothing to show, with no Settings link.
- [x] 4.2 Render the strip from `displayedTopSeries` instead of `rankedTopSeries`.

## 5. Verification

- [x] 5.1 Build and lint the frontend (`nvm use 22` first — the default node is v16 and Vite's build needs 22): `npm run build` and `npm run lint` in `frontend/`, both clean.
- [x] 5.2 Walk the profile page: default lists single-entry series unchanged; pressing the toggle hides every series whose main line is one entry (including one-season-plus-OVAs series) and leaves the survivors' order untouched; pressing again restores them; the control reads as active while on.
- [x] 5.3 Check the two controls compose: switch the basis with the filter on — the strip reorders and single-entry series stay hidden, and switching the filter afterwards leaves the basis where it was.
- [x] 5.4 Check restore behaviour: turn the filter on, open a series tile, press back — still on; navigate to the profile page fresh — back to off.
- [x] 5.5 Check the new empty state by turning the filter on with a basis under which every rankable series has a single main-line entry (if no real data reaches it, force `filterMultiEntry` to return `[]` temporarily) — the filter message shows, not the Settings-link message.

## 6. Don't count an announced-but-unaired sequel as a second entry

- [x] 6.1 In `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs`, compute `mainLineAiredCount = mainLine.Count(m => m.AiringStatus != "not_yet_aired")` in `EligibleSeries()` and add `MainLineAiredCount` to `SeriesRankingResult` (design.md decision 2, revised).
- [x] 6.2 Add `MainLineAiredCount` to `TopSeriesItemDto` (`Services/Profile/ProfileDto.cs`) and thread it through `ProfileService.ToTopSeriesItem`.
- [x] 6.3 Add `mainLineAiredCount: number` to the frontend `TopSeriesItemDto` type and change `mainLineEntryCount(item)` (task 1.1) to read `item.mainLineAiredCount` instead of `item.malMain.totalCount`.
- [x] 6.4 Add a `SeriesRankingLookupTests` case: a series with one finished-airing main-line member and one `not_yet_aired` main-line member has `EntryCount == 2` but `MainLineAiredCount == 1`.
- [x] 6.5 Backend build (`dotnet build`, via the SDK 10 Docker image) and full test suite clean; frontend `npm run build`/`npm run lint` clean. Verified against the live profile's real `/api/profile/top-series` data: 26 series (e.g. Cyberpunk: Edgerunners, Ao Ashi, Dungeon Meshi) flip from multi-entry to single-entry because their second main-line season is announced but hasn't aired yet.
