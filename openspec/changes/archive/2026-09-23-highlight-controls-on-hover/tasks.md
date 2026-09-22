## 1. App-wide rules in `index.css`

- [x] 1.1 Before editing, run `git diff frontend/src/index.css` and confirm the only pending hunk is `keep-navbar-within-reach`'s `html { scroll-padding-top }` rule. Leave that hunk untouched (design D9).
- [x] 1.2 In `:root`, directly after the `--accent*` tokens, add `--accent-hover: color-mix(in srgb, var(--accent) 85%, black);`. Add a comment saying it is the one filled-button hover fill (design D5), and that it doesn't need repeating in the dark block because it resolves against whichever `--accent` `:root` holds.
- [x] 1.3 Next to the pointer-cursor rule at the bottom of the file, add `select:not(:disabled):hover { color: var(--text-h); border-color: var(--accent-border); }`. Give it a comment in the same shape as the cursor rule's: expressed once so a dropdown added later carries it, why it is (0,2,1), which component rules it beats and which beat it, and why text fields are deliberately not included (design D1).

## 2. Outlined controls that aren't dropdowns

- [x] 2.1 `SeasonPage.css` and `YearPage.css`: add `.season-page__checkbox:hover` / `.year-page__checkbox:hover` with `color: var(--text-h); border-color: var(--accent-border)`.
- [x] 2.2 `MyListControls.css`: add `.my-list-controls__find:hover { border-color: var(--accent-border); color: var(--text-h); }`. The wrapper is the bordered box and the input inside it is borderless, so the rule goes on the wrapper.
- [x] 2.3 `SettingsPage.css`: add `.settings-refresh-picker__search input:hover:not(:disabled) { border-color: var(--accent-border); }`.
- [x] 2.4 `SeriesTitlePickerOverlay.css`: add `.series-title-picker-overlay__field input:hover:not(:disabled) { border-color: var(--accent-border); }`.
- [x] 2.5 `AnimeRankOverlay.css`: add the outlined hover to `.anime-rank__header-actions button` and `.anime-rank__score-tab:not(.anime-rank__score-tab--active)`. Also add a hover to `.anime-rank__retry` in its own red: `background: rgba(229, 72, 77, 0.1)`, matching `.date-field__clear:hover` and `.entry-editor__delete:hover`. Gate all three with `:not(:disabled)`.
- [x] 2.6 `CompletionScoreOverlay.css`: this overlay's three buttons mirror the entry editor's, so they take the entry editor's hovers.
  - Add `.completion-score__buttons button:hover:not(:disabled)` with `border-color: var(--accent-border)` for Cancel.
  - Add `.completion-score__save-and-rank:hover:not(:disabled)` with `background: var(--accent-bg); border-color: var(--accent)`, the same as `.entry-editor__rank`'s hover. Its resting border is already the accent, so the plain outlined hover would change nothing.
  - The filled `.completion-score__save` gets its hover in 6.3.
  - Check that each rule outranks the plain one where the two overlap.
- [x] 2.7 `RelatedAnimeOverlay.css`: add `.related-anime-overlay__buttons button:hover:not(:disabled)` with `border-color: var(--accent-border)`.

## 3. Settings page action buttons

- [x] 3.1 In `SettingsPage.css`, widen the three `.settings-box__buttons button` rules (base, `:hover:not(:disabled)`, `:disabled`) into selector lists that also match `.settings-action__control button` (design D7).
- [x] 3.2 Update the comment above them to say both groups deliberately share one recipe. The accept/decline pairs and the job actions are the same kind of button.
- [x] 3.3 Confirm by reading `SettingsPage.tsx` that every `button={…}` passed to `SettingsAction` is a plain `<button>` with no class of its own, so the new selector reaches all of them: Resync now, Run full reconciliation, airing full refresh, series bulk build, list backup, export, and choose import file.

## 4. My list's grouping switch

- [x] 4.1 In `MyListControls.css`, add `.my-list-controls__segmented:hover { border-color: var(--accent-border); }`.
- [x] 4.2 Replace `.my-list-controls__segmented-button:hover` with `.my-list-controls__segmented-button:not(.my-list-controls__segmented-button--selected):hover { background: var(--code-bg); color: var(--text-h); }`. Comment why the tint is neutral and not the accent: the accent tint is what marks the selected segment (design D4).
- [x] 4.3 Confirm the internal divider (`border-left` on the second segment) is unaffected, and that hovering the selected segment leaves it looking selected.

## 5. "On" controls thicken their outline

- [x] 5.1 Add a `:hover:not(:disabled)` rule setting `border-color: var(--accent)` and `box-shadow: inset 0 0 0 1px var(--accent)` to each of these (design D3):
  - `.my-list-controls--active` (MyListControls.css)
  - `.filter-multi-select__button.filter-multi-select--active` (FilterMultiSelect.css)
  - `.profile-box__control--active` (ProfilePage.css)
  - `.series-page__type-filter-button--active` and `.series-page__toggle-mine--active` (SeriesPage.css)
- [x] 5.2 For the status-coloured toggles, use `var(--tab-color, var(--accent))` for both the border and the ring: `.series-browser-page__filter-button--active` (SeriesBrowserPage.css) and `.my-list-page__tab--active` (MyListPage.css).
- [x] 5.3 Check the specificity of each rule against the component's existing `:hover` and `--active` rules. The new rule must win over both. Use a compound selector where the class alone would lose on source order, as `.my-list-page__clear-filters.my-list-page__clear-filters--reset` already does.
- [x] 5.4 Leave the chosen-option states listed in design D3 untouched.
- [x] 5.5 Add one comment at the first site (MyListControls.css) stating the rule, and point the other sites back to it rather than repeating it.

## 6. Filled action buttons darken

- [x] 6.1 `MyListPage.css`: change `.my-list-page__clear-filters.my-list-page__clear-filters--reset:hover` to `:hover:not(:disabled)` with `background: var(--accent-hover); border-color: var(--accent-hover); color: #fff`. Rewrite the "A filled control doesn't change colour on hover" comment to state the new rule (design D5).
- [x] 6.2 `EntryEditorOverlay.css`: set `.entry-editor__save:hover:not(:disabled)` to `var(--accent-hover)`. Set `.entry-editor__delete-confirm:hover:not(:disabled)` to `color-mix(in srgb, #e5484d 85%, black)`. Rewrite the "Solid buttons (Save, Delete-confirm) keep their own colour on hover" comment to match.
- [x] 6.3 `CompletionScoreOverlay.css`: add `.completion-score__save:hover:not(:disabled)` with `var(--accent-hover)` for background and border. Make sure it outranks the outlined rule from 2.6.
- [x] 6.4 `AnimeRankOverlay.css`: replace `.anime-rank__promote:hover { filter: brightness(1.1); }` with the `--accent-hover` fill, so the app has one filled-hover idiom.
- [x] 6.5 Leave `.top-anime-page__selector-button--active` and `.anime-rank__score-tab--active` as they are. These are chosen options, not actions. Leave `.increment-button`'s scale hover as well.

## 7. Disabled controls take no hover

- [x] 7.1 Gate these hover rules with `:not(:disabled)` (design D6):
  - `.airing-page__nav button:hover` (AiringPage.css)
  - `.pagination__arrow:hover, .pagination__page:hover` (Pagination.css)
  - `.top-anime-row__action:hover` (TopAnimePage.css)
  - `.anime-detail-page__action:hover` and `.anime-detail-page__refresh-notice-retry:hover` (AnimeDetailPage.css)
  - `.series-page__stats-action:hover` (SeriesPage.css)
- [x] 7.2 Audit: list every `:hover` selector under `frontend/src` (`grep -rn ':hover' --include='*.css' frontend/src`). For each one, find the element it styles in the TSX. If that element can render `disabled`, the hover must be gated or explicitly reset. Fix anything the list in 7.1 missed, and note in this task what was found.
  - **Found and fixed two gaps beyond the 7.1 list:**
    - `.my-list-row__score-select:hover` and `.my-list-row__score-select.my-list-row__score-select--mine:hover` (MyListPage.css) style a `<select disabled={scorePending}>` (MyListRow.tsx:84) but had no gate — the plain variant was a real visible bug (hover set `color`/`background` to values that differ from resting, so a disabled select still highlighted). Both now `:hover:not(:disabled)`.
    - `.recap-picker-overlay__tab--active:hover` (RecapPickerOverlay.css) styles a `<button disabled={watchedDisabled|airedDisabled}>` (RecapPickerOverlay.tsx:225,235) but had no gate. Its hover declarations were identical to its resting `--active` declarations, so there was no visible bug, but it's now gated to `:hover:not(:disabled)` for consistency with the rest of the app's gated idiom and with the equivalent, already-gated `.recap-page__tab--active:hover:not(:disabled)` on the Recap page itself.
  - Every other `:hover` selector in the audit either targets an element that can never carry `disabled` (a link, row, card, div, or span — `:disabled` only matches button/input/select/textarea/fieldset/optgroup/option) or was already gated.
- [x] 7.3 In the same audit, confirm every hover rule this change adds (sections 1–6) is gated. Confirmed: every rule added in sections 1–6 already includes `:not(:disabled)` (the two find/segmented-wrapper div rules in section 2/4 don't need it since `:disabled` never matches a `<div>`, but it's harmless there regardless).

## 8. Profile box title gap

- [x] 8.1 In `ProfilePage.css`, change `.profile-box__header-row` to `align-items: center`, and add `.profile-box__header-row > .profile-box__control { margin-block: -1em; }` (design D8).
- [x] 8.2 Rewrite the row's comment. It should say the control is centred on the title's line and given negative block margins so it adds no height to the row, which keeps title-to-content at the box's own 12px gap and keeps the title flush to the top. Include the arithmetic: the title's line box is at least 23.6px and the control's margin box is at most 14.1px.
- [x] 8.3 In the browser, measure the gap from the bottom of each title to the top of the next row in "My top anime" (with **Rank** shown), "Top series", "Most rewatched" and "Latest updates", at a wide window and at the narrowest supported width. All four must be equal.
  - Measured via `getBoundingClientRect()` at 1400px and 380px viewports against the running dev server (real account data): all four gaps are exactly 12.00px at both widths. Control margin-box height measured 40.09px (wide) / 37.20px (narrow), matching design D8's predicted 37.2–40.1px range, always under the title's smallest line box.
- [x] 8.4 Confirm "Latest updates" still shows exactly five whole rows reaching the bottom of its box, and that the top row's boxes still stretch to a shared height.
  - Screenshot shows exactly five rows reaching the box bottom (unchanged `.activity-feed` sizing, untouched by this change). "Anime stats", "Rating distribution" and "Latest updates" all measured 455.4375px tall — a shared height.

## 9. Recap year floor

- [x] 9.1 In `pages/RecapPage.tsx`, delete `const EARLIEST_YEAR = 1960` and import `EARLIEST_YEAR` from `../utils/browseRange.ts`. Rewrite the comment above it to say the recap shares the Season and Year pages' floor (design D9).
- [x] 9.2 Update the `yearOptions` comment that names "1960-current window".
- [x] 9.3 Confirm by reading that `renderPeriodControls`' `years`, `lowYear` and the stepper `disabled` checks all read `EARLIEST_YEAR` and nothing else hard-codes 1960 in the recap (`grep -n 1960 frontend/src/pages/RecapPage.tsx` returns nothing).

## 10. Build and lint

- [x] 10.1 Run `nvm use 22 && npm run build` in `frontend/` (`tsc -b && vite build`).
- [x] 10.2 Run `npm run lint` in `frontend/` and clear anything new.
  - `oxlint` reports 29 pre-existing warnings (react-hooks/exhaustive-deps, react/only-export-components) in files this change doesn't touch. Nothing new from `ProfilePage.css` or `RecapPage.tsx`.

## 11. Manual walkthrough

The frontend has no test runner, so each spec scenario is checked by hand in both the light and dark themes. Run the backend and `npm run dev`.

**Dropdowns and outlined controls**

- [x] 11.1 Season page: hover the season, year and sort dropdowns and the **In my list** chip. Each gets an accent border and heading-colour text, the same as the Type filter.
- [x] 11.2 Year page: the same for its year and sort dropdowns and its **In my list** chip.
- [x] 11.3 Recap page: hover the from/to year dropdowns (multi-year), the year dropdown (yearly), the season and year dropdowns (season), and **All types**. All of them highlight.
- [x] 11.4 Series browser sort, Home "Followed shows airing" sort, and Search page sort: all highlight, and they look the same as each other.
- [x] 11.5 My list: the find field (empty and with text), the score filter, both sort dropdowns, and the direction button. All highlight in the same way.
- [x] 11.6 Settings page: every action button now matches the accept/decline buttons at rest and on hover, and a disabled one (for example Resync now while a sync runs) stays dimmed with no highlight. Hover the refresh picker's search field.
- [x] 11.7 Overlays: in the rank editor, completion-score overlay, related-anime overlay and series title picker, hover each outlined button and text field.
- [x] 11.8 Check that no highlight moves or resizes anything. Watch the neighbouring controls while hovering.

**"On" states, the switch and filled buttons**

- [x] 11.9 Narrow My list by score and hover the score filter: its outline thickens. Do the same for an active Type filter.
- [x] 11.10 Press **Multi-entry only** on the profile and on the Series browser, then hover each: the outline thickens and they still read as pressed. Press a status filter on the Series browser and on My list: it thickens in its status colour.
- [x] 11.11 My list's grouping switch: hover the unselected option. The switch's outline turns accent and the option gets a neutral tint that is clearly different from the selected one. Hover the selected option: it still reads as selected.
- [x] 11.12 Hover **Reset filters & sort**, the entry editor's Save and delete-confirm, the completion Save, and the rank editor's promote. Each fill darkens and the label stays readable in both themes.
- [x] 11.13 Hover an already-chosen tab ("Most rewatched" scope, a Top anime selector, a recap mode tab). It still reads as chosen.

**Disabled controls**

- [x] 11.14 Airing page on the current week: hover **current**. It stays dimmed with no highlight. Step back a week: it highlights on hover.
- [x] 11.15 Top anime page 1: hover the previous arrow and it shows no highlight. Go to the last page: hover the next arrow and it shows no highlight.
- [x] 11.16 Recap stepper at its first and last period, Season/Year steppers at their ends: no highlight when disabled.

**Profile gap and recap floor**

- [x] 11.17 Repeat the gap check from 8.3 by eye, including toggling a tier so **Rank** appears and disappears. The title and tabs don't move.
- [x] 11.18 Recap page, yearly mode: the year dropdown reaches 1917. Step back from 1960 to 1959. At 1917 the previous arrow is disabled.
- [x] 11.19 Season mode: choose winter 1917. The recap renders (empty) and the previous arrow is disabled. Multi-year mode: both dropdowns reach 1917.
