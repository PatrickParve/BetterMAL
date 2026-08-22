## 1. The shared height

- [x] 1.1 Declare `--navbar-control-h: 40px` in `components/Navbar/Navbar.css` (on `.navbar`, so it is scoped to the navbar rather than added to the app-wide token block), with a comment stating what it clears: the tallest line box the fluid root font produces is 18px × 145% = 26.1px, plus the controls' 1px borders.
- [x] 1.2 Re-express `.navbar__link` in terms of it — `height: var(--navbar-control-h)`, `box-sizing: border-box`, `display: inline-flex`, `align-items: center`, and horizontal padding only — so its height no longer comes from vertical padding against the inherited line-height. This covers the six left links and Profile, which already share the class.
- [x] 1.3 Give `.navbar__settings` the same height (and an equal width, keeping it a square icon box), replacing its fixed `32px` pair.
- [x] 1.4 Give the navbar's search field the same height via `.navbar__links--right .search-bar` — the selector that already carries its navbar-specific `flex` basis — leaving `SearchBar.css` untouched so the Settings page's field keeps its current size.
- [x] 1.5 Leave `.navbar__score-switch` and every `.navbar__score-switch-*` rule exactly as they are, including its `height: var(--control-h)`.

## 2. The active-page border

- [x] 2.1 Add `border-color: var(--accent)` to `.navbar__link--active`, and confirm the rule sits after `.navbar__link:hover` in the stylesheet so hovering an active link cannot override its stronger border at equal specificity.
- [x] 2.2 Add a `.navbar__settings--active` rule with the same background and border as an active link.
- [x] 2.3 In `Navbar.tsx`, swap the Settings `NavLink`'s fixed `className` string for an active-aware callback producing `navbar__settings` / `navbar__settings navbar__settings--active`, mirroring the existing `linkClassName` helper. Leave its `to`, `aria-label`, and the `GearIcon` unchanged.

## 3. Verification

- [x] 3.1 Check the navbar at a narrow window (root font 16px) and a wide one (root font 18px): the six links, Profile, the gear, and the search field are the same height at both, with nothing clipped.
- [x] 3.2 Visit each page in turn and confirm its navbar control — including the gear on the settings page — shows the active background and border, and that hovering it keeps the stronger border.
- [x] 3.3 Confirm the score toggle is visually and behaviourally identical to before: same pill, same knob travel, same eye open/closed transition, no active treatment.
- [x] 3.4 Confirm the Settings page's own search field is unchanged, and that the navbar's type-ahead dropdown still anchors beneath its field.
- [x] 3.5 Check the narrow-width wrap (`max-width: 900px`), where the search field takes its own row, still lays out correctly with the new heights.
- [x] 3.6 Build the frontend (`nvm use 22 && npm run build` in `frontend/`) and run `npm run lint`.
- [x] 3.7 Run `openspec validate align-navbar-control-heights --strict`.
