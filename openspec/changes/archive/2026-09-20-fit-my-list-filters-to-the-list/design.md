## Context

**Where the option lists come from today.** `MyListPage.tsx`'s `filterOptions` memo runs over `scopedItems` — the raw list, narrowed only by an active recap scope — and collects the media types, airing statuses and score values present in it. It is keyed on `[scopedItems]` alone, so nothing the user does to the page can move it. The status tabs, the find-in-list text and the other two filters all narrow the list *after* this memo has already decided what the controls offer.

**How the filters are applied.** One `derived` memo does the rest: `statusScoped` (status tabs) → a text-match filter → three predicates in a single `.filter()` pass — type, airing, score — then a sort and an optional grouping. Two counts fall out of it and are consumed by the empty states: `total` is `statusScoped.length` (**before** the text query, which is why "Nothing here yet" and "Nothing matches these filters" are two different screens), and `shown` is the matched count.

**The trigger's width.** `StableLabel` renders every candidate label into one CSS grid cell — the current one visible, the rest `visibility: hidden` and `aria-hidden` — so the cell is as wide as the widest candidate and the control never resizes as its value changes. `FilterMultiSelect.summaryCandidates()` builds those candidates from `options`: `All`, `None`, each option's own label, and `N selected` for `2..options.length - 1`. That is exactly right for a control whose options are fixed, and exactly wrong for one whose options move — on a cold load `options` is `[]`, so the trigger is drawn at the width of `Type: All` and jumps once the entries arrive. The `page-header-design` requirement says as much in so many words: "the widest label the control could show **for the options it currently offers**".

The score filter is a native `<select>` with no `StableLabel` at all; it is sized by the browser to its own longest `<option>`, today always `Score: Unrated`. It is stable only because its option list is nearly fixed — which is what this change ends.

**Alignment.** `.filter-multi-select__button` is `display: grid` with a single named area and inherits the `<button>` UA default `text-align: center`, so every label is centred inside a box sized for the longest one.

**Four call sites, one component.** `FilterMultiSelect` is used by `MyListPage` (Type and Airing), `SeasonPage`, `YearPage` and `SearchPage` (Type each). The three non-list pages build their `typeOptions` with the same `MEDIA_TYPE_ORDER` + `Unknown` code my list uses, and each renders the control only when `typeOptions.length > 0`.

**No test runner.** `frontend/package.json` has `dev`, `build`, `lint`, `preview` and no test dependency; there is no `*.test.*` under `frontend/src`. Everything below is verified by `tsc -b && vite build`, `oxlint`, and the manual walkthrough in `tasks.md`.

## Goals / Non-Goals

**Goals:**

- A filter never offers a choice that would empty the list, and never offers a choice that would leave the list exactly as it is.
- The three filters read each other: narrowing by one changes what the other two offer, and so does the status tab and the find text.
- A control with nothing left to decide says so at rest — disabled, naming the one value — instead of opening a panel to present a single foregone choice.
- A selection already in force is always visible and always reversible, whatever the rest of the page has since narrowed to.
- Every filter trigger is its final size on the first painted frame and never resizes afterwards — not when entries load, not when a tab changes, not when the option list itself changes.
- Labels start at the same x-position whatever they say.

**Non-Goals:**

- Changing which entries match, how they are ordered, how many are drawn, or either of the two counts the page reports. Only the *offered options* change.
- Making the Season, Year and Search Type filters filter-aware. They take the width and disabled behaviour only (D6, D7).
- Touching the status tabs, the Sort group, the reset action, or the recap scope.
- Writing to any filter's restorable state as a side effect of the list changing (D4).
- Adding a frontend test harness.

## Decisions

### D1 — Options come from the listed entries, and each control's own restriction is left out of its own base

The rule the user asked for is "whatever gets listed, the options follow". Applied literally that is circular: the Type filter's options would come from a list the Type filter has already narrowed, so selecting `TV` would leave `TV` as the only option on offer and no way to widen back.

The standard resolution — and the one taken here — is one base per control, each excluding only that control's own restriction:

| Control | Its options come from entries passing |
| --- | --- |
| Type | recap scope, status tabs, find text, **airing**, **score** |
| Airing | recap scope, status tabs, find text, **type**, **score** |
| Score | recap scope, status tabs, find text, **type**, **airing** |

So selecting **On hold** — a filter no control owns — removes Music from Type, Airing and Score alike; selecting `TV` in Type narrows what Airing and Score offer but not what Type itself offers.

*Alternative considered:* one shared base of the fully-matched entries for all three. Simpler to compute and a one-line change, but it makes every control a ratchet: the moment a value is selected every other value vanishes from its own list, and the only way out is the **All** shortcut. Rejected outright.

### D2 — The rule is two-sided: an option must remove something and leave something

Today's rule is one-sided — "only the media types actually present in my list … so the control never offers a choice that would return nothing". The user's ask adds the other side: an option that changes nothing is as useless as one that returns nothing.

**An option is offered when choosing it would both remove at least one listed entry and leave at least one behind.**

For the two multi-selects this collapses to something simpler than it sounds. The values present in a control's base are exactly the choices that leave something; and if the base holds more than one distinct value then every one of them also removes something, while if it holds exactly one then none of them does. So:

- **Multi-select options** = the distinct values present in the base, in the canonical display order (`MEDIA_TYPE_ORDER` then `Unknown`; `finished_airing`, `currently_airing`, `not_yet_aired` then `Unknown`) — unchanged from today except for which entries the base holds.
- **The control** is useful iff that list has **more than one** entry (D5 handles the rest).

For the score control, which is a single-select, the rule bites per option, because each option is a complete choice rather than one tick among several. With `rated` and `unrated` counts over its base:

- a score value `v` is offered iff `0 < count(v) < base.length` — present, and not everything;
- **Rated** and **Unrated** are each offered iff the base is mixed (`rated > 0 && unrated > 0`) — if nothing is unrated, Rated is a no-op; if nothing is rated, Rated is empty; and symmetrically. The two therefore always appear and disappear together;
- **Any** is the neutral option and is always offered.

### D3 — The find text counts as one of the other controls

The three bases in D1 include the find-in-list text. Typing `frier` can therefore disable the Type filter, and clearing it brings the options back. This is what "whatever gets listed" means, and the text field is a filter in the same Filter group as the other three.

The cost is that the option lists are recomputed per keystroke. That is already true of the matched set, the sort and the grouping — the debounce was deliberately removed in `bound-my-list-render` — and D8 folds the option work into the same pass rather than adding one, so per keystroke this adds three boolean tests per candidate entry and nothing else.

*Alternative considered:* exclude the query so the options only move on a tab or filter change. It is less surprising while typing, but it means the Type button can offer Music against a text query that has already excluded every music entry — the exact defect being fixed, reintroduced in the one place the user is most likely to see it.

### D4 — A control always offers its current value, and no filter is ever rewritten

D1's bases exclude a control's own restriction, which usually keeps a selection visible on its own. It does not always: select **Movie**, then switch to a status tab that holds no movies, and `Movie` is absent from the Type base. The trigger would read `Type: Movie` while the panel offered no `Movie` box to untick.

So each control's option list is the **union of its base's values and its own current selection**, ordered canonically — an option carried in only because it is selected renders ticked, which reads as "your choice", not as an offered dead end. The score select needs this for a second, harder reason: a native `<select>` whose `value` matches no `<option>` renders blank.

The alternative is to prune the stored filter to the values that still exist. Rejected on two grounds. It writes `useRestorableState` as a side effect of data arriving, which can fire during a load or a background settle and silently rewrite state the user set. And it is lossy in a way the user will notice: flipping to **On hold** and back would return them to a list they never widened themselves. **Nothing in this change writes to `typeFilter`, `airingFilter` or `scoreFilter`.**

### D5 — One option is no choice: disable the control and let its label state the value

When a control's option list (D2, widened by D4) has **one** entry, every selection it can express is the list it is already showing, so the control is disabled: the trigger does not open, and its label names the value rather than the state — `Type: TV`, not `Type: All`. With **zero** entries — the base is empty, so the list is empty — it is disabled reading `Type: All`, since there is no value to name.

Two guards on that:

- **A control that is itself narrowing the list is never disabled.** Disabling is conditioned on the control being at its neutral value (`selected === null`, `scoreFilter === 'any'`). Without this, a stale selection plus a narrow base is a trap with no way back to **All**.
- **Disabled is not unavailable.** The Airing filter's requirement that it be offered under every status tab still holds: it is always present, in its place, at its size — a disabled control is still an offered control, and its label is now carrying information the enabled one could not.

For the score control the disabled label is derived the same way, and by D2 there are exactly three shapes it can take: every listed entry shares one score (`Score: 8`), every listed entry is unrated (`Score: Unrated`), or the list is empty (`Score: Any`). Nothing else can reach the disabled state — a mix of rated and unrated always leaves Rated and Unrated on offer, and two distinct scores always leave both of them offerable.

### D6 — The disabled rule lives in the shared component, not in the page

`FilterMultiSelect` derives `disabled` itself from `selected === null && options.length <= 1`, rather than taking it as a prop from `MyListPage`. It has everything the rule needs, and putting it there means the Season, Year and Search Type filters get it too: a season listing with only TV entries shows `Type: TV`, disabled, instead of a button whose panel offers one pre-ticked box. That is the same improvement, and it keeps the rule in `page-header-design` where the shared control's other rules already live.

Those three pages keep building `options` from their own loaded listing (**their options stay filter-unaware**); only my list passes narrowed ones.

**Revision:** the three pages originally kept their pre-existing `typeOptions.length > 0` render guard around the control, on the reasoning that it was orthogonal to this change. It isn't: with zero types loaded, `options` is `[]`, `unavailable` (D5) is already true, and the control would render disabled as `Type: TV`-style but reading `Type: All` at its full `widthOptions` width — exactly the first-painted-frame state the Goals section requires. Guarding the control's existence on `typeOptions.length > 0` instead hid it entirely until the listing loaded, so it popped into the row rather than starting there. The guard was removed on all three pages (task 6.22/6.25); each page's *option-building* stays exactly as before.

### D7 — Width is reserved against everything the control could ever offer

The width candidates move from `options` to a new optional `widthOptions` prop: the **universe** of values the control can ever hold, independent of the data. `utils/anime.ts` exports it once for each kind — every media type plus `Unknown`, every airing status plus `Unknown` — so my list's two controls, the other three pages' Type filters and the option-building code all name the same list. `widthOptions` defaults to `options`, so a caller that has no universe to give keeps today's behaviour.

Candidates are then built from `widthOptions` exactly as they are built from `options` today (`All`, `None`, each label, `N selected` for `2..N-1`) — **union the current `options`' labels**, which matters because `mediaTypeLabel` prettifies any raw value MAL adds that the app has not mapped yet (`tv_special_2` → `Tv special 2`). Such a value is in `options` but not in the universe, and a candidate list without it would clip the label. The union keeps the clip impossible; the width can still move the day MAL invents a type longer than every mapped one, which is rare, one-way, and much better than moving on every filter change.

The score control cannot use `StableLabel` — it is a native `<select>`, and a hidden `<span>` cannot account for the browser's own dropdown arrow without a magic number. It gets the same technique through a **hidden twin select**: a second `<select>` in the same single-area grid cell, holding every option the control could ever show (`Score: Any`, `Score: Rated`, `Score: 10`…`Score: 1`, `Score: Unrated`), marked `aria-hidden` and `tabIndex={-1}` and hidden with `visibility: hidden` so it takes layout space but is neither read nor focusable. The cell is sized by the widest real select the browser can draw, arrow included, with nothing estimated.

*Alternatives considered:* (a) a padded `<span>` sizer with a hand-tuned arrow allowance — a magic number that is wrong on the next browser or font-size change, and clips when it is too small; (b) keeping every score option in the DOM and marking the dead ones `disabled` — free width, but greyed-out entries are still a list of choices the user is told they cannot make, which is not what was asked for; (c) replacing the select with a custom single-select button — the most consistent end state, but it rebuilds a working control, and the user asked for a size fix, not a new control.

### D8 — One pass produces the three option lists and the matched set

The three bases in D1 differ only in which of the three predicates they skip, and the matched set is the intersection of all three. Rather than four passes over the candidates, each entry is tested once against each predicate and the three booleans are dealt out:

```
for each candidate:
  t = passesType, a = passesAiring, s = passesScore
  if (a && s) -> count it toward the Type options
  if (t && s) -> count it toward the Airing options
  if (t && a) -> count it toward the Score options
  if (t && a && s) -> it is matched
```

The page therefore keeps two derivations where it has two today: `candidates` (recap scope → status tabs → find text), and one memo producing `{ matched, typeOptions, airingOptions, scoreOptions }` from it; sorting and grouping stay in `derived`, reading `matched`.

One thing must not move in the shuffle: **`derived.total` stays `statusScoped.length`** — the status-scoped count *before* the text query — because that is what separates "Nothing here yet" from "Nothing matches these filters". `candidates` includes the query, so the status-scoped array has to stay addressable in its own right rather than being folded into it.

The reveal budget is unaffected: `viewIdentity` is a string of the nine *control values*, none of which this change writes, so a change to an option list does not reset the reveal and a filter change resets it exactly as before.

### D9 — Labels read from the left

`text-align: left` on `.filter-multi-select__button`, which the `StableLabel` candidates inherit through the grid cell they stretch across. Native selects are already left-aligned, so the score control needs nothing. The Sort group's direction button (`.my-list-controls__direction`, also a `StableLabel`) is deliberately left centred: it was not part of the request, its candidates are fixed rather than data-driven, and it does not sit in the Filter row.

## Risks / Trade-offs

- **A filter disappearing mid-interaction feels like a bug** → It only ever happens as a direct result of the user's own last action (a tab, a keystroke, another filter), the control stays in place at the same size, and its label states the reason (`Type: TV`). Nothing shifts sideways, which is what made the old jumping behaviour read as breakage.
- **A selection can name a value no listed entry has** (D4: switch to a tab with no movies while Type is on Movie) → The trigger keeps the accent, the panel keeps a ticked `Movie` box, and **All** is one press away. The list correctly reports that nothing matches. Auto-pruning would hide the cause rather than show it.
- **Per-keystroke recomputation of four things instead of one** → D8 makes it one pass with three extra boolean tests per entry, over a personal list of a few hundred entries that is already filtered by status and text. The render is still capped by `PAGE_SIZE`, which is what bounds the frame.
- **The width universe can go stale** if MAL adds a media type that is longer than every mapped label and the app has not mapped it → the union in D7 keeps it from clipping; only the constant-width promise weakens, and mapping the new type in `MEDIA_TYPE_LABELS` restores it.
- **The hidden twin select is dead weight in the DOM** (one extra `<select>` with 13 `<option>`s, on one page) → it is the only way to reserve a native control's width exactly; it is `aria-hidden` and unfocusable, so it costs nothing to a screen reader or the tab order.
- **`disabled` derived inside the shared component changes three other pages** → deliberate (D6), and in each the previous behaviour was a control whose only choices were the list it was already showing, or **None**.

## Open Questions

None. Both judgement calls were put to the user and confirmed as designed:

- **D3** — the find-in-list text counts as one of the other controls, so typing narrows the option lists like any other filter. If it turns out to be distracting in use, removing `query` from the `candidates` memo's inputs is a one-line reversal that leaves everything else in place.
- **D6** — the Season, Year and Search Type filters take the shared component's new unavailable state along with its width and alignment, rather than being held back on today's behaviour. Their options stay filter-unaware, as those pages have no second filter to react to.
