## Context

**How hover is written today.** There is no shared button or control class. Every component styles its own controls and writes its own `:hover` rule, which is why coverage depends on who wrote the component. The app uses three hover idioms:

| Idiom | Declarations | Where |
| --- | --- | --- |
| Outlined | `color: var(--text-h); border-color: var(--accent-border)` | most outlined buttons: `.filter-multi-select__button`, `.my-list-controls__direction`, `.settings-box__buttons button`, `.profile-box__control`, `.series-page__*`, `.top-anime-row__action`, `.airing-page__jump select` (border only) |
| Tinted | the outlined pair plus `background: var(--accent-bg)` | navigation steppers and pagination: `.season-page__nav button`, `.year-page__nav button`, `.recap-page__period-nav button`, `.airing-page__nav button`, `.pagination__*`, plus `.recap-page__board-button`, `.date-field__part`, `.entry-editor__field select/input` |
| Family tint | `--fam-bg` / `--fam-border` | section-coloured tabs and rows (`.recap-page__tab`, `.profile-media-tabs__tab`, profile rows) |

**What has no hover at all.** Every dropdown except the Airing page's jump selects and the entry editor's fields; the Season/Year **In my list** chips; My list's find field; the Settings refresh picker and series title picker text fields; the outlined buttons in `AnimeRankOverlay`, `CompletionScoreOverlay` and `RelatedAnimeOverlay`.

The Settings page's action buttons are a special case. `SettingsAction` renders `button` into `.settings-action__control`, and no rule anywhere targets that. `index.css` only sets their cursor, so they are bare browser buttons. The accept/decline buttons on the same page (`.settings-box__buttons button`) are styled and do highlight.

**Where hover does nothing visible.** There are three cases:

- `.my-list-controls__segmented-button:hover` changes only the text colour, and the text is already close to heading colour.
- Filled action buttons (`.my-list-page__clear-filters--reset`, `.entry-editor__save`, `.entry-editor__delete-confirm`) have hover rules that re-state their resting fill on purpose. The comments say "a filled control doesn't change on hover".
- Every "on" state (`--active`, `--selected`) that is drawn with the same `--accent-border` (or `--tab-color`) its hover uses looks identical at rest and under the pointer.

**Where hover fires on a disabled control.** `.airing-page__nav button:hover` and `.pagination__arrow:hover` have no `:not(:disabled)`. The markup does disable them: `disabled={isCurrentWeek}` in `AiringPage.tsx:129`, and `disabled={currentPage === 1}` / `=== totalPages` on the arrows in `Pagination.tsx`. So the controls are disabled, but the tint still paints over the 0.5 opacity. The same gap exists on `.top-anime-row__action` (`disabled={pendingId === …}`), `.anime-detail-page__action`, `.anime-detail-page__refresh-notice-retry` and `.series-page__stats-action`. Their markup can disable them, but their hover isn't gated. Roughly half of the app's button hover rules are already written as `:hover:not(:disabled)` (`.season-page__nav button`, `.settings-box__buttons button`, `.my-list-controls__direction`, `.filter-multi-select__button`), so that is the house idiom.

**The profile title row.** In `.profile-box`, the gap between children is 12px. "Most rewatched" puts a bare `h2` in the box, so title-to-tabs is 12px. "My top anime", "Top series" and "Latest updates" wrap the `h2` in `.profile-box__header-row` next to a `.profile-box__control`. The row is `display: flex; align-items: flex-start`, so its height is the taller of the two children.

- `h2` line box: `clamp(20px, 16px + 0.625vw, 24px) × 118%`, which is 23.6px to 28.3px.
- `.profile-box__control`: `font: inherit` picks up the root's `145%` line height as an absolute length (1.45 × 16px to 18px = 23.2px to 26.1px). Add 12px of padding and 2px of border and the button is 37.2px to 40.1px tall.

So those three boxes leave about 12px more below their titles than "Most rewatched" does. The row's comment explains why it uses `flex-start`: centring pushed the title down. The title's position was already fixed that way; its gap never was.

**The recap's floor.** `RecapPage.tsx:53` declares its own `const EARLIEST_YEAR = 1960`. `utils/browseRange.ts:9` exports `EARLIEST_YEAR = 1917` for the Season and Year pages, documented as matching the backend's `SeasonCalendar.EarliestArchiveYear`. `RecapController` checks only that parameters are present and has no year bound.

**Stylesheet order.** `main.tsx:4` imports `index.css` before any component, so a component rule wins a specificity tie against an `index.css` rule.

## Goals / Non-Goals

**Goals:**

- Every enabled control visibly responds to the pointer, and the same kind of control responds the same way on every page.
- The dropdown hover lives in one place, so a dropdown added later can't be left without one.
- No disabled control highlights.
- The profile's titled boxes leave one gap under the title, at every window width.
- The recap's year range starts where the Season and Year pages' does, from the same constant.

**Non-Goals:**

- A shared button class or component. This would be a refactor of every surface, and it isn't needed to get consistent behaviour (D7).
- Unifying the outlined and tinted idioms into one (D2).
- Making disabled controls look the same across the app (opacity 0.35/0.5/0.6, cursor `default`/`not-allowed`).
- Hover on the Settings page's preference toggle rows.
- The Airing page's 1960 floor.
- Touching the navbar. It has its own hover requirements, and the in-progress `keep-navbar-within-reach` change is editing it.

## Decisions

### D1: Dropdown hover is one rule in `index.css`; text fields stay per-component

`index.css` gets:

```css
select:not(:disabled):hover {
  color: var(--text-h);
  border-color: var(--accent-border);
}
```

It goes next to the pointer-cursor rule, with a comment that explains it the same way. Its specificity is (0,2,1). I checked it against every dropdown the app renders:

- It **beats** every per-component base rule, all of which are (0,1,0) or (0,1,1): `.season-page__sort`, `.recap-page__period-controls select`, `.recap-picker-overlay__row select`, `.completion-score__field select`, `.my-list-controls__select`, and the rest. So the hover shows everywhere.
- It **loses** to the few component hover rules that are more specific: `.entry-editor__field select:hover:not(:disabled)` at (0,3,1) and `.my-list-row__score-select--mine:hover` at (0,3,0). Those keep their own look.
- It **ties** with `.airing-page__jump select:hover` at (0,2,1). The component loads later and wins, with the same border colour.
- It **overrides** `.date-field__part:hover` and `.my-list-row__score-select:hover`, both (0,2,0), but only for `color` and `border-color`, and with the values those rules already use. Their `background` is untouched. The score select has `border: none`, so the border colour is inert there.

Every `<select>` in the app has an author-set border, so none of them sits in the browser's native appearance. That matters because setting `border-color` on a native-appearance select can switch it to the fallback rendering on hover.

**Text fields are not made global.** The app's text inputs don't share a shape. The navbar search input and My list's find input are borderless inside a bordered wrapper, and checkbox and date inputs use native rendering. A global `input` rule would either do nothing or fight those wrappers. The three bare fields (`.my-list-controls__find` wrapper, `.settings-refresh-picker__search input`, `.series-title-picker-overlay__field input`) get the outlined hover in their own stylesheets.

*Alternative considered:* a rule per dropdown class, like every other hover in the app. Rejected. It is how the current gaps happened, and `index.css` already sets the precedent for app-wide rules with the pointer cursor.

### D2: Two hover idioms stay: outlined for controls, tinted for steppers

The outlined idiom is the standard for every newly covered control. The tinted idiom stays on navigation steppers and pagination, and nothing moves between the two.

The tint is the reason the two can't simply merge. `--accent-bg` is also the fill that marks an **"on" state**: a narrowing filter (`.my-list-controls--active`, `.filter-multi-select--active`) or a pressed toggle. If outlined filter controls tinted on hover, hovering an idle filter would look exactly like that filter being active. Steppers have no "on" state, so their tint can't be confused with anything, and it makes up for their one-glyph label.

Each idiom is consistent within its own kind, which is the consistency the ask is about: the sort dropdown looks the same on every page, and the stepper arrows look the same on every page.

### D3: An "on" control thickens its outline in its own colour

A control already drawn as on, whose click does something, gets on hover:

```css
border-color: <its colour at full strength>;
box-shadow: inset 0 0 0 1px <same colour>;
```

That reads as a 2px outline and adds no layout, the same technique `.search-bar:hover` uses (`SearchBar.css:18`). "Its colour" is `var(--accent)` for accent-drawn controls. For status-coloured toggles it is `var(--tab-color, var(--accent))`, so a pressed Dropped filter thickens in red and doesn't switch to purple.

Sites:

- `.my-list-controls--active`
- `.filter-multi-select__button.filter-multi-select--active`
- `.profile-box__control--active`
- `.series-page__type-filter-button--active`
- `.series-page__toggle-mine--active`
- `.series-browser-page__filter-button--active`
- `.my-list-page__tab--active` (My list's status tabs are `aria-pressed` toggles, `MyListPage.tsx:786`)

A **chosen option in a set where exactly one is chosen** is left alone: `.profile-media-tabs__tab--active`, `.top-anime-page__selector-button--active`, `.recap-page__tab--active`, `.recap-picker-overlay__tab--active`, `.anime-rank__score-tab--active`, `.pagination__page--active`, `.my-list-controls__segmented-button--selected`, and `.ranking-score-filter__button--selected`. Clicking any of these again does nothing, and the spec allows them to stay as they are.

*Alternative considered:* switching the border to `var(--accent)` only, without the inset ring. That works for `--accent-border` controls, but it does nothing for status toggles whose resting border is already a solid status colour. The ring works on both.

### D4: The segmented switch highlights as a group, and picks out the option in a neutral tint

- `.my-list-controls__segmented:hover` sets `border-color: var(--accent-border)`, so the switch reacts like the outlined controls next to it.
- `.my-list-controls__segmented-button:not(.my-list-controls__segmented-button--selected):hover` sets `background: var(--code-bg); color: var(--text-h)`.

`--code-bg` is the neutral tint the app already uses for a hovered list item: `.settings-refresh-picker__dropdown button:hover`, `.my-list-row__score-select:hover`, and the current page number. The accent tint belongs to the selected segment. If a hovered segment used it too, the hovered segment would look chosen.

### D5: Filled action buttons darken through one derived token

`index.css` `:root` gains:

```css
--accent-hover: color-mix(in srgb, var(--accent) 85%, black);
```

It only needs to be declared once, not repeated in the dark block. It sits on `:root`, where `--accent` is itself redefined per theme, so it resolves against whichever accent is in force.

Filled accent buttons take `background` and `border-color` from it on `:hover:not(:disabled)`: `.my-list-page__clear-filters--reset`, `.entry-editor__save`, `.completion-score__save`, `.anime-rank__promote`. The red `.entry-editor__delete-confirm` uses the same mix of its own colour, `color-mix(in srgb, #e5484d 85%, black)`. The comments in `MyListPage.css` and `EntryEditorOverlay.css` that state the "a filled control doesn't change on hover" rule are rewritten to state the new one.

**Darken, not brighten.** In the dark theme `--accent` is already light (`#c084fc`) and carries a white label. Brightening it, as `.anime-rank__promote`'s `filter: brightness(1.1)` does today, would lower the label's contrast. Darkening raises it in both themes. The promote button moves onto the token, so the app has one filled-hover idiom.

**`color-mix` over `filter: brightness()`.** A filter would also dim the white label and create a stacking context. `color-mix` changes only the fill, and the app already uses it for `--fam-to`.

`.increment-button`, the round plus, keeps its scale-up hover. It already responds, and it is an icon control, not a labelled call to action.

### D6: Disabled is excluded by gating each hover rule with `:not(:disabled)`

Each affected rule becomes `…:hover:not(:disabled)`. There is no global override, because CSS can't say "on a disabled hover, look like you do at rest". A global `:disabled:hover` rule would have to restate every component's resting look.

Known sites:

- `.airing-page__nav button`
- `.pagination__arrow`
- `.top-anime-row__action`
- `.anime-detail-page__action`
- `.anime-detail-page__refresh-notice-retry`
- `.series-page__stats-action`

`Pagination.css` pairs the arrow and page hovers in one selector list (`.pagination__arrow:hover, .pagination__page:hover`). Only the arrow half needs the gate, because page numbers are never disabled, but gating both keeps that one rule simple. Every hover rule added by this change (D1, D3, D4, D5, D7) is written gated from the start.

`.series-page__type-filter-button:disabled:hover` already resets itself explicitly and satisfies the requirement. It is left alone. `.anime-detail-page__related-link--disabled` is a class on a disabled control that already has its own reset rule, and is also left alone.

The audit in `tasks.md` is what finds anything missing from the list above. For every `:hover` selector, if the element can render `disabled`, it must be gated.

### D7: Settings action buttons share the existing Settings button rule

The two selector lists merge: `.settings-action__control button, .settings-box__buttons button` for the base, hover and disabled rules. That puts the page's action buttons on the exact recipe its accept/decline buttons already use: 13px, 6px 12px padding, 6px radius, border, `--bg`, and the outlined hover.

This is a visible change from bare browser buttons to the app's outlined buttons, and it's intended. The settings-page spec says nothing about how the buttons look, only what their explanations say.

*Alternative considered:* a new app-wide `.button` class. Rejected for this change. Every surface would have to adopt it for it to mean anything, and that is its own refactor (see Non-Goals).

### D8: The profile control stops contributing height to the title row

```css
.profile-box__header-row { align-items: center; }
.profile-box__header-row > .profile-box__control { margin-block: -1em; }
```

A negative block margin of `1em` (13px at the control's font size) shrinks the control's margin box to 11.2px to 14.1px. That is always under the title's smallest line box (23.6px), so the row's height is the title's own at every width, and title-to-content comes out at exactly the box's 12px gap. The margin is symmetric, so `align-items: center` centres the control's visible box on the title's line.

The control overflows the row by about 6px to 7px above and below. That lands inside the box's 16px top padding and the 12px gap below, leaving at least 5px to the tab row.

The old comment's concern still holds: the title is not pushed down, because the row is now exactly the title's height. The comment is rewritten to say why the control is centred and the margins are negative.

**"Latest updates"** uses the same row, so its feed gains about 12px. `.activity-feed` sizes its five rows from its own container height (`--activity-row-h: calc((100cqh - …) / 5)`), so each row grows by about 2.4px and it still shows exactly five whole rows. If that box is the tallest in the top row instead, the row gets 12px shorter, subject to the feed's `min-height`.

*Alternatives considered:*

- **Absolutely positioning the control.** Rejected. It drops out of the flex row's `gap`, so a long title at a narrow width could run under it.
- **Shrinking the control's padding or line height until it fits under the title.** Rejected. Rank and Multi-entry only would then be a different size from every other outlined button on the page, including the tabs right below them.

### D9: The recap imports the shared floor

`RecapPage.tsx` deletes its local `EARLIEST_YEAR` and imports the one from `utils/browseRange.ts`. `yearOptions()`, `lowYear` and the steppers' `disabled` checks already read that name, so they follow without edits. The comments at `:51` and `:126` that name "1960" are updated.

The yearly and season dropdowns grow from about 67 to about 110 options. That is still one scrollable native list.

**The working tree.** `index.css` currently has uncommitted edits from `keep-navbar-within-reach` (the `html { scroll-padding-top }` rule, around line 296). This change adds a `:root` token near the top of the file and a `select` rule next to the cursor rule at the bottom. Neither touches that region. They can be committed separately with `git add -p`, or after that change lands.

## Risks / Trade-offs

- **[A future component wants a dropdown that does not highlight]** → It would have to override a (0,2,1) rule. That is deliberate friction: the requirement says every dropdown highlights.
- **[`margin-block: -1em` overflow could meet a wrapped tab row at very narrow widths]** → At least 5px of clearance holds at every width the fluid sizes allow, and the control is right-aligned while the tab rows start at the left. The narrow-window check in `tasks.md` covers this.
- **[The "Latest updates" feed rows change height by about 2.4px]** → The whole-rows guarantee is kept by construction (`100cqh`). The row pictures follow `--activity-row-h`, so nothing distorts. The profile walkthrough checks it.
- **[Settings buttons change appearance]** → Intended. They adopt the look of the page's own accept/decline buttons.
- **[Darkened fills in the dark theme are less bright]** → The label contrast improves. The fill stays clearly a filled accent button.
- **[An ungated hover rule is missed by the audit]** → The audit is mechanical: every `:hover` selector is paired with the TSX that renders it. The walkthrough then checks each disabled state the app can reach by hand.

## Migration Plan

CSS and one constant. No data, API or stored state is involved. It deploys with the frontend, and a revert is a plain revert.
