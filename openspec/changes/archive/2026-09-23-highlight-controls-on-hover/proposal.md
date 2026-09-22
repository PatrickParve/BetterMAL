## Why

Across the app, some controls react when the pointer is over them and some don't, and which ones do depends on the page rather than on what kind of control it is. The Type filter on the Season page gets an accent border on hover, but the season, year and sort dropdowns next to it don't. On My list, the sort-direction button reacts but the sort dropdowns, the score filter, the find field and the By status / Single list switch don't. Most of the Settings page's buttons get no styling from the app at all and render as bare browser buttons. The reverse problem shows up too: the Airing page's current-week button and the Top anime page's pagination arrows are disabled at the end of their range, but they still light up on hover as if they could be clicked.

Two smaller issues come with this. On the profile page, "My top anime" and "Top series" leave a bigger gap under their titles than "Most rewatched" does. The recap page's year controls also stop at 1960, even though the Season and Year pages go back to 1917, the start of MyAnimeList's season archive.

## What Changes

**Every control highlights on hover**

- Every dropdown (`<select>`) in the app gets the standard outlined-control hover: an accent border and heading-colour text. This is written once, app-wide, in the same way `index.css` already sets the pointer cursor, so a dropdown added later gets it automatically. That covers the Season page's season/year/sort dropdowns, the Year page's year and sort dropdowns, the Recap page's period dropdowns and **All types** filter, the Series browser's sort, the Home page's "Followed shows airing" sort, My list's score filter and both sort dropdowns, the Search page's sort, and the Recap picker's dropdowns.
- The outlined controls that aren't dropdowns get the same hover in their own stylesheets:
  - the Season and Year pages' **In my list** checkbox chips
  - My list's find field
  - the Settings refresh picker's search field
  - the series title picker's text field
  - the outlined buttons in the rank editor, completion-score and related-anime overlays.
- The Settings page's action buttons (Resync now, Run full reconciliation, airing refresh, series build, list backup, export, choose import file) are styled as the app's outlined buttons, matching the accept/decline buttons that already sit on the same page, and get their hover.
- My list's **By status / Single list** switch: the switch's outline takes the accent border on hover, and the option under the pointer gets a neutral tint. That tint is deliberately different from the accent tint that marks the chosen option, so the two can't be mistaken for each other.
- A control that is already drawn as on gets a thicker outline in its own colour on hover. That means a filter that is narrowing the list, or a pressed toggle such as **Multi-entry only**, the series type and "mine" toggles, or a status filter on My list or the Series browser. These already have a tinted fill and a coloured border at rest, so without this they look exactly the same under the pointer.
- Filled action buttons darken on hover instead of staying unchanged: **Reset filters & sort**, the entry editor's **Save** and delete-confirm, the completion overlay's **Save**, and the rank editor's promote button. This replaces the old rule that "a filled control doesn't change on hover" for buttons that perform an action. A selected tab or segment is still exempt, since clicking it again does nothing.

**A disabled control shows no hover**

- Every hover rule on a control that can be disabled only applies while the control is enabled. This fixes the Airing page's current-week button and the pagination arrows (the Top anime page's). The same audit also catches the other disabled-capable controls whose hover still fires, such as the Top anime row actions, the detail page's actions and retry, and the series page's stats action.

**Profile box titles sit the same distance above their content**

- A title that has a control beside it ("My top anime" with **Rank**, "Top series" with **Multi-entry only**) leaves exactly the same gap below it as a title without one ("Most rewatched"). Right now the control is taller than the title, so it sets the row's height. The fix is to keep the control centred on the title's line so it no longer adds height to the row.
- "Latest updates" (with **Full history**) uses the same title row, so it gets the same fix and lines up with the bare titles of the boxes next to it. Its feed already sizes its rows from whatever height the box gives it, so it still shows exactly five whole rows.

**The recap reaches back to 1917**

- The Recap page's year choices and its steppers start at 1917 instead of 1960. The page uses the `EARLIEST_YEAR` constant the Season and Year pages already share, rather than keeping its own copy.

No **BREAKING** changes. Everything here is CSS plus one frontend constant, and no data, endpoint or stored state changes.

## Capabilities

### New Capabilities

None. Each rule belongs to a capability that already covers its surface.

### Modified Capabilities

- `navigation-and-search`: this capability already holds the page-wide hover and cursor rules ("Clearly visible hover state on navigation controls", "A clickable control shows the pointer cursor").
  - **Added:** "Every interactive control highlights on hover". It extends the navigation-controls rule to every control kind: buttons, dropdowns, text fields, checkbox chips, segmented switches and filled action buttons, on every page and in every overlay. The same kind of control highlights the same way wherever it appears.
  - **Added:** "A disabled control shows no hover highlight". This generalises the rule `main-dashboard` already states for the carousel arrows alone.
  - **Not modified:** "Clearly visible hover state on navigation controls" and "The search field highlights on hover". Both still hold as written. The navbar search field keeps its own, stronger focus-matching highlight.
- `profile-stats`:
  - **Added:** "A profile box title keeps one gap whether or not it carries a control". No existing requirement governs the title row's spacing. The nearest one, "Profile section titles are banded by family", covers banding only.
- `list-recaps`:
  - **Added:** "Recap year choices start at MyAnimeList's first season year". No requirement states the recap's year range today; "Period stepper arrows" only says the steppers stop where the period controls stop, so it follows the new floor without being edited.

## Impact

- **Frontend CSS:**
  - `index.css`: the app-wide dropdown hover rule, plus one derived `--accent-hover` fill token.
  - Per-component hover rules, "on" state hovers or `:not(:disabled)` gates in: `SeasonPage.css`, `YearPage.css`, `MyListControls.css`, `MyListPage.css`, `SettingsPage.css`, `AiringPage.css`, `Pagination.css`, `TopAnimePage.css`, `AnimeDetailPage.css`, `SeriesPage.css`, `SeriesBrowserPage.css`, `FilterMultiSelect.css`, `EntryEditorOverlay.css`, `CompletionScoreOverlay.css`, `AnimeRankOverlay.css`, `RelatedAnimeOverlay.css`, `SeriesTitlePickerOverlay.css`
  - `ProfilePage.css` for the header-row gap and the pressed **Multi-entry only** hover.
- **Frontend TS:** `pages/RecapPage.tsx` imports `EARLIEST_YEAR` from `utils/browseRange.ts` in place of its local `1960`, and its comment that names the old window is updated. No markup changes anywhere. Every fix lands on class names that already exist.
- **Backend:** none. The recap endpoint has no lower year bound (`RecapController` only checks that the parameters are present), so 1917 is accepted as it stands.
- **Working tree:** `index.css` has uncommitted edits from the in-progress `keep-navbar-within-reach` change (the `html { scroll-padding-top }` rule). This change adds rules in a different part of that file. See design D9.
- **The frontend has no test runner**, so this is verified by `tsc -b && vite build`, `oxlint`, and the hover walkthrough in `tasks.md`, as this repo's frontend changes already are.
- **Out of scope:**
  - **The Airing page's 1960 year floor.** The ask was about the recap. The airing schedule's own spec sets 1960 (`airing-schedule` spec, line 51), and a 1917 weekly schedule has nothing to show.
  - **Making disabled controls look the same everywhere.** Opacities range from 0.35 to 0.6 and cursors are either `default` or `not-allowed`. That is a separate consistency pass. This change only guarantees that a disabled control doesn't highlight.
  - **Folding the tinted navigation-stepper hover into the outlined one**, or the reverse (design D2).
  - **The Settings page's preference toggle rows.** These are plain checkbox rows, not bordered controls. The checkbox keeps the browser's own hover.
