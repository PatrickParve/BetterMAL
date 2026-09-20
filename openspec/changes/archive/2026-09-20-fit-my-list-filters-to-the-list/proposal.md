## Why

My list's Type, Airing and Score controls are built from the whole list and then never consulted again as the list is narrowed. Select the **On hold** tab and the Type filter still offers Music, Movie and OVA even though every on-hold entry is a TV series; picking any of them empties the list. The controls advertise choices that cannot pay off, and when a control has only one real answer left it still opens a panel to say so.

They also change size under the user. Each trigger reserves the width of the widest label it can show *for the options it currently holds*, so on a refresh the buttons are drawn narrow against an empty list and then jump wider as the entries arrive — and, once the options start following the filters, they would jump on every filter change too. Their labels are centred in that reserved width, so a short state (`Type: All`) floats in the middle of a box sized for a long one.

## What Changes

**The three my-list filters offer only what would change the list**

- Each of the Type, Airing and Score controls draws its options from the entries that pass *every other* control — the status tabs, the find-in-list text, and the other two filters — rather than from the whole list. Selecting **On hold** when nothing on hold is a music video removes Music from the Type filter; it comes back when the tab does.
- An option is offered only if choosing it would both **remove at least one entry** from what is currently listed and **leave at least one entry** behind. That single rule replaces today's "only types present in the list": it drops the choice that returns nothing (as today) *and* the choice that returns exactly what is already shown. For the score control it also governs **Rated** and **Unrated** — Rated is dropped when nothing in view is unrated (it would change nothing) and when nothing is rated (it would return nothing).
- A control always offers its own current value, even when the rule above would drop it, so a selection can always be undone. A filter left on a value the rest of the list has moved away from is never silently rewritten.
- When a control's only remaining choice is its neutral one — every listed entry has the same type, the same airing status, or the same score — the control is **disabled** and its label states that one value: `Type: TV`, `Airing: Currently airing`, `Score: 8`. It does not open. A control that is itself narrowing the list is never disabled, whatever its options, so **All** / **Any** is always reachable.
- This applies to the shared multi-select wherever it is used — the Season, Year and Search pages' Type filters get the same one-option disabled treatment. Their options keep coming from their own loaded listing; **only my list's become filter-aware.**

**The filter triggers hold one size and read from the left**

- A trigger's reserved width comes from the full set of values the control could **ever** offer, not the set it happens to offer now: every media type for a Type filter, every airing status for an Airing filter, every score option for the Score filter. The buttons are their final size on the first frame, before any entry has loaded, and they do not move as tabs, text or filters change what is on offer.
- The Score control is a native `<select>`, which sizes itself to its own longest option. It gets the same treatment by a different route (see design D7) so that dropping options no longer shrinks it.
- Trigger labels are left-aligned within that reserved width instead of centred, so the label starts in the same place whatever it says. The Sort group's direction button is unchanged.

No **BREAKING** changes: no stored state, URL or API changes shape, and every filter combination expressible today stays expressible.

## Capabilities

### New Capabilities

None. Nothing about what my list *contains*, or about how it is grouped, sorted or drawn, changes.

### Modified Capabilities

- `library-views`:
  - **Modified:** "My list type filter" (`spec.md:289`) — the sentence bounding the offered options ("only the media types actually present in my list … so the control never offers a choice that would return nothing") is widened to the two-sided rule: present *in what the other controls leave listed*, and only when it would actually narrow that. Plus the one-option disabled state and its label.
  - **Modified:** "My list airing-status filter" (`spec.md:320`) — today it states a fixed option list ("offering Finished airing, Currently airing, Not yet aired, and — when the list contains one — unknown"), which the new rule contradicts. Same two-sided rule and same disabled state. The requirement that the filter is available under *every* status tab is kept and reconciled: available is not the same as enabled.
  - **Modified:** "My list score filter" (`spec.md:347`) — today it states a fixed option list ("Any, Rated, Unrated, and one option per score value from 10 down to 1"). Same rule, applied to a single-select: which of Rated/Unrated/1–10 survive, and the disabled state naming the one score or `Unrated`. Its scenario "A score with no entries" (`:378`) is reworked rather than dropped — a score with no entries is no longer selectable, so the case it describes is now reached only through a stale selection, which is exactly what it should still say.
  - **Added:** "My list filters offer only what the listed entries make possible" — the rule itself, stated once for all three controls with the interaction between them (each control's options ignore its own restriction), the sticky-selection guarantee, and the promise that no filter's stored value is rewritten as a side effect of the others changing. Added rather than folded three times into three requirements because it is one rule about how the controls relate to each other, which none of them owns.
  - **Not modified:** "My list marks the filters in force" (`spec.md:268`). The active-accent marking is untouched, and its "SHALL NOT change any control's size" clause is what the width work below serves, not something it changes.
  - **Not modified:** "My list filter bar" (`spec.md:179`), "Find in list" (`:243`). Order, grouping, shared height and the reset action are untouched. Find-in-list gains no behaviour — it simply counts as one of the other controls whose result the three filters now read.
  - **Not modified:** "My list two-level sorting" (`:368`), "My list reveals its rows as the page scrolls" (`:490`), "My list reports what is being shown" (`:550`). Which entries match, how they are ordered, how many are drawn, and the `shown`/`total` counts are all unchanged — this change only decides which options are offered.
- `page-header-design`:
  - **Modified:** "The shared multi-select filter's trigger holds one width" (`spec.md:177`) — its width rule is "the widest label the control could show **for the options it currently offers**", which is exactly what breaks once the options move. Rewritten to reserve against the full set of values the control can ever offer, with the load-time and filter-change cases stated. The accessible-name clause is kept verbatim.
  - **Modified:** "The shared multi-select filter's label names its state" (`spec.md:152`) — gains the left alignment and the label a disabled one-option control shows. The existing four states (All / None / one label / a count) are unchanged.
  - **Added:** "The shared multi-select filter is unavailable when it has nothing to choose between" — the disabled state, what it reads, that it does not open, and that a filter currently narrowing the view is never disabled. Added rather than folded into "shows when it is narrowing" (`:100`) because that requirement is about accent colour on an *enabled* control.
  - **Not modified:** "distinguishes All from None" (`:121`), "the panel reads as a layer attached to its trigger" (`:77`), "the shortcuts read as buttons" (`:204`), "Filter and sort controls in one cluster share one height" (`:48`). A disabled control simply never opens the panel those describe.

## Impact

- **Frontend** (under `frontend/src/`):
  - `pages/MyListPage.tsx`: the `filterOptions` memo is replaced by one pass that computes, per entry, which of the three predicates it passes, and from that both the three option lists and the matched set `derived` already needs — so the extra work is three membership tests per entry per keystroke, not three extra passes.
  - `components/FilterMultiSelect.tsx`: a `widthOptions` prop (the stable universe, defaulting to `options` so the other three pages can opt in one at a time), the self-derived disabled state, and the one-option label.
  - `components/FilterMultiSelect.css`: `text-align: left`, a disabled rule.
  - `components/MyListControls.tsx` / `.css`: the score `<select>`'s narrowed option list, its disabled state and label, and the hidden full-width sizer beside it.
  - `utils/anime.ts`: the media-type and airing-status option *universes* exported once, so the four pages and the width reservation all name the same list.
  - `pages/SeasonPage.tsx`, `pages/YearPage.tsx`, `pages/SearchPage.tsx`: one prop each, passing the media-type universe. Their option-building code is otherwise untouched.
- **Backend, API, database:** no change, no migration. Every option list here is computed from entries the client already holds.
- **Restorable state:** `typeFilter`, `airingFilter` and `scoreFilter` keep their shapes and their `useRestorableState` keys, and **nothing in this change writes to them**. A snapshot taken before this change restores identically after it.
- **The frontend has no test runner.** `frontend/package.json` has no test script and no test dependency; `frontend/src` holds no `*.test.*` file. Verification is `tsc -b && vite build`, `oxlint`, and a manual walkthrough carried item by item in `tasks.md`, as this repo's frontend changes already do (`archive/2026-09-11-redo-my-list-filtering-and-sorting/tasks.md` §7).
- **Out of scope:**
  - **Making the Season, Year and Search Type filters filter-aware.** Those pages have one filter each of this kind plus checkboxes and a sort; there is no second filter for the options to react to. They take the width and disabled changes only.
  - **The status tabs.** They are not narrowed by the three filters and do not disable themselves when a status is empty. Making tabs react would mean deciding what a tab with no entries should look like, which is its own change.
  - **Auto-clearing a filter that no longer matches anything.** Rejected in design D4: it would write restorable state as a side effect of a data load and lose a selection the user will want back when they switch tabs back.
  - **The Sort group.** Sort keys are not narrowed to "keys that would reorder this list", and the direction button's alignment and width are untouched.
