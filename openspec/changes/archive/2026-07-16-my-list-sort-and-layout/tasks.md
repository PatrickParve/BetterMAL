## 1. Sort helper and type changes

- [x] 1.1 Add `'airingStatus'` to the `SortKey` union in `MyListPage.tsx`
- [x] 1.2 Add a `compareByAiringStatus` comparator to `frontend/src/utils/anime.ts` ordering entries Finished airing → Currently airing → Not yet aired (unknown/null status sorts last), alongside the existing `compareByMalScoreDesc` / `compareByTitleAlphabetical` comparators
- [x] 1.3 Wire `compareByAiringStatus` into `sortByKey`'s switch in `MyListPage.tsx`

## 2. Scope the airing-status option to Plan to watch

- [x] 2.1 Compute the sort `<select>`'s option list dynamically: base options (Alphabetical, MAL score, My score) plus "Airing status" only when `statusFilter === 'PlanToWatch'`
- [x] 2.2 In the status-tab click handler, reset `sort` to `'alphabetical'` when switching to a filter other than `PlanToWatch` while `sort === 'airingStatus'`

## 3. Relocate the sort control

- [x] 3.1 Remove the sort `<select>` from `.my-list-page__header` (page keeps just the `<h1>My list</h1>`)
- [x] 3.2 In grouped (alphabetical) mode, wrap each group's `<h2>` in a header row that also renders the sort `<select>`, reusing the same `sort`/`setSort` state for every instance
- [x] 3.3 In ranked (non-alphabetical) mode, add a single header row above the flat list showing the active filter's label (`STATUS_LABELS[statusFilter]`, or "All" when unfiltered) alongside the same sort `<select>`

## 4. Styling

- [x] 4.1 Add a `.my-list-page__group-header` flex rule (space-between, centered) in `MyListPage.css` for the `<h2>` + `<select>` row, used by both grouped and ranked headers
- [x] 4.2 Verify `.my-list-page__header` still lays out sensibly with only the `<h1>` left in it

## 5. Verification

- [x] 5.1 Manually verify: switching the Plan to watch tab shows "Airing status" in the dropdown; other tabs and "All" do not
- [x] 5.2 Manually verify: selecting Airing status sorts Plan to watch entries Finished airing → Currently airing → Not yet aired with rank numbers, and the header line shows "Plan to watch" next to the control
- [x] 5.3 Manually verify: switching away from Plan to watch while sorted by airing status falls back to alphabetical grouped view without errors
- [x] 5.4 Manually verify: in "All" grouped view, the sort control appears on each visible group's heading line and changing any instance re-sorts/re-modes the whole page consistently
