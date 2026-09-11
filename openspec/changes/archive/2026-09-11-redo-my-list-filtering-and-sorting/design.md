## Context

Everything above the list lives in `frontend/src/pages/MyListPage.tsx` (800 lines) and `MyListPage.css`, plus the shared `FilterMultiSelect`. Today's page, top to bottom:

1. `.my-list-page__header`: the `h1` alone, in a flex row that already `justify-content: space-between`s for a trailing slot it never uses.
2. `.my-list-page__tabs`: the All + six status toggles, **and** "Recap a period", which shares their `.my-list-page__tab` pill class and is pushed right by `margin-left: auto` (`MyListPage.tsx:757`).
3. `.my-list-page__recap-scope` (only when scoped): a full-width tinted, bordered strip (`MyListPage.tsx:766-776`).
4. `renderFilterBar()`: one `flex-wrap` bar holding two `flex-wrap` clusters. The ordering cluster is pinned right with `margin-left: auto`.
5. `.my-list-page__count` (only when narrowed), then the body.

Why it reads as a mess, cause by cause:

- **No separation.** The two clusters differ only by a gap. When the bar wraps, the ordering cluster's `margin-left: auto` drops it onto a second line flush right, or splits it, so where each control lands depends on the window width.
- **One shape for everything.** Started, Reverse, and Group-by-status all use `.my-list-page__tab`, the status-tab pill. Three of the bar's controls look like status tabs.
- **Controls that appear.** The "show first" select renders only when `sort === 'airingStatus'` (`:634`), mid-cluster, pushing Then-by, Group, and Clear sideways. Clear filters renders only when off-default (`:669`), at the end of the cluster, where it can add a whole wrapped line. The count line renders only when narrowed (`:779`), so typing the first character into Find pushes the list down.
- **Popover landing.** `.filter-multi-select__panel` is `position: absolute; left: 0; top: 100% + 4px; z-index: 90`. Nothing on the page stacks above 90 (only the modal backdrop at 100, notices at 90–95, and the title tooltip at 1000), so the panel is never *under* anything. The collisions are the panel landing on whichever controls the wrap happened to put beneath it, and a left-anchored panel near the right edge running off-screen at narrow widths.

Two latent sort defects surfaced while reading `utils/anime.ts`:

- `sortComparator` implements reversed Airing status as `-cmp(a, b)`. The comparator ranks unknown as `order.length` (last), so negating it puts unknown-airing entries **first**. That violates "missing values sort last regardless of direction".
- `composeComparator` passes the stored `airingStatusFirst` to the *tiebreaker's* airing comparator too, but the only control for it is shown when airing is the *primary* key. A tiebreaker on Airing status therefore silently uses whatever was last chosen there.

**State.** The page keeps eleven `useRestorableState` values: `statusFilters`, `query`, `typeFilter`, `airingFilter`, `scoreFilter`, `startedFilter`, `sort`, `sortDirection`, `sortThen`, `groupByStatus`, `airingStatusFirst`. The recap handoff seeds four of them from the `focus` token via `parseFocus`. The recap scope itself is URL-only (`recap*` params). `composeComparator` and `sortComparator` have no caller outside this page.

## Goals / Non-Goals

**Goals:**

- Narrowing and ordering read as two labelled groups at every width.
- Nothing in the control rows appears, disappears, or resizes as a result of using another control.
- Airing appears once per role (filter, sort key) and never as a control that pops in.
- The recap entry point reads as a page action, and the scope reads as a small chip.
- Popovers read as layers attached to their triggers and stay on screen.
- The page states what it is showing (count, scope, what's in force) with enough weight to be read.
- Zero change to restorable state, URL vocabulary, or deep-link seeding, so no snapshot or recap link needs migrating.

**Non-Goals:**

- `MyListRow` and anything inside a row: poster, progress, score, airing badge rules, edit button.
- The status tabs' behaviour (multi-select, All exclusivity, standard group order). Only their neighbour changes.
- `RecapPickerOverlay`'s contents and availability rules, and `RecapPage`'s `myListScopeSearch`.
- New filters or new sort keys. Every one of today's is kept; none is added.
- Any backend or API change.

## Decisions

### D1: Five bands, each with one job

```
┌ header ──────────────────────────────────────────────────────────┐
│ My list                                        [ Recap a period ]│
├ status tabs ─────────────────────────────────────────────────────┤
│ (All) (Watching) (Rewatching) (Completed) (Plan to watch) (…)    │
├ controls ────────────────────────────────────────────────────────┤
│ FILTER  [⌕ Find in list…   ×] [Type: All ▾] [Airing: All ▾]      │
│         [Score: Any ▾] [☐ Started]                               │
│ ──────────────────────────────────────────────────────────────── │
│ SORT    [My score ▾] [Highest first] then [— none — ▾]           │
│         [ By status | Single list ]                              │
├ results ─────────────────────────────────────────────────────────┤
│ [Recap · 2022 · What I watched · All types  View recap ×]        │
│ Showing 12 of 48                           Reset filters & sort  │
├ list ────────────────────────────────────────────────────────────┤
```

- The header's unused trailing slot takes Recap a period. `page-header-design` already counts My List among the pages whose header row carries controls.
- The tab row goes back to holding only status tabs.
- The controls block holds the two groups (D2).
- The results line holds everything that *reports* rather than *controls* (D6).

**Rejected alternatives:**

- *A single "Filters (3)" button opening one panel of every narrowing control.* It is the most compact option, but it hides what's in force behind a click, and `library-views` requires the type filter's restriction to be "readable without opening it". It also nests the multi-select popovers inside another popover.
- *A left sidebar.* Rows already need their width for progress, score, and edit columns, and no page in the app uses a sidebar.
- *Keep one bar with a vertical divider.* The divider stops meaning anything the moment the bar wraps, which is the original problem.

### D2: The controls block is a two-column grid of label + wrapping row

`.my-list-controls` is `display: grid; grid-template-columns: max-content minmax(0, 1fr)`:

- Each group is a label cell and a controls cell. The controls cell is a `flex-wrap` row with the cluster gap.
- A hairline between the two groups' rows makes the separation visible even when both rows wrap.
- The block gets a light border and radius so it reads as one surface distinct from the list.
- Labels are small uppercase secondary text (`font-size: 12px`, letter-spacing), vertically centred on the first control line (`line-height: var(--control-h)`).
- Each controls cell is `role="group"` with `aria-labelledby` pointing at its label.
- Below ~560px the grid collapses to one column, so each label sits above its row.

Wrapping is contained by construction: a group's controls can only wrap inside its own cell, and the other group is a different grid row. Nothing uses `margin-left: auto` in the block, so a wrap never reshuffles alignment. The Find field is `flex: 1 1 14rem; max-width: 22rem`, so it absorbs spare width on wide screens and takes a full line on narrow ones rather than leaving ragged gaps.

*Rejected:* two plain flex rows with no label column. The labels are what make the split legible when both rows are the same width and full of similar-looking controls.

### D3: Shape language: pills mean status

Every control in the block is a rectangle at `--control-h` with the 6px radius the selects already use.

- **Started** becomes `<label class="…__check"><input type="checkbox"> Started</label>`, the same shape as the Season page's "In my list" checkbox, which `page-header-design` already names as a cluster control.
- **Grouping** becomes a two-button segmented control (`role="group"` labelled "Grouping", each button `aria-pressed`) joined into one rectangle with a shared border. It keeps the boolean `groupByStatus` underneath.
- **Direction** is a plain rectangular button (D5).

`.my-list-page__tab` is then used only by status tabs and the pill rule stays with them. "Reset filters & sort" and "Recap a period" use the small-button family (`.my-list-page__clear-filters` today): bordered rectangle, `--bg`, hovering to `--accent-border`.

### D4: Airing "show first" folds into the sort key choice

Both sort `<select>`s get an `<optgroup label="Airing status">` with three options whose text is exactly today's conditional select's text: `${AIRING_STATUS_LABELS[s]} first`. A native select shows only the chosen option's own text when closed, never its optgroup label. "Currently airing first" is self-contained; "Currently airing" alone would read as a filter value, and "Airing status" alone would lose the choice.

The option values encode key + first:

```ts
type SortChoice = Exclude<SortKey, 'airingStatus'> | `airingStatus:${AiringStatus}`
toSortChoice(key: SortKey, first: AiringStatus): SortChoice
fromSortChoice(choice: SortChoice): { key: SortKey; first: AiringStatus | null }
```

Choosing an airing option in either select writes `sort` (or `sortThen`) = `'airingStatus'` **and** `airingStatusFirst`.

- One stored `airingStatusFirst` is enough because the primary and the tiebreaker can never both be Airing status. `handleSortChange` already drops a tiebreaker equal to the new primary, and the tiebreaker omits the whole optgroup while the primary is airing.
- When the tiebreaker is airing, its select shows the stored first. That makes today's hidden-value defect visible and settable.
- The restorable-state keys, their types, and `composeComparator`'s signature are all unchanged.
- The airing *filter* keeps its "Airing: …" trigger in the Filter row. Filter and sort key are now told apart by the labelled group each sits in, and by different wording ("Airing: Currently airing" vs. "Currently airing first").

**Rejected alternatives:**

- *Keep the conditional select but reserve its slot.* That leaves either a visible hole or three airing controls.
- *A custom sort popover with a sub-choice.* That rebuilds a select for one key.
- *Separate `airingFirstPrimary` / `airingFirstThen` state.* That would change the snapshot shape for a conflict that can't occur.

### D5: The direction button names its order; Airing status has none

The Reverse pill becomes a button whose text is the order in force for the current key. The labels come from one table beside the sort options in `MyListControls.tsx`:

| Keys | Natural | Reversed |
|---|---|---|
| My score, MAL score, Progress | Highest first | Lowest first |
| Episodes watched, Total episodes | Most first | Fewest first |
| Popularity | Most popular first | Least popular first |
| Alphabetical, Type | A–Z | Z–A |
| Start date, Finish date | Newest first | Oldest first |

It keeps `aria-pressed` off: the label *is* the state, and a pressed "Lowest first" is meaningless. Its accessible name is "Sort direction: Lowest first", and activating it flips `sortDirection`.

**Width.** The button must not resize as its label changes, or the tiebreaker beside it moves. `FilterMultiSelect` already solves this: stack every candidate label in one grid cell, show the live one, and hide the rest with `visibility: hidden` + `aria-hidden`. That technique is extracted into `components/StableLabel.tsx` (`<StableLabel current candidates />` + a four-line CSS file). Both `FilterMultiSelect` and the direction button then render it. A second copy of the aria-hidden grid trick would be the kind of divergence `page-header-design`'s "one shared definition" rule exists to prevent.

**Airing status.** When Airing status is primary:

- The button is `disabled`, reads "Set by first status" (a reserved candidate like the others), and carries `title` / `aria-description` explaining the order comes from the choice in the sort select.
- The comparator ignores direction for airing: `sortComparator`'s airing branch returns the cycle comparator unchanged. That also fixes the reversed-airing "unknown first" defect at its source, rather than relying on the page never passing `'reversed'`.
- The stored `sortDirection` is left alone, so a detour through airing and back resumes the user's direction. `isOffDefault` keeps reading the raw stored value, so Reset stays offered when a reversed direction is stored.

*Rejected:* keeping reverse for airing. It yields three extra orderings that differ from a first-choice rotation only in the order of the middle two statuses, and it can't be labelled in words. *Rejected:* an icon-only ↑/↓ button. It is compact and stable, but "which way is up" for dates and popularity is exactly the ambiguity words remove.

### D6: Nothing conditional lives in the control rows; the results line absorbs it

After D4 and D5, the only things that come and go are the reset action and the scope chip, and neither is a control that orders or narrows. Both move to the **results line**: `display: flex; flex-wrap: wrap; align-items: center; gap`.

- Order within the line: scope chip, then count, then reset pushed to the far end with `margin-left: auto`. Because reset is the last item, its appearance can't push anything.
- The line gets `min-height` equal to its tallest possible child, so the compact reset button and the chip appearing never change its height on a wide viewport.
- The line is always rendered once `loading` is false, so the entries' vertical position no longer depends on whether a filter is active.
- The count sits in a `role="status"` element (polite), so filtering is announced without stealing focus.
- Its numbers are `--text-h` at weight 600, and the surrounding words stay `--text`. That gives the "secondary text is easy to miss" line real weight without making it a heading.

"Clear filters" is renamed **Reset filters & sort**, because it has always reset sort and grouping too, and the old label hid that. Its behaviour is otherwise identical, plus it resets `airingStatusFirst` to `finished_airing`. That value is invisible unless airing is chosen, and choosing airing always sets it, so this is tidiness, not a behaviour change.

*Rejected:* splitting into "Clear filters" and "Reset sort". It adds a control, and the brief lists one reset capability. Flagged under Open Questions.

### D7: Empty states are framed blocks

`renderBody()`'s two empty states become one small presentational block: a dashed `--border` frame with generous padding, a primary line (`--text-h`, 16px, weight 500), and a secondary line (`--text`):

- **Nothing matches:** "Nothing matches these filters" / "Try removing a filter, or reset them all." plus the Reset button.
- **Nothing here yet:** "Nothing here yet" / a line naming the current status selection (using the existing `statusFiltersLabel`, suffixed "in this recap period" when scoped). No action.

The loading text stays as it is.

### D8: Recap: header action + chip in the results line

**Recap a period** moves to `.my-list-page__header`'s trailing slot as a small-button-family rectangle. It keeps the `pickerOpen` state and the `RecapPickerOverlay` call exactly as they are.

The scope indicator becomes `RecapScopeChip`:

- It is one line tall, with the accent tint and 999px radius. It is a chip, not a tab: it is visibly smaller than `--control-h` and sits outside the tab row.
- It reads "Recap · {period} · {filter} · {type}", followed by a "View recap" link and a × button (`aria-label="Dismiss recap scope"`).
- Its props are the label, the recap href, and `onDismiss`. `recapScopeLabel()` and `recapSearchFromScope()` stay in the page and feed it.
- It renders from the URL, so it is visible while the scoped fetch is still loading. The list shows "Loading…" meanwhile, as today.

**Rejected placements:**

- *Chip in the header next to the title.* It sits far from the list it narrows, and reads as decoration on the title.
- *Chip in the Filter row.* It is set by a different flow and has different reset semantics (Reset must not clear it), so it would be the one Filter-row item Reset ignores.
- *Replace the Recap button with the chip while scoped.* Re-picking would then need a hidden affordance on the chip.

### D9: `FilterMultiSelect` panel: attached, elevated, on-screen

**Alignment.** On open, a `useLayoutEffect` measures the panel's rect. If `rect.right > innerWidth - 8`, it sets an `alignEnd` state that switches the panel to `right: 0; left: auto`. `max-width: calc(100vw - 16px)` bounds it in every case. A `resize` listener, attached only while open, re-evaluates. It is a layout effect so the first painted frame is already aligned, with no visible jump.

**Elevation and attachment.**

- The panel keeps `z-index: 90`: above all page content, which never exceeds 20; below the modal (100) and the action-failure notice (95).
- It keeps `--shadow` and gains a 1px `--accent-border` top edge matching the trigger's open-state border, so the pair reads as one piece.
- Its background stays the opaque `--bg`.
- It is absolutely positioned inside the control's `position: relative` wrapper, so opening reflows nothing.

**Trigger.**

- A CSS chevron (`::after`, border-drawn, same size as the native select arrow) is added inside the button's padding. It sits *outside* the `StableLabel` cell, so the reserved width is unchanged.
- `filter-multi-select--active` (when `selected !== null`) applies `--accent-bg` / `--accent-border` / `--text-h`. That covers both a selection and None.

**Rejected alternatives:**

- *CSS anchor positioning with `position-try-fallbacks`.* It is not available across the engines the app is used in.
- *Portalling the panel to `document.body`.* That breaks `useClickOutside`'s containment and the container-level Escape handler, which both rely on the panel being a DOM descendant, and it buys nothing because nothing stacks above 90 on these pages.

These changes are visible on the Search, Season, and Year pages too. That's intended: it is the shared control, and all four pages have the same off-screen risk.

### D10: Marking the filters in force

Each Filter-group control takes an `--active` modifier from values the page already has:

- `query !== ''`
- `typeFilter !== null`
- `airingFilter !== null`
- `scoreFilter !== 'any'`
- `startedFilter`

The treatment is the one the status tabs and the multi-select trigger use: `--accent-bg` background, `--accent-border` border, `--text-h` text. Only colours change, so size can't. Sort-group controls get no modifier: ordering hides nothing, and marking them would dilute the signal.

### D11: Component split; state stays where it is

- **`components/MyListControls.tsx` / `.css`**: both groups, the sort-option list, the direction-label table, `toSortChoice` / `fromSortChoice`, and the segmented grouping control. It is purely presentational. It takes the eleven values and their setters (grouped into a `filters` object and a `sort` object) plus `typeOptions` / `airingOptions`.
- **`components/RecapScopeChip.tsx` / `.css`**: the chip.
- **`components/StableLabel.tsx` / `.css`**: see D5.
- **`MyListPage.tsx`** keeps every `useRestorableState` call, `parseFocus`, the URL scope logic, `derived`, `isOffDefault`, `clearFilters`, the sort-change handlers, and `renderBody()`. It renders header → tabs → `<MyListControls>` → results line → body.

State stays in the page so the deep-link seeding and the restoration keys are untouched. This is a render refactor, not a state one.

### D12: Spec corrections carried in the delta

`library-views` "Recap a period control on my list" says confirming the picker "SHALL open the recap for that period", with the scenario "the recap for 2022 opens". Since the scope-from-my-list change, the page applies the period in place (`confirmLabel="Apply to list"`), as `list-recaps` "Scoping my list to a period from my list" says. The delta rewrites it to match the code and the other spec.

"My list grouping is an explicit choice" lists the groups as Watching → On hold → Plan to watch → Completed → Dropped, omitting Rewatching. It is corrected to the standard order.

## Risks / Trade-offs

- **[Risk] Always showing the results line costs one line of vertical space on an unfiltered list.** → The full-width scope banner and the conditional count line it replaces cost more when active, and the line stops the list jumping when filtering starts. It is one short line of small text.
- **[Risk] Cutting reversed Airing-status order removes three orderings.** → Every "X first" is still one choice away. The only loss is the relative order of the two non-first statuses, and the cut removes the unknown-first defect.
- **[Risk] "Currently airing first" widens both sort selects by about six characters.** → A native select is sized to its widest option once, so the width is stable. It is still narrower than the Find field it shares the block with.
- **[Risk] The shared-control changes (chevron, accent, edge alignment) land on the Search, Season, and Year pages without those pages being "redesigned".** → Each is an additive, size-neutral improvement to the one shared control. Tasks walk all three pages.
- **[Risk] A native `<optgroup>` renders differently across browsers.** → The option text is self-contained (D4), so nothing depends on the group label being shown.
- **[Trade-off] A disabled direction button while airing is primary is a visible control that does nothing.** → The alternatives were a hole or a control that appears. Disabled with an explanation keeps the row still, and tells the user why.
- **[Risk] A stored `sortDirection: 'reversed'` behind airing keeps Reset offered with nothing visibly reversed.** → Reset clears it, which is what the user would expect of "reset". Using the effective direction instead would hide Reset while a reversal waits to reappear.

## Migration Plan

None. This is frontend only. Every `useRestorableState` key and value shape is unchanged, so snapshots written before the change restore correctly after it. The `recap*` / `focus` URL vocabulary is unchanged, so recap deep links are unaffected. Rolling back is reverting the commit.

## Open Questions

Nothing blocks implementation. Two judgment calls are worth a look in review:

- **One reset or two?** This design keeps one action and renames it to say what it does. Splitting it into "Clear filters" (narrowing only) and resetting sort separately is a cheap follow-up if the combined action proves annoying.
- **Recap a period's label while scoped.** It stays "Recap a period", which re-picks the scope. "Change period" is an alternative worth trying once it's on screen.
