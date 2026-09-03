## Why

Four small things get in the way of using the app day to day. The shared Type/Airing filter popover's **All** and **None** shortcuts do the same thing — both leave every anime showing — so one of the two is a lie; pressing **All** then reports "Type: 5 selected" rather than "All"; and because the trigger's label changes width with the selection, the whole filter cluster shifts sideways as you use it. The navbar's type-ahead dropdown can pop back over the results after you press Enter, leaving the search field focused with a stale list of suggestions over the page you just asked for. The recap's score board is thrown away the moment you follow a poster out of it, so coming back means reopening it and scrolling to where you were. And several lists that are meant to read as content — the recap and profile "See all" rankings, the Latest updates feed, the edit-history list, the two opinion-divergence lists — carry a visible scrollbar that reads as chrome inside an already-small box.

## What Changes

**The shared multi-select filter control** (Search page Type, Season page Type, Year page Type, My List Type and Airing)

- **All** and **None** become genuinely different states. **All** applies no restriction, as today. **None** clears every checkbox and matches nothing, so the view reports that nothing is showing rather than silently reverting to everything. Ticking a box back on narrows from there.
- The trigger reports **All** when everything is in play — whether that came from the All shortcut, from a fresh visit, or from ticking every box by hand — instead of a count. It reports **None** when nothing is selected, a single option's label when one is selected, and a count only in between.
- The two shortcuts are rendered as bordered buttons matching the app's other small controls, rather than bare text that only underlines on hover.
- The trigger button holds one width, sized to the widest label it could ever show for its own options, so changing the selection never resizes it and the cluster it sits in never reflows. **BREAKING** for the Season, Year and Search pages' URL state only in that a "nothing selected" state is now representable (`?type=` present but empty); an absent parameter still means All, so existing links are unaffected.

**Navbar type-ahead**

- Submitting a search (Enter or the magnifier) closes the dropdown and leaves it closed — a request already in flight can no longer reopen it over the results — and the search field gives up focus. Typing again reopens it as before, and the submitted text still stays in the field.

**Recap score board**

- The score board's open state and its own scroll offset become part of the recap page's restorable state, so leaving the board to open an anime and coming back returns the board, scrolled where it was. It still closes on navigation like every other overlay; restoring is the page being rebuilt as it was left, not an overlay hanging over somewhere else.

**Scrollbars**

- The rankings "See all" overlay (recap's Season ranking, Year ranking, Seasons by time watched, Years by time watched; the profile's Favourite years and Favourite seasons), the profile's Latest updates feed, its two opinion-divergence lists, and the full edit-history overlay all stop drawing a scrollbar while still scrolling exactly as they do now.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `page-header-design`: adds the shared filter control's presentation rules — All/None as bordered buttons, a label that reports All/None/one/count, and a trigger width that does not change with the selection.
- `library-views`: My list's type filter and airing-status filter gain a distinct "none selected" state that matches nothing; their labels follow the shared rule.
- `season-browser`: the season type filter's "none selected" state matches nothing, and is representable in the page's URL state distinctly from All.
- `year-browser`: the same for the year type filter.
- `navigation-and-search`: the search page's type filter gains the same "none selected" state; submitting a search dismisses the type-ahead dropdown for good and blurs the field.
- `list-recaps`: the score board is restored — open and scrolled where it was — when the recap page is returned to by back/forward.
- `page-state-restoration`: an overlay's open state and a scrollable overlay's own scroll offset are restorable page state.
- `overlay-behaviour`: closing on navigation is reconciled with a restored page reopening an overlay that belongs to it.
- `profile-stats`: the "Latest updates" feed, both opinion-divergence lists, the full edit-history list, and the rankings "See all" list no longer draw scrollbars — replacing the current requirement that they lay a scrollbar beside their rows.

## Impact

**Frontend only.** No backend, API, schema or dependency changes.

- `frontend/src/components/FilterMultiSelect.tsx` / `.css` — the shared control: shortcut buttons, label rule, stable width, and the All-vs-None state.
- `frontend/src/pages/SearchPage.tsx`, `SeasonPage.tsx`, `YearPage.tsx` — URL parsing of the `type` parameter and the filter predicate.
- `frontend/src/pages/MyListPage.tsx` — the type and airing filter state, the filter predicate, and the "any filter active" test that drives its badges and its "what is being shown" line.
- `frontend/src/components/SearchBar.tsx`, `frontend/src/hooks/useAnimeSearch.ts`, `frontend/src/pages/SettingsPage.tsx` — dismissal that an in-flight response cannot undo; the settings picker shares the hook.
- `frontend/src/pages/RecapPage.tsx`, `frontend/src/components/ScoreBoardOverlay.tsx` — restorable open state and the board's scroll offset.
- `frontend/src/components/RankingOverlay.css`, `frontend/src/components/EditHistoryOverlay.tsx`, `frontend/src/pages/ProfilePage.tsx` / `.css`, `frontend/src/index.css` — the scroll containers that stop showing a scrollbar.
