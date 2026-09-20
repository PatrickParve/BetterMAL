## 1. One place that names every value of a kind

- [x] 1.1 In `frontend/src/utils/anime.ts`, export `MEDIA_TYPE_FILTER_OPTIONS` — every entry of `MEDIA_TYPE_ORDER` as `{ value, label: mediaTypeLabel(value) }` followed by `{ value: 'unknown', label: 'Unknown' }` — as the complete universe of options a Type filter can ever offer. Document that it exists to reserve a trigger's width before any data has loaded (design D7), which is why `Unknown` is unconditional here while the *offered* lists still add it only when an untyped entry exists.
- [x] 1.2 Beside it, export `AIRING_FILTER_OPTIONS` — `finished_airing`, `currently_airing`, `not_yet_aired` from `AIRING_STATUS_LABELS` in that order, then `{ value: 'unknown', label: 'Unknown' }` — the same universe for an Airing filter.
- [x] 1.3 Export a `mediaTypeFilterOptions(values: Iterable<string | null>)` helper building the present-types option list (canonical order, `Unknown` appended when a null is seen), and point `SeasonPage.tsx`, `YearPage.tsx` and `SearchPage.tsx` at it so the four copies of that loop become one. Leave each page's memo and its dependency array as they are — only the body changes.

## 2. The shared multi-select holds its size and knows when it has no choice to offer

- [x] 2.1 Add an optional `widthOptions?: FilterMultiSelectOption[]` prop to `FilterMultiSelect`, defaulting to `options`, and document it as width reservation only: it never appears in the panel, never affects `emit`'s All-normalisation, and never affects which values pass the filter.
- [x] 2.2 Rebuild `summaryCandidates()` from `widthOptions` **unioned with the current `options`** — `All`, `None`, every label from both, and `N selected` for `2..N - 1` where `N` is the larger of the two lengths — deduped. Comment why the union is needed rather than the universe alone: `mediaTypeLabel` prettifies any raw value MAL adds that the app has not mapped, so an offered label can sit outside the universe and would otherwise be clipped (design D7).
- [x] 2.3 Derive `unavailable` inside the component as `selected === null && options.length <= 1`, and give the trigger `disabled={unavailable}` so it cannot open. Document both halves of the condition: one option means every expressible selection is the view already on screen, and `selected === null` is what keeps a narrowing filter available so its restriction can be lifted (design D5).
- [x] 2.4 When `unavailable`, make `summary()` read `${label}: ${options[0].label}` for a single option and `${label}: All` for none, leaving the four ordinary states untouched. Keep the summary inside the existing `StableLabel` so the disabled label is width-reserved like every other.
- [x] 2.5 Do not render `filter-multi-select--active` on an unavailable trigger — by 2.3 it is only unavailable when `selected === null`, which is already the neutral case; add the assertion as a comment rather than a second condition.
- [x] 2.6 In `FilterMultiSelect.css`, add `text-align: left` to `.filter-multi-select__button` (design D9) and a `:disabled` rule in the app's disabled idiom — copy `.my-list-controls__direction:disabled`'s `opacity: 0.6; cursor: not-allowed;` — plus `:disabled` exclusions on the existing `:hover` / `[aria-expanded='true']` accent rule so an unavailable trigger does not light up under the pointer.
- [x] 2.7 Pass `widthOptions={MEDIA_TYPE_FILTER_OPTIONS}` at the `SeasonPage.tsx`, `YearPage.tsx` and `SearchPage.tsx` call sites. Leave their option-building alone — those three pages' options stay filter-unaware (design D6). ~~Leave their `typeOptions.length > 0` render guards alone~~ — removed; see design.md's D6 revision and tasks 6.22/6.25.

## 3. My list computes its options from what the other controls leave listed

- [x] 3.1 In `MyListPage.tsx`, lift the three filter predicates out of `derived`'s `.filter()` into three named helpers (`passesType`, `passesAiring`, `passesScore`) taking the item and the filter value, so the option pass and the match pass apply exactly the same rule rather than two copies of it.
- [x] 3.2 Split today's `derived` preamble into two memos: `statusScoped` (recap scope → status tabs) and `candidates` (`statusScoped` → the find-in-list text). Keep `statusScoped` addressable in its own right — `derived.total` must stay its length, which is what separates the page's "Nothing here yet" screen from "Nothing matches these filters" (design D8).
- [x] 3.3 Replace the `filterOptions` memo with one keyed on `[candidates, typeFilter, airingFilter, scoreFilter]` that walks `candidates` **once**, evaluating the three predicates per entry and dealing the results out: an entry counts toward the Type options when it passes airing and score, toward the Airing options when it passes type and score, toward the Score options when it passes type and airing, and into `matched` when it passes all three (design D8). Return `{ matched, typeOptions, airingOptions, scoreOptions, scoreDisabledLabel }`.
- [x] 3.4 Build `typeOptions` from the types counted in 3.3, in `MEDIA_TYPE_ORDER` then `Unknown` order, **unioned with `typeFilter`'s own current values** so a selection is always offered and always untickable (design D4). Build `airingOptions` the same way against `AIRING_STATUS_LABELS`' order and `airingFilter`.
- [x] 3.5 Build the score option list per design D2, from the rated/unrated counts and the per-value counts over the score base: a value `v` when `0 < count(v) < base.length`; `rated` and `unrated` when the base holds both kinds; `any` always; plus `scoreFilter`'s own current value whatever the rule says. Keep the presentation order Any, Rated, 10…1, Unrated.
- [x] 3.6 Compute `scoreDisabledLabel` in the same pass: `null` unless the option list is `any` alone, then the shared score when every base entry carries one score, `'Unrated'` when none of them is rated, and `'Any'` when the base is empty. Comment that design D5 proves those three are exhaustive — a mixed base always leaves Rated and Unrated offerable, and two distinct scores always leave a value offerable.
- [x] 3.7 Rewrite `derived` to sort and group `matched` rather than filtering `scopedItems` itself, keeping `total: statusScoped.length`, `shown: matched.length`, the `GROUP_ORDER` grouping and the flat branch byte-for-byte otherwise. Its dependency array drops `query`, `typeFilter`, `airingFilter` and `scoreFilter` and gains `matched` and `statusScoped`.
- [x] 3.8 Confirm by inspection that `viewIdentity` is untouched — it is a string of the nine control *values*, none of which this change writes — so an option list changing does not reset the reveal, and a filter changing still resets it exactly as before.
- [x] 3.9 Verify no code path writes `setTypeFilter`, `setAiringFilter` or `setScoreFilter` in response to the option lists changing. The only writers stay the controls themselves, `clearFilters`, and the `focus` seed (design D4).

## 4. The my-list controls render the narrowed options

- [x] 4.1 In `MyListControls.tsx`, pass `widthOptions={MEDIA_TYPE_FILTER_OPTIONS}` and `widthOptions={AIRING_FILTER_OPTIONS}` to the Type and Airing `FilterMultiSelect`s. Nothing else about those two call sites changes — the disabled behaviour arrives with the component.
- [x] 4.2 Widen `MyListControlsProps.scoreOptions` from `number[]` to the full option list the page now computes (`ScoreFilter` values in presentation order), and add `scoreDisabledLabel: string | null`. Update the comment above it: the control is no longer "the score values at least one entry has" but "the choices that would change the list".
- [x] 4.3 Render the score `<select>`'s `<option>`s from that list rather than from the fixed Any/Rated/1–10/Unrated run, labelling each `Score: Any`, `Score: Rated`, `Score: <n>`, `Score: Unrated`.
- [x] 4.4 When `scoreDisabledLabel` is non-null, mark the select `disabled` and render a single `<option value="any">Score: {scoreDisabledLabel}</option>` so the disabled control names the value rather than the state (design D5). Keep `filters.scoreFilter` as the select's `value` — it is `'any'` in exactly that case.
- [x] 4.5 Add the hidden twin select beside the live one (design D7): the same `.my-list-controls__select` class plus a sizer modifier, holding `Score: Any`, `Score: Rated`, `Score: 10`…`Score: 1`, `Score: Unrated` unconditionally, with `aria-hidden="true"` and `tabIndex={-1}`. Document that it exists to reserve the width of the widest score option *including the browser's own dropdown arrow*, which a `<span>` sizer cannot measure.
- [x] 4.6 In `MyListControls.css`, wrap the pair in a single-area grid (`grid-template-areas: 'value'`, both children on `value`) mirroring `.stable-label`'s technique and comment the parallel; give the sizer `visibility: hidden` and the live select `width: 100%`. Add the `:disabled` rule for the select in the same idiom as 2.6.
- [x] 4.7 Check the disabled score select still carries the neutral (non-accent) treatment: `my-list-controls--active` is applied on `scoreFilter !== 'any'`, and a disabled control is only ever on `'any'` — assert by inspection and leave the condition alone.

## 5. Build and lint

- [x] 5.1 Run `nvm use 22 && npm run build` in `frontend/` (`tsc -b && vite build`) and fix any type error the widened `scoreOptions` shape surfaces at the `MyListPage` → `MyListControls` boundary.
- [x] 5.2 Run `npm run lint` in `frontend/` and clear anything new — in particular an exhaustive-deps warning on the reworked memos.
- [x] 5.3 Re-read the three changed `useMemo` dependency arrays against their bodies by hand: `candidates`, the option/match memo, and `derived`. A missing dependency here is a stale option list, which the type checker cannot catch.

## 6. Manual walkthrough

The frontend has no test runner (`frontend/package.json` has no test script or test dependency, and there is no `*.test.*` under `frontend/src`), so each spec scenario is walked by hand. Run `npm run dev` and work through my list with a list that has at least one music video, one movie, one unrated entry and one still-airing entry.

**Options follow the other controls**

- [x] 6.1 Select **On hold** with nothing on hold being a music video: the Type filter no longer offers Music. Return to **All**: Music is back.
- [x] 6.2 Select Movie in Type while no movie of yours is currently airing: the Airing filter no longer offers Currently airing.
- [x] 6.3 With Movie selected, open the Type filter: every other type present alongside movies under the remaining controls is still there, and **All** is still reachable — a filter does not narrow its own options.
- [x] 6.4 Type text in find-in-list that matches only TV series: the Type filter narrows to TV. Clear the text: the other types return.
- [x] 6.5 Narrow to entries that all carry the same score: the score filter no longer offers that score, nor Rated, nor Unrated.

**Unavailable controls**

- [x] 6.6 Narrow so every listed entry is a TV series with no type restriction set: the Type button reads `Type: TV`, is drawn disabled, and does not open when clicked.
- [x] 6.7 Same for Airing (`Airing: Finished airing`) and Score (`Score: 8`).
- [x] 6.8 Narrow to a set with no rated entries: the score control reads `Score: Unrated` and is disabled.
- [x] 6.9 Press **None** in Type so nothing is listed: the Airing and Score controls are disabled reading `Airing: All` and `Score: Any`, and the Type control — which is narrowing — stays available so **All** can be pressed.
- [x] 6.10 Set Type to Movie, switch to a status tab with no movies: the Type control stays **available**, still reads `Type: Movie`, still shows Movie ticked, and **All** restores the list. Switch back to the original tab: the filter is still on Movie and the movies are back, with nothing reselected.

**Size and alignment**

- [x] 6.11 Hard-reload my list and watch the Filter row while the entries load: Type, Airing and Score are already at their final width and do not grow when the entries arrive.
- [x] 6.12 Change the status tab so the Airing filter offers fewer statuses: no control in the row changes width or moves sideways.
- [x] 6.13 Move the Type filter through All → one type → a count → None: the width never changes, and each label starts at the same x-position rather than being centred.
- [x] 6.14 Check `Airing: Currently airing` and `Score: Unrated` are shown in full, neither clipped nor wrapped, and that the score select's arrow is not sitting on top of its text.
- [x] 6.15 Narrow the viewport until the app's fluid root font size moves, and until the Filter row wraps: the controls still share the cluster height, the reserved widths still fit, and nothing overflows.

**Nothing else moved**

- [x] 6.16 Reset filters & sort restores the default view, and the option lists widen back with it.
- [x] 6.17 The empty states still read correctly: a status tab with no entries at all says "Nothing here yet"; a filter combination that matches nothing (reachable via **None**, find text, or a stale selection from 6.10) says "Nothing matches these filters" and offers the reset.
- [x] 6.18 The results line and the group headers report the same counts as before the change for the same view.
- [x] 6.19 The airing badge on rows still follows the airing filter being on anything other than All, and Plan-to-watch rows still always show it.
- [x] 6.20 Scroll a long filtered list to reveal a second page of rows, change a filter, and confirm the reveal resets to the first batch exactly as before.
- [x] 6.21 Navigate away from my list and back: every filter, the sort, the grouping and the reveal count come back as they were.

**The other three pages**

- [x] 6.22 On the Season and Year pages, the Type filter is at its final width before the listing loads and does not grow when it arrives; its label is left-aligned.
- [x] 6.23 Find (or narrow to) a season whose listing is all one type: its Type filter reads `Type: TV` and is disabled. Its options are otherwise unchanged — they still come from the whole loaded listing, not from the page's other controls.
- [x] 6.24 The Search page's Type filter behaves the same way as the entry list changes with the query.
- [x] 6.25 The Search page's Type filter is also at its final width before results arrive for a query and does not appear only once they load (same fix as 6.22, applied to `SearchPage.tsx` too since it had the identical guard).
