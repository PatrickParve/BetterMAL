## 1. Colour tokens and the band treatment (index.css)

- [x] 1.1 Move the `--fam-*` accent defaults onto `:root` (`--fam-from`, `--fam-to`, `--fam-bg`, `--fam-border`, `--fam-ink`), with a comment recording why they cannot live on the consuming elements (design decision 2), and delete the now-redundant `:where(.recap-page__tab)` block in `RecapPage.css` and `:where(.profile-media-tabs__tab)` block in `ProfilePage.css`.
- [x] 1.2 Replace the five `--family-*-ink` tokens with one `--fam-ink` per theme — `#fff` on `:root`, `#09090b` in the dark-theme block — and comment the contract it depends on: every family's fill is dark in the light theme and light in the dark theme (design decision 4).
- [x] 1.3 Make `.family--mal` / `.family--mine` mix `--fam-to` toward black in the light theme and toward white in the dark theme, so neither ramp crosses the middle of the lightness range (design decision 5). Leave `--mal` / `--mine` themselves untouched.
- [x] 1.4 Replace `.tinted-title` (and its `@supports (background-clip: text)` block) with `.section-band`: block-level, `box-sizing: border-box`, `padding: 8px 14px`, `border-radius: 8px`, `text-align: center`, `background-image: linear-gradient(135deg, var(--fam-from), var(--fam-to))`, `color: var(--fam-ink)`; no font-size or weight of its own.
- [x] 1.5 Rewrite the `@media (forced-colors: active)` block for `.section-band`: no background image, `color: CanvasText`, `border: 1px solid CanvasText`, so the band degrades to a plain bordered header.

## 2. Family moves from the title to the section

- [x] 2.1 `RankingSection.tsx`: apply `family--${family}` to the `<section className="recap-page__section">` instead of the `<h2>`, and give the `<h2>` `className="section-band"` when a family is present.
- [x] 2.2 `RankingSection.tsx`: widen the `onSeeAll` payload to `{ title, rows, family }` so the overlay can carry the ranking's family.
- [x] 2.3 `RankingOverlay.tsx`: accept an optional `family` prop, apply `family--${family}` to `.ranking-overlay`, and band `.ranking-overlay__title` with `section-band` when it is set.
- [x] 2.4 `RankingOverlay.css`: swap `.ranking-overlay__link:hover, :focus-within`'s `--accent-bg` / `--accent-border` for `--fam-bg` / `--fam-border`, and drop the `.ranking-overlay__title { margin: 0 }` override only if the band's own rules already zero it.
- [x] 2.5 `RankingSection.css`: swap `.recap-ranking-row__link:hover, :focus-within` to `--fam-bg` / `--fam-border`, and remove the `:where(.recap-page__section) h2 { color: var(--text-h) }` fallback comment block's now-stale reasoning about gradient text (the plain-heading default itself stays, for untinted titles).

## 3. Profile page

- [x] 3.1 `ProfilePage.tsx`: move `family--mal` / `family--mine` from the divergence `<h2>`s onto their `<section className="profile-box">` elements, and give each `<h2>` `className="section-band"`.
- [x] 3.2 `ProfilePage.tsx`: put `family--year` / `family--season` on the two favourites `<section className="profile-box">` wrappers (the `RankingSection` inside keeps its `family` prop for its own band).
- [x] 3.3 `ProfilePage.tsx`: widen the `rankingOverlay` state to carry `family` and pass it to `<RankingOverlay>`.
- [x] 3.4 `ProfilePage.css`: swap `.profile-list-row:hover, :focus-within` to `--fam-bg` / `--fam-border`, and confirm the activity feed and top-anime strip (same class, no family ancestor) still resolve to the accent.
- [x] 3.5 `ProfilePage.css`: add `.profile-box > .section-band` spacing check — the box is a flex column with `gap: 12px`, so the band needs no margin of its own; remove any `.profile-box h2` rule that would fight the band's padding.

## 4. Recap page

- [x] 4.1 `RecapPage.tsx`: move `family--hot` off the hot-takes `<h2>` onto its `<section className="recap-page__section">`, and band the `<h2>` with `section-band`.
- [x] 4.2 `RecapPage.tsx`: give each hot-take `<li className="recap-hot-take">` `family--mal` or `family--mine` from `take.direction`, matching the direction pill it already renders (design decision 7).
- [x] 4.3 `RecapPage.tsx`: widen the `overlay` state to carry `family`, pass it through from all four `RankingSection` call sites, and hand it to `<RankingOverlay>`.
- [x] 4.4 `RecapPage.css`: swap `.recap-hot-take:hover, :focus-within` to `--fam-bg` / `--fam-border`.
- [x] 4.5 Verify the untinted titles (`Top N`, `Stats`, `Rating distribution`) and the rows beneath them are visually unchanged, including the two that sit inside `.recap-page__section-header` flex rows.

## 5. Series timeline card

- [x] 5.1 `SeriesTimeline.css`: `--card-w: 192px`, landscape `--card-w: 384px`, and update both hard-coded `168px` picture-height figures to `192px` so a landscape card's picture area still matches the portrait height.
- [x] 5.2 `SeriesTimeline.tsx`: move the `series-rewatch-badge` out of `.series-timeline__card-status` and make it a sibling in `.series-timeline__card-footer`.
- [x] 5.3 `SeriesTimeline.css`: give the footer's status `flex: 0 1 auto; min-width: 0` and the badge `flex: 0 0 auto`, so the status text truncates before the badge does.
- [x] 5.4 Same hoist in `SeriesExtraTile.tsx` / `.css` (badge out of the truncating status span, `flex: 0 0 auto`), with no width change to the tile.
- [x] 5.5 Check a long main line (One Piece: four-digit watched/total) shows the rewatch badge whole, and that portrait cards, landscape cards, chips, and footers still line up across a row.

## 6. Settings page

- [x] 6.1 `SettingsPage.tsx`: add the presentational components `SettingsGroup`, `SettingsToggleRow`, `SettingsAction`, and `JobProgress` (design decision 10). No hook, handler, poll, or endpoint call changes.
- [x] 6.2 `SettingsPage.tsx`: regroup the existing sections into **Preferences** (score reveal, hide NSFW), **Sync** (status figures, resync now, run reconciliation, the pending diff when one exists), **Data tools** (corrective re-sync, airing refresh, build all series, force-refresh picker), **Account** (MAL connection + re-authorize), in that order.
- [x] 6.3 `SettingsPage.tsx`: route all three background jobs' state through `JobProgress` — running shows a proportional bar plus processed-of-total counts; finished keeps its final counts; failed is marked as a failure with how far it got; never-run shows nothing.
- [x] 6.4 `SettingsPage.css`: style the group shell, the toggle row, the action block, and the progress meter; raise `max-width` to 760px; give the failed state the danger colour; keep every control keyboard-reachable with a visible focus ring.
- [x] 6.5 `SettingsPage.css`: check narrow widths — no horizontal overflow, controls wrap beneath their labels rather than shrinking out of reach.
- [x] 6.6 Walk the regrouped page control by control against the pre-change page: every preference, status figure, button, and the diff review actions are present and still call the same endpoint.

## 7. Verification

- [x] 7.1 Build the frontend with Node 22 (`nvm use 22 && npm run build` in `frontend/`) and fix any type errors from the widened overlay payload.
- [x] 7.2 Check every banded title in the light theme and the dark theme: label legible on the fill, band spanning its list's full width, bands of equal height side by side on a multi-year recap.
- [x] 7.3 Check row highlights: season/year rankings in their families, divergence lists blue/purple, hot-take rows blue/purple by direction under a red band, "See all" overlays matching the ranking that opened them, and every non-family list still on the accent.
- [x] 7.4 Check the selected **Season** / **Yearly** recap tabs read as the same colour as the bands they produce, and that the dark theme's season and hot tabs are now legible.
- [x] 7.5 Run `openspec validate banded-section-titles-and-settings-restyle` and confirm the four delta specs and the new `settings-page` spec still pass.
