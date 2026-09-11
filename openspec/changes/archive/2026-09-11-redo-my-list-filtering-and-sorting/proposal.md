## Why

My list's filter/sort bar grew one control at a time across many changes and now reads as one undifferentiated strip of about eleven pills and dropdowns. Nothing separates "narrow the list" from "order the list". "Airing" shows up as three controls that look like duplicates: a filter, a sort key, and a "show first" select that pops in mid-row and pushes everything after it. The Type/Airing popovers land on top of whatever the wrap happened to put beneath them. On top of that, a full-width recap-scope banner shoves the list down, and "Recap a period" is dressed as a status tab even though it opens an overlay. A small fix would not hold up, so this change redesigns the whole area above the list in one pass.

## What Changes

**Layout of the page above the list**

- **Page header**: "My list" on the left and **Recap a period** on the right, as a page action. It moves out of the status-tab row and stops looking like a tab.
- **Status tabs**: unchanged in behaviour. The rounded pill shape becomes exclusive to them. Nothing else above the list is a pill.
- **Controls block**: two labelled groups, each on its own row:
  - **Filter**: find-in-list, Type, Airing, Score, Started.
  - **Sort**: sort key, direction, "then by", grouping.

  Each group wraps within its own row under its own label. The two never merge into one strip, and nothing is pinned right with `margin-left: auto`.
- **Results line**: a new line directly above the list, present whenever the list has loaded. It holds the recap-scope chip (when scoped), the count, and the reset action at its far end. Starting to filter no longer inserts a line that pushes the list down.
- **Empty states**: the "nothing matches" and "nothing here yet" states get real visual weight instead of faint plain text.

**Controls reshaped**

- **Started** changes from a pill toggle to a checkbox control, so it no longer looks like a status tab.
- **Reverse** changes from a pill to a direction button that names the order it produces ("Highest first" / "Lowest first", "A–Z" / "Z–A", "Newest first" / "Oldest first"…). The button holds one width, so switching keys never moves the "then by" select.
- **Group by status** changes from a pill to a two-option choice: **By status** | **Single list**.
- **Airing "show first"** stops being a conditional third control. It folds into the sort key choices: under an "Airing status" heading, both sort selects offer "Finished airing first", "Currently airing first", and "Not yet aired first". Choosing Airing status never makes a control appear. The airing *filter* stays in the Filter row, so filter and sort key are told apart by the group they sit in.
- **Clear filters** becomes **Reset filters & sort** (the old name hid that it resets the sort too) and moves to the results line. Its behaviour is unchanged.
- The **recap-scope banner** becomes a compact chip at the start of the results line, carrying the same label, "View recap" link, and dismiss control.
- **Active filters are marked**: every Filter control that is currently narrowing the list shows the active accent at rest, so it's readable at a glance what's in force.
- **Shared `FilterMultiSelect` popover**: the panel stays inside the viewport, reads as an elevated layer attached to its trigger, and never reflows the page. The trigger gains a dropdown chevron and the same "narrowing" accent. The shared control changes on the Search, Season, and Year pages too.

**Every existing capability, and what happens to it**

| Capability | Decision |
|---|---|
| Find-in-list (case-insensitive substring over both titles, combined with everything) | Kept. First in the Filter group; gains an inline clear button. |
| Type filter (present types + Unknown, All/None) | Kept as-is, in the Filter group. |
| Airing-status filter (every status tab, All/None) | Kept as-is, in the Filter group. |
| Score filter (Any / Rated / 10–1 / Unrated) | Kept as-is, in the Filter group. |
| Started filter | Kept. Reshaped into a checkbox control. |
| Two-level sort (11 keys, natural directions, missing last, My-score ties broken by ranking, then alphabetical) | Kept. The direction control is reshaped to name its order. |
| "Which airing status shows first" | Reshaped. It is now part of choosing Airing status in either sort select, instead of a separate conditional control. The tiebreaker's airing choice is now visible instead of silently reusing the primary's hidden setting. |
| Reversing an Airing-status sort | **Cut.** With Airing status as the primary key, the direction control is shown unavailable; which status comes first is the choice. Reversing a three-value rotation only reorders the two statuses in the middle, and today it also puts unknown-airing entries *first*, breaking "missing values sort last". |
| Group-by-status toggle; rank numbers only when flat and not alphabetical | Kept. Reshaped into a two-option choice. |
| Clear-filters action (only when off-default) | Kept as **Reset filters & sort**, in the results line. It also resets the airing-first choice. |
| "Showing N of M" count | Reshaped. Always shown once loaded ("340 anime" when unfiltered, "Showing 12 of 340" when narrowed), so the list never jumps when filtering starts. |
| Empty-list vs. nothing-matches states | Kept as two distinct states, now visually weighted. |
| Recap a period entry point + picker overlay (types, settings, availability rules) | Kept. The entry point moves to the page header; the picker is unchanged. |
| Recap scope indicator; arriving scoped / pre-narrowed from "see all", a stat, or a distribution row | Kept. The indicator becomes a compact chip; the URL vocabulary and the `focus` seeding are unchanged. |
| Back/forward restoration of every control (`useRestorableState`), including `focus` seeding | Kept. Every restorable-state key and value shape is unchanged. |

**Spec corrections carried along**

- `library-views` "Recap a period control on my list" still says confirming the picker *opens the recap*. The page, and `list-recaps`, apply the period as a scope on the list instead. The delta aligns the two.
- `library-views` "My list grouping is an explicit choice" lists a group order without Rewatching. The delta corrects it to the standard order.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `library-views`: rewrites the my-list controls surface:
  - the filter bar becomes two labelled groups in which no control appears, disappears, or resizes, and only status tabs are pills
  - Find in list gains an inline clear
  - Started becomes a checkbox control
  - two-level sorting folds the airing "show first" choice into the sort keys, adds a direction control that names its order, and makes direction unavailable for Airing status
  - grouping becomes a two-option choice
  - the results line is always present and hosts the scope chip and the reset action; empty states gain weight
  - Recap a period moves to the page header, and its confirm behaviour is corrected
  - the scope indicator becomes a compact chip
  - adds marking of the filters in force
- `list-recaps`: "Scoping my list to a period from my list" moves the period control from the status-tab row to my list's page header, and the indicator becomes a chip in the results line.
- `page-header-design`: My List's cluster-height coverage is updated for the new controls. Adds the shared multi-select panel's layering/viewport rule and the trigger's "narrowing" treatment.

## Impact

**Frontend only.** No backend, API, DTO, schema, or dependency change; `getMyList` already delivers everything every control reads.

- `frontend/src/pages/MyListPage.tsx` / `.css`: page structure (header action, tabs, controls block, results line, empty states); sort-choice decoding; reset also resets airing-first.
- New `frontend/src/components/MyListControls.tsx` / `.css`: the Filter and Sort groups, direction button, grouping choice.
- New `frontend/src/components/RecapScopeChip.tsx` / `.css`: the compact scope indicator.
- New `frontend/src/components/StableLabel.tsx` / `.css`: the "reserve the widest label" technique `FilterMultiSelect` uses today, extracted so the direction button reuses it rather than copying it.
- `frontend/src/components/FilterMultiSelect.tsx` / `.css`: viewport-aware panel alignment, elevation, chevron, narrowing accent. This is visible on the Search, Season, and Year pages too.
- `frontend/src/utils/anime.ts`: `sortComparator` ignores direction for Airing status, so missing airing status stays last.
- Unchanged: `MyListRow.tsx`, `RecapPickerOverlay.tsx`, `RecapPage.tsx`'s `myListScopeSearch`, and every `useRestorableState` key. Existing snapshots and recap deep links keep working without migration.
