## Why

The My list page's sort control is disconnected from the status group it affects, and the Plan to watch group — the one place airing status is already surfaced as a badge — has no way to sort by that airing status. Both are small usability gaps in the existing My list grouping/sorting feature.

## What Changes

- Add a new sort option, "Airing status," available when viewing the Plan to watch group (via the "Plan to watch" filter tab, or within the Plan to watch group when "All" is selected). It orders entries by airing status: Currently airing → Not yet aired → Finished airing.
- Move the sort control off the page header (next to the "My list" title) and place it inline with each status group's title (e.g. "Watching", "Completed") so the control sits next to the section it affects.
- When the flat ranked view is active (sorting by MAL score or my score), retain a single header line pairing the active status title (or "All") with the sort control, since ranked mode collapses groups into one list.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `library-views`: The "My list quick-filter control" requirement gains an airing-status sort option scoped to Plan to watch entries. The layout of the sort control changes from the page header to alongside each status group's title (or the active filter's title in ranked/single-status view) instead of beside the "My list" page heading.

## Impact

- Frontend only: `frontend/src/pages/MyListPage.tsx` and `frontend/src/pages/MyListPage.css`.
- `frontend/src/utils/anime.ts` gains a comparator/ordering helper for airing status, mirroring the existing `compareByMalScoreDesc` / `compareByTitleAlphabetical` helpers.
- No API or backend changes — airing status is already returned on `MyListItemDto`.
