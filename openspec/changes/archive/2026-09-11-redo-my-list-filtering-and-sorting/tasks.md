## 1. Sort utility: Airing status ignores direction (design D5)

- [x] 1.1 In `frontend/src/utils/anime.ts`, make `sortComparator`'s `airingStatus` branch return `compareByAiringStatus(airingStatusFirst)` unchanged whatever `direction` is. Comment why: the chosen first status *is* the order, and negating the cycle comparator put unknown-airing entries first, contrary to "missing values sort last".
- [x] 1.2 Update the `SortDirection` comment to say `'reversed'` flips every key except Airing status, and update the "Airing status isn't here" note above `SORT_KEY_FACTORIES` to match.

## 2. Extract the reserved-width label (design D5)

- [x] 2.1 Add `frontend/src/components/StableLabel.tsx` / `.css`: `<StableLabel current candidates />` renders every candidate in one grid cell, with the non-current ones `visibility: hidden` + `aria-hidden`. Move the explanatory comment from `FilterMultiSelect.css` (the fluid font size, labels are data, why a pixel `min-width` was rejected) with the rule.
- [x] 2.2 Switch `FilterMultiSelect` to render its summary through `StableLabel` (candidates stay `${label}: ${candidate}`), and delete its now-duplicated `__button-text` rules. Confirm the trigger's width and accessible name are unchanged on the Search, Season, Year and My list pages.

## 3. Shared filter popover: attached, on screen, marked (design D9; spec page-header-design)

- [x] 3.1 In `FilterMultiSelect.tsx`, add a panel ref and a `useLayoutEffect` that runs while open. If the panel's right edge is past `innerWidth - 8`, set `alignEnd`, which applies a `filter-multi-select__panel--end` modifier (`right: 0; left: auto`). Re-evaluate on `resize` while open. Comment why it is a layout effect: the first painted frame must already be aligned.
- [x] 3.2 In `FilterMultiSelect.css`, give the panel `max-width: calc(100vw - 16px)` and a 1px `--accent-border` top edge matching the trigger's open state. Keep `z-index: 90`, with a comment naming the band it sits in: above page content (≤20), below the modal (100) and the action-failure notice (95).
- [x] 3.3 Add a border-drawn chevron to the trigger (`::after`, inside its padding, outside the `StableLabel` cell), so the trigger reads as a dropdown like the selects. Confirm the width is still constant across All / None / one / count.
- [x] 3.4 Add `filter-multi-select--active` whenever `selected !== null` (a selection or None): `--accent-bg` background, `--accent-border` border, `--text-h` text. Colours only.
- [x] 3.5 On the Search, Season and Year pages at ~400px and at full width: open the Type filter near the right edge and confirm the panel is fully on screen. Confirm opening and closing reflows nothing, and that the active accent and chevron appear without changing the cluster's height.

## 4. `MyListControls`: the two labelled groups (design D2, D3, D4, D5, D10)

- [x] 4.1 Create `frontend/src/components/MyListControls.tsx` / `.css` as a presentational component. Move `SORT_OPTIONS`, `SCORE_FILTER_VALUES` and the `ScoreFilter` type out of `MyListPage.tsx` (exporting the type). Props: a `filters` object (values + setters for query, type, airing, score, started), a `sort` object (sort, direction, then, grouping, airing-first + setters/handlers), and `typeOptions` / `airingOptions`.
- [x] 4.2 Lay the block out as a `max-content minmax(0, 1fr)` grid:
  - a label cell ("Filter", "Sort") and a `flex-wrap` controls cell per group
  - a hairline between the groups' rows, a light border and radius around the block
  - labels as small uppercase secondary text centred on the first control line
  - each controls cell `role="group"` with `aria-labelledby` on its label
  - collapse to one column below ~560px, so labels sit above their row
  - no `margin-left: auto` anywhere in the block
- [x] 4.3 Filter group, in order:
  - the Find field (`flex: 1 1 14rem; max-width: 22rem`), with a leading search glyph and an inline clear button that renders only when non-empty, sits inside the field's bounds, empties it, and refocuses the input
  - Type and Airing (`FilterMultiSelect`)
  - the Score select (options and labels unchanged)
  - the Started checkbox label
- [x] 4.4 Give each Filter control its `--active` modifier from `query !== ''`, `typeFilter !== null`, `airingFilter !== null`, `scoreFilter !== 'any'`, and `startedFilter`. Use the same accent treatment as 3.4, colours only. Sort controls get none.
- [x] 4.5 Add the `SortChoice` type with `toSortChoice(key, first)` / `fromSortChoice(choice)`. Build the primary select from the non-airing keys plus an `<optgroup label="Airing status">` of three options whose text is `${AIRING_STATUS_LABELS[s]} first`, keeping the existing key order with the group in Airing status's slot.
- [x] 4.6 Add the direction button:
  - text from the natural/reversed label table in design D5, rendered through `StableLabel`, with every label in the table plus "Set by first status" as candidates
  - accessible name "Sort direction: …", no `aria-pressed`
  - `disabled` with "Set by first status" and an explanatory `title` / `aria-description` while the primary is Airing status
- [x] 4.7 Add a visible "then" word and the tiebreaker select: "— none —" first, then every choice except the primary key. The whole Airing status group is omitted while the primary is airing; otherwise it offers the same three airing choices, showing the stored first when the tiebreaker is airing.
- [x] 4.8 Add the grouping control: two joined rectangular buttons, "By status" and "Single list", in a `role="group"` labelled "Grouping", each `aria-pressed`, driving the existing boolean.
- [x] 4.9 Size every control in both groups to `--control-h` and the 6px radius. Confirm nothing in the block uses `.my-list-page__tab` or a 999px radius.

## 5. Wire the page (design D1, D4, D6, D7, D8, D11)

- [x] 5.1 In `MyListPage.tsx`, replace `renderFilterBar()` with `<MyListControls>`, deleting the conditional "show first" select and `AIRING_STATUS_FIRST_OPTIONS`.
- [x] 5.2 Rewrite `handleSortChange` to take a `SortChoice`: set `sort` from it, set `airingStatusFirst` when it is an airing choice, and still drop a tiebreaker equal to the new primary key. Add `handleThenChange(choice | null)`, which does the same for `sortThen`. Comment that one stored first status serves both selects because they can never both be airing.
- [x] 5.3 Have `clearFilters` also reset `airingStatusFirst` to `'finished_airing'`. Leave `isOffDefault` reading the raw stored direction (design D5, Risks).
- [x] 5.4 Move **Recap a period** into `.my-list-page__header`'s trailing slot as a small-button-family rectangle, and remove it from the tabs row. `pickerOpen` and the `RecapPickerOverlay` call are unchanged.
- [x] 5.5 Add `frontend/src/components/RecapScopeChip.tsx` / `.css`:
  - props: label, recap href, `onDismiss`
  - "Recap · {label}", a "View recap" link, and a × button with `aria-label="Dismiss recap scope"`
  - one line tall, accent tint, smaller than `--control-h`
  - fed by the existing `recapScopeLabel()` / `recapSearchFromScope()` / `dismissRecapScope()`

  Delete the banner markup.
- [x] 5.6 Render the results line whenever the list is not loading:
  - the chip first (when scoped; it may render during the scoped fetch)
  - then the count in a `role="status"` element: "Showing **N** of **M**" when `isNarrowed`, "**M** anime" otherwise, with numbers `--text-h` weight 600 and no count while `loading || (hasRecapScope && recapScopeLoading)`
  - then **Reset filters & sort** at the far end, only while `isOffDefault`

  Give the line a `min-height` equal to its tallest child, so the reset action or chip appearing doesn't change its height.
- [x] 5.7 Turn `renderBody()`'s two empty states into framed blocks:
  - **Nothing matches:** "Nothing matches these filters" / "Try removing a filter, or reset them all." plus the Reset button.
  - **Nothing here yet:** "Nothing here yet" / the status selection via `statusFiltersLabel`, suffixed "in this recap period" when scoped, with no action.
- [x] 5.8 Update the component's header comment, which describes the page as "a filter bar … and a two-level sort", to describe the five bands.

## 6. Stylesheet cleanup

- [x] 6.1 In `MyListPage.css`, delete the rules the new structure no longer uses:
  - `__filter-bar`, `__filter-cluster`, `__order-cluster`
  - `__query` and `__sort` (moved to `MyListControls.css`)
  - `__recap-button` and its comment
  - `__recap-scope`, `__recap-scope-link`, `__recap-scope-dismiss`
  - `__count`

  Add the header action, `__results`, and the framed empty-state rules. Keep the tab and status-colour rules.
- [x] 6.2 Check both the light and dark palettes, and the `forced-colors` block in `index.css`. The active marks and the segmented control's pressed state must stay distinguishable; each control's label or tick still carries its state.

## 7. Build and walk the change

- [x] 7.1 Run `npm run build` in `frontend` under Node 22 via nvm (the default Node is v16), and fix anything surfaced.
- [x] 7.2 Run `npm run lint`.
- [x] 7.3 Walk the layout at ≥1280px, ~900px and ~400px:
  - two labelled groups on separate rows, each wrapping only within itself
  - labels above their rows at the narrowest width
  - no horizontal scroll
  - Recap a period in the header, status tabs alone in their row
- [x] 7.4 Check that nothing moves:
  - switching the sort key through every choice, including the three airing ones, moves neither the direction button nor the tiebreaker
  - toggling Started and typing into Find (clear button included) shifts no neighbour
  - opening and closing Type/Airing reflows nothing, and the panel lies over the Sort row as a layer
  - the reset action appearing leaves the first row where it was
- [x] 7.5 Walk the sort behaviour:
  - every key's direction label, natural and reversed
  - "Currently airing first" gives Currently → Not yet → Finished, with unknown last, including after a reversal on another key
  - the direction button is disabled while airing is primary, and a My score → Lowest first → airing → My score detour comes back as Lowest first
  - the tiebreaker's airing choice orders ties as named, and the tiebreaker offers no airing choice while airing is primary
  - picking a primary equal to the tiebreaker clears the tiebreaker
  - rank numbers still show only on Single list with a non-alphabetical key
- [x] 7.6 Walk the results line and empty states:
  - unfiltered reads "N anime", narrowed reads "Showing N of M", and a screen reader announces changes
  - **None** on Type reaches the nothing-matches block with Reset
  - an empty status tab shows the nothing-here-yet block
  - each Filter control shows the active accent exactly while it narrows
- [x] 7.7 Walk the recap flows:
  - open the picker from the header and confirm: the list scopes in place and the chip names the period
  - "View recap" round-trips to the same period and type
  - dismissing restores the full list with every control untouched
  - arrive from a recap's "See all", its **Movies watched** stat, and a score-8 distribution row: each seeds its controls (marked active) and matches the followed number
  - Reset within a scope keeps the scope
- [x] 7.8 Check back/forward restoration: set every control (including a tiebreaker airing choice and Single list), open an anime, and go back; everything should be restored. Then reach My list from the navbar and confirm every control is on its default.
- [x] 7.9 Run `openspec validate redo-my-list-filtering-and-sorting --strict` and fix any delta-format issue.
