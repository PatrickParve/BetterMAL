## Context

`section-colour-language` shipped as a *text* treatment: `.tinted-title` paints the family's gradient through an `<h2>`'s own letters with `background-clip: text`, and the family class (`family--year`, `family--mal`, …) sits on that `<h2>`. Everything else in the section — the rows, their hover highlight — is drawn in the app's purple accent, the same as every other list in the app. The result is that a family names a heading, not a section.

Three facts about the current code shape this design:

- the `--fam-*` aliases (`--fam-from/-to/-bg/-border/-ink`) already exist in `index.css` and are already read by `.recap-page__tab` and `.profile-media-tabs__tab`, each of which declares its own `:where()` accent defaults locally;
- the family class is on the `<h2>`, so nothing else in the section can see it;
- `RankingSection` takes `family` as a prop and puts the class on its `<h2>`; the "See all" overlay payload (`{ title, rows }`) carries no family at all.

The unrelated parts of this change — the timeline card width and the settings page — share no machinery with the above and are designed independently below.

## Goals / Non-Goals

**Goals:**
- A family is carried by a full-width filled band above the section's content, with the label centred and legible in both themes.
- A family reaches the rows inside its section: the standard hover treatment, drawn in the section's hue.
- My-score-vs-MAL comparisons keep reading blue-for-MAL / purple-for-mine, including per-row on hot takes.
- A main-line timeline card always shows its rewatch indicator.
- The settings page reads as grouped preferences and jobs rather than nine identical boxes.

**Non-Goals:**
- No new colour families, and no change to which sections carry which family.
- No change to the podium medals, score-board tiers, status colours, or `--mal`/`--mine` themselves.
- No change to any list's contents, ordering, filtering, or empty states.
- No backend, endpoint, or data change; the settings page's behaviour is untouched, only its presentation.
- The rating-distribution rows and the top-10 rows keep the accent — their sections carry no family.

## Decisions

### 1. The family class moves from the title to the section element

`family--x` goes on the section container (`.recap-page__section`, `.profile-box`, `.ranking-overlay`), and the title gets its own presentational class. Custom properties inherit, so one class on the section makes the family visible to the title *and* every row inside it — which is the whole point of this change. `RankingSection` keeps its `family` prop and simply applies the class one level up.

*Alternative rejected:* keep the class on the `<h2>` and add a second copy to the list. Two sources of the same fact, and a section could silently end up with a title in one family and rows in another.

### 2. `--fam-*` defaults move to `:root`

`index.css` declares the accent defaults once on `:root`:

```css
:root { --fam-from: var(--accent); --fam-to: color-mix(in srgb, var(--accent) 65%, black);
        --fam-bg: var(--accent-bg); --fam-border: var(--accent-border); --fam-ink: #fff; }
```

and `.family--x` overrides them for its subtree. This deletes the two local `:where(.recap-page__tab)` / `:where(.profile-media-tabs__tab)` default blocks.

This is not cosmetic: a default declared **on the row element itself** (`:where(.profile-list-row) { --fam-bg: … }`) would beat the value *inherited* from the section, no matter how low its specificity — inheritance loses to any declaration matching the element. Defaults therefore have to live above every family class, and `:root` is the only place that is unconditionally above all of them. Rows read `--fam-bg` / `--fam-border` with no local default of their own.

*Alternative rejected:* `var(--fam-bg, var(--accent-bg))` fallbacks at each use site. The fallback only fires when the property is *unset*, which is true here, but it would repeat the accent default in a dozen rules and drift.

### 3. The band: a block `<h2>`, not a wrapper element

`.section-band` (replacing `.tinted-title`) is applied to the same `<h2>` that exists today — no wrapper div, so no page's DOM structure changes:

```css
.section-band {
  display: block;               /* both containers are flex columns → stretches to full width */
  box-sizing: border-box;
  padding: 8px 14px;
  border-radius: 8px;           /* the radius the rows below already use */
  text-align: center;
  background-image: linear-gradient(135deg, var(--fam-from), var(--fam-to));
  color: var(--fam-ink);
}
```

135° matches `.recap-page__tab--active` and the podium cards, so a selected **Season** tab and a season band are the same fill, as the spec's "A selected tab and its band match" scenario requires. The band keeps the `<h2>`'s existing 18px/600, so nothing about the type scale changes; it gains a box, which is exactly the point, and the sections it heads are flex columns with a `gap`, so the band's own padding is the only new vertical space.

`background-clip: text`, `-webkit-text-fill-color`, and the whole `@supports` dance go away — a filled band needs no feature detection. The forced-colours block stays, rewritten: `background-image: none; color: CanvasText; border: 1px solid CanvasText;` so the band degrades to a plain bordered header rather than a fill the platform has flattened.

### 4. One ink per theme, not one per family

Measured against the five families' fills (WCAG contrast against the *centre* of each ramp, where the centred label actually sits, and checked at the lighter end too):

| Theme | Every family's fill | Ink | Worst case measured |
|---|---|---|---|
| Light | dark/saturated (year runs silver → near-black) | `#fff` | 4.8:1 (year, at its lightest end) |
| Dark | light/pastel (year runs silver → near-white) | `#09090b` | 7.4:1 (MAL blue) |

So `--fam-ink` collapses to a single value per theme and the five `--family-*-ink` tokens go away. The year family — the one whose direction flips between themes — is the reason this works rather than the exception to it: in the light theme every family is dark, in the dark theme every family is light.

This also **fixes two existing contrast bugs** in the dark theme, both currently visible on selected tabs: white on the season family's `#facc15` end is 1.5:1, and white on the hot family's `#fb923c` end is 2.3:1. Both become near-black ink at 7:1 or better.

### 5. MAL/mine fills mix toward the theme, not always toward black

`.family--mal` / `.family--mine` currently derive `--fam-to` as `color-mix(in srgb, var(--mal) 65%, black)` in both themes. In the dark theme that runs a *light* `#60a5fa` into a *dark* mix, so the ramp crosses the middle of the lightness range and no single ink can sit on it. The mix direction becomes theme-aware — toward black in the light theme, toward white in the dark one — which is what makes decision 4's single ink hold for these two families as well. `--mal` and `--mine` themselves are untouched; only the family's second stop changes.

### 6. Rows: the same rule, a different hue

Every row rule that reads `--accent-bg` / `--accent-border` in a family-carrying list swaps to `--fam-bg` / `--fam-border`: `.profile-list-row:hover/:focus-within`, `.recap-ranking-row__link:hover/:focus-within`, `.ranking-overlay__link:hover/:focus-within`, `.recap-hot-take:hover/:focus-within`. Tint strength, border, `box-shadow: var(--shadow)`, and transitions are untouched, so a hovered row differs from today's only in hue. Rows in sections with no family inherit `:root`'s accent defaults and render identically to today.

The activity feed and the top-anime strip share `.profile-list-row` with the divergence lists. They keep the accent because their sections carry no family class — the swap is inheritance-driven, not per-component.

### 7. A hot-take row answers in its own direction

The hot-takes section carries `family--hot` for its band; each `<li class="recap-hot-take">` additionally carries `family--mal` or `family--mine` per `take.direction` — the same fact its direction pill already renders. A class on the row wins over the value inherited from the section, so the band stays red while the rows highlight blue or purple, with no extra CSS beyond the class.

*Alternative rejected:* red rows under a red band. Simpler, but it throws away the one thing a hot-take row is actually about — who liked it more — which the user asked to keep in blue and purple.

### 8. The "See all" overlay carries its ranking's family

`RankingSection`'s `onSeeAll` payload grows a `family` field, `RankingOverlay` takes it as a prop, applies `family--x` to `.ranking-overlay`, and bands its own `<h2>`. Both call sites (`ProfilePage`'s `rankingOverlay`, `RecapPage`'s `overlay`) widen their state type by one optional field. Without this, clicking "See all" on a season ranking would drop into a purple-accented overlay, which is exactly the discontinuity the colour language exists to avoid.

### 9. Timeline card: widen, and take the badge out of the truncating box

Two independent changes, both needed:

- `--card-w: 168px → 192px`, landscape `336px → 384px` (the same 2× relation), and the landscape picture's pinned height follows the new portrait figure (`calc(192px * 3 / 2)`). Two hard-coded `168px` in `SeriesTimeline.css` are the only places that need to agree.
- The rewatch badge moves out of `.series-timeline__card-status` (which is `white-space: nowrap; overflow: hidden; text-overflow: ellipsis`) and becomes its own child of the footer, `flex: 0 0 auto`, with the status span `flex: 0 1 auto; min-width: 0`.

Width alone would not fix this: an inline badge inside an ellipsized span is clipped by that span no matter how much room the card has, so a longer title or a bigger font would reintroduce it. Hoisting alone would fix the clipping but leave the footer cramped. Together, the ordinary case fits comfortably and the four-digit case truncates the status text while the badge survives — which is what the spec now requires.

`SeriesExtraTile` has the identical badge-inside-truncating-span structure and the same latent defect; it gets the hoist (no width change), so a rewatch count reads the same way on a tile as on a card.

`SeriesEntryRow`'s component is no longer rendered anywhere — only its `airedFigureLabel` / `watchedFigureLabel` helpers are imported — so it is left alone.

### 10. Settings: three presentational components, no logic change

`SettingsPage.tsx` keeps every hook, handler, poll, and endpoint call exactly as-is. Only the JSX below `if (loading)` is restructured, into:

- `SettingsGroup({ title, hint, children })` — a named group; four instances (Preferences, Sync, Data tools, Account).
- `SettingsToggleRow({ checked, onChange, label, hint })` — the compact preference row.
- `SettingsAction({ title, hint, state, button })` — name, explanation, optional run state, one button.
- `JobProgress({ phase, done, total, noun })` — the shared job readout: a `role="progressbar"` track filled `done/total`, the counts beside it, and a failed variant in the danger colour. Used by all three background jobs, replacing three hand-written `<p className="settings-box__hint">` variants that each word progress slightly differently.

The page's `max-width` goes 640px → 760px. A preference row and an action are visually distinct by construction: the toggle row is one line with its control on the left, an action is a titled block with its button on its own row.

*Alternative rejected:* a sticky section nav or a two-column grid. Nine entries in four groups do not need in-page navigation, and a two-column grid would put a several-minute job beside a checkbox, undoing the ranking the grouping exists to express.

## Risks / Trade-offs

- **[The band is much louder than tinted text]** → It is meant to be, and the loudest families (hot, MAL, mine) are on sections that are genuinely about something. Mitigation if it proves overbearing in review: the fill is one `background-image` declaration; swapping it for `--fam-bg` (the 12–16% tint) plus a `--fam-border` and `color: var(--fam-from)` is a one-rule change with no markup impact.
- **[Rows change hue in five places at once]** → Every swap is `--accent-*` → `--fam-*` with `:root` defaults that resolve to the same accent values, so any list that does not sit under a family class is provably unchanged; the risk is confined to the five sections that carry one.
- **[A future family whose fill is light in the light theme breaks the single ink]** → Decision 4's table is the contract: a family's fill must be dark in the light theme and light in the dark one. Recorded as a comment on the `--fam-ink` declaration so the next family added is checked against it rather than discovering it at review.
- **[Wider cards mean fewer visible at once]** → A main line is scrolled horizontally anyway and 192px is a 14% step; the alternative (leaving the card at 168px and only hoisting the badge) leaves the footer with status, badge, and Edit button in ~176px of content width, which crowds on the very case this fixes.
- **[The settings restructure touches a page with real side effects]** → The change is confined to JSX below the loading guard; no handler, hook, poll interval, or endpoint is edited. Each group is verified against the pre-change page control by control.

## Migration Plan

Pure frontend; ships in one commit and reverts as one. No data migration, no persisted state, no API contract. `.tinted-title` disappears with its only consumers in the same change, so no stylesheet is left referencing it.
