## Context

Three colour vocabularies already exist in `index.css` and are used exactly as their comments describe: the status colours, the podium's `--medal-*` triple, and the score board's `--tier-*` set, on top of the two score roles `--mal`/`--mine` (aliases of `--status-completed`/`--accent`). Two established local patterns come with them — `.recap-podium__card` aliases its rank's medal into `--medal`/`--medal-bg`/`--medal-border` so the badge, band, glow, and wave are each written once against a variable rather than three times against a literal; and `.score--mal`/`.score--mine` in `index.css` are global one-line utility classes that carry a role's colour onto whatever element takes them. This change adds a fourth vocabulary (the section families) and leans on both patterns rather than inventing a third.

The surfaces being recoloured share their CSS with surfaces that must not change. `.recap-page__tab` styles the recap-type tabs, the time filter, **and** the ranking-basis toggle from one six-state block; `.profile-media-tabs__tab` styles the Top series basis tabs **and** the media-type tabs on My top anime and Most rewatched. `ScoreDistribution` renders both pages' distributions and `RankingSection` renders both pages' rankings. Nothing here may be done by editing a shared rule in place.

## Goals / Non-Goals

**Goals:**

- One colour-family vocabulary that a title rule and a control rule both read, so a family's title and its tab cannot drift apart.
- Tinted titles that add colour and nothing else: no box, no size change, no reflow, no state.
- Recolouring by *added modifier*, so every co-tenant of a shared class renders byte-identically.
- The distribution's tier colours read from the score board's own mapping, not a second copy of it.

**Non-Goals:**

- Restyling the profile page's all-anime distribution, the podium, the score board, or any status colour.
- Changing the six states of the recap's segmented controls, their geometry, or their transitions — only which colours fill them.
- Fixing the app's pre-existing white-on-bright-accent contrast in the dark theme (see Risks). The `--fam-ink` token this change introduces is the mechanism that *would* fix it, but it defaults to today's `#fff` so no tab outside the year family changes appearance.
- Any backend, DTO, or data work. Every figure being coloured is already rendered.

## Decisions

### 1. Families are token sets in `index.css`, aliased locally through one `--fam-*` vocabulary

Each new family gets five tokens per theme, named for what they are rather than for the hue so a later retune does not make the name a lie:

```
--family-year-from / -to / -bg / -border / -ink
--family-season-from / -to / -bg / -border / -ink
--family-hot-from / -to / -bg / -border / -ink
```

`-ink` is the label colour printed on top of the family when it *fills* a control. It is `#fff` for every family in every theme but one — the achromatic year family in the dark theme, which fills toward white and therefore takes a dark label (decision 2a). It exists as a token rather than as a one-off override so the rule that prints the label reads a variable in every case and never special-cases a family by name.

Five global palette classes alias whichever family into one local vocabulary, exactly as `.recap-podium__card` does with `--medal*`:

```css
.family--year   { --fam-from: var(--family-year-from); --fam-to: var(--family-year-to);
                  --fam-bg: var(--family-year-bg);     --fam-border: var(--family-year-border);
                  --fam-ink: var(--family-year-ink); }
.family--season { … }
.family--hot    { … }
.family--mal    { --fam-from: var(--mal);  --fam-to: color-mix(in srgb, var(--mal) 65%, black);
                  --fam-bg: var(--mal-bg); --fam-border: var(--mal-border); --fam-ink: #fff; }
.family--mine   { --fam-from: var(--mine); … }
```

The MAL and mine families are aliases, not new colours — the `score-presentation` capability already owns those two, and a second definition of "MAL blue" is exactly the drift the spec forbids. Their second gradient stop is derived with the same `color-mix(… 65%, black)` the active tab already uses for the accent, so a score family needs no new colour at all; only the three genuinely new families carry an explicit second value, because "silver to black" and "orange and yellow" are deliberate pairs rather than shades of one colour.

*Alternative rejected:* overriding `--accent`/`--accent-bg`/`--accent-border` on the element itself. It would make every existing state rule work with zero edits, which is seductive — but it redefines an app-wide token to mean something local, and any rule later added inside a tab that legitimately means "the app's accent" would silently come out silver. A private `--fam-*` vocabulary costs one rewrite of the state rules and cannot mislead.

### 2. Light-theme family colours are picked against white text, which makes them good title colours for free

A family colour is used two ways: as text on the page background, and as the fill under a label on a selected tab. For the three hued families in the light theme both reduce to the same measurement — contrast against white — so one pair satisfies both:

| Family | Light `from` → `to` | Dark `from` → `to` |
| --- | --- | --- |
| year | `#71717a` → `#09090b` (silver → black) | `#a1a1aa` → `#fafafa` (silver → white) |
| season | `#c2410c` → `#a16207` (orange → amber) | `#fb923c` → `#facc15` |
| hot | `#b91c1c` → `#c2410c` (red → orange) | `#f87171` → `#fb923c` |

Every light value clears 4.5:1 against white; every dark value clears 4.5:1 against `--bg` (`#16171d`). The `-bg`/`-border` tokens follow the file's existing convention exactly — `rgba(…, 0.12)` and `rgba(…, 0.45)` in light, `0.16` and `0.5` in dark — built from the `from` colour.

The light-theme season pair is the compromise on the list: a true yellow cannot clear 4.5:1 on white, so "orange and yellow" reads as orange → dark amber in the light theme and as orange → true yellow in the dark one. Legibility wins; the family is still unmistakably the warm one.

The year family is achromatic, which makes it the only family whose *direction* flips between themes: it runs silver → black in the light theme and silver → white in the dark one, always ending at the theme's own extreme. Three consequences follow, and each is a requirement in the spec rather than a nicety:

- **It opens at silver in both themes, never at the extreme.** A ramp starting at near-black in the light theme would be indistinguishable from an ordinary `--text-h` heading over a short title, which is exactly the case the "identifiable from its first colour alone" rule exists for. Starting at silver and travelling to black keeps the travel visible and the opening distinct.
- **The greys are zinc, not slate.** `--medal-silver` is a deliberately cool slate-blue (`#475569` / `#94a3b8`) — the podium's comment explains why. A true-neutral zinc keeps the year family off that hue, so a silver title and a silver medal are tellable apart despite both being "silver".
- **Its filled label cannot be white.** Hence `-ink` (decision 2a).

### 2a. The filled label reads from `--fam-ink`, not from a hard-coded `#fff`

`.recap-page__tab--active` prints `color: #fff` on the accent gradient today. That is safe for every hued family in the light theme and for the year family's dark-in-light fill, and it is fatal for the year family in the dark theme, whose fill is near-white. The active rule therefore reads `color: var(--fam-ink)`, defaulting to `#fff` on `.recap-page__tab` so every tab that carries no family — Multi-year, both time filters — computes exactly what it computes today.

This is the `--on-accent`-shaped token the original design noted would be needed to fix the app's pre-existing white-on-bright-accent contrast in the dark theme. It arrives here scoped to families only, because the year family forces it; widening it to the plain accent tabs is a separate change and deliberately not made here.

*Alternative rejected:* keeping the dark-theme year family dark so `#fff` still works. It would mean a near-black title on a near-black page — illegible as text, which is the family's primary use. The fill is the secondary use, so the fill is what adapts.

### 3. The tinted title is a two-class global utility, painted across the section by construction

`.tinted-title` goes in `index.css` beside `.score--mal`/`.score--mine`, which it is the direct analogue of — a global one-purpose colour utility, used from three files (`RecapPage.tsx`, `ProfilePage.tsx`, `RankingSection.tsx`). A title is `class="tinted-title family--year"`.

The "ramp spans the section, not the text" requirement needs no measuring code: an `<h2>` is a block box that already fills its section's content width — `.recap-page__section` is a block and `.profile-box` is a `column` flex container with the default `stretch` — so the element's own background box *is* the section's width. `background-clip: text` then clips that full-width ramp to the glyphs, and a title occupying the leading third of the section shows the leading third of the ramp. This is why the treatment is text-fill rather than a pill or an underline: those would have needed a box, and a box would have changed the section's layout.

```css
.tinted-title {
  color: var(--fam-from);              /* fallback: a plain legible colour, never transparent */
}

@supports (-webkit-background-clip: text) or (background-clip: text) {
  .tinted-title {
    background-image: linear-gradient(90deg, var(--fam-from), var(--fam-to));
    -webkit-background-clip: text;
    background-clip: text;
    -webkit-text-fill-color: transparent;
  }
}

@media (forced-colors: active) {
  .tinted-title { -webkit-text-fill-color: revert; background-image: none; color: CanvasText; }
}
```

Order matters: `color` is set unconditionally *first*, so a browser without `background-clip: text` — or a forced-colours mode, which discards the gradient but not `text-fill-color` — renders a solid legible title rather than nothing. The rule sets no size, weight, margin, or display, so the `h2` keeps every geometric property it has today and nothing below it moves.

Only `--fam-from`/`--fam-to` are read here; the `-bg`, `-border`, and `-ink` remainder of a family exists for the controls.

*Alternative rejected:* a `background-size` measured from JS to make each title show the *whole* ramp. It would need a resize observer per title for a two-hue difference nobody asked for, and the user explicitly chose "the heading's letters pick up the part of the gradient sitting over them".

### 4. The recap tab's six states are rewritten once against `--fam-*`, defaulting to the accent

`.recap-page__tab` gains the five `--fam-*` defaults (accent, accent-mixed-with-black, accent-bg, accent-border, `#fff` ink) and its six state rules swap their `var(--accent…)` and hard-coded `#fff` references for `var(--fam-…)`. With no palette class the computed colours are byte-identical to today, so the time filter, the Multi-year tab, and every other tab are unchanged. `.family--year`, `.family--season`, `.family--mal`, and `.family--mine` are then added in `RecapPage.tsx` on the four options that carry one.

`My score` gets an explicit `.family--mine` even though `--mine` is currently an alias of `--accent` and the class changes nothing today. The spec says that option carries the mine role; if `--accent` and `--mine` are ever allowed to diverge — which `index.css`'s own comment says is the point of the alias — the explicit class is what keeps that true.

The same treatment applies to `.profile-media-tabs__tab`'s three rules, with `.family--mal`/`.family--mine` added only in the `TOP_SERIES_BASIS_TABS` map. The media-type tabs pass no palette class and keep the accent. One addition on the profile side: that control's active state is a tint rather than a fill, so its label — today `--text-h` — becomes `var(--fam-from)` for a family-carrying tab, since a blue tint alone under near-black text does not read as "blue". Media-type tabs, having no `--fam-from` of their own, are unaffected.

### 5. `scoreTier` moves to `utils/anime.ts`; the distribution opts in with one prop

The spec requires the distribution and the board to read one tier definition. `scoreTier` and its `Tier` type move out of `ScoreBoardOverlay.tsx` into `utils/anime.ts` (the app's single utils module, which already holds presentation mappings such as `STATUS_CLASS` and `mediaTypeLabel`), renamed `ScoreTier` for a name that survives the move. Both components import it; neither restates the 10→1 mapping.

`ScoreDistribution` takes a new optional `tiered?: boolean`. Only `RecapPage` passes it. Left unset — the profile page — the component emits exactly today's markup and classes, so its distribution is untouched, as the spec requires. Set, each row's `__label` and `__bar` additionally carry `score-distribution__row--tier-<tier>`-scoped colours driven by a `--tier` local alias set per row, mirroring the podium card's `--medal` pattern; the 10 row takes the board's apex gradient treatment rather than a flat fill so the two agree there too. Counts and shares are left alone.

*Alternative rejected:* colouring by score number in CSS (`:nth-child`). It would put the 10→1 tier mapping in a second place, which is precisely what the spec's "read, not restated" clause exists to prevent.

### 6. `RankingSection` takes a `family` prop; the two hard-coded titles take the classes directly

`RankingSection` renders the title for all four recap rankings and both profile favourites, so it grows one optional `family?: 'year' | 'season'` prop and renders `<h2 className={family ? \`tinted-title family--${family}\` : undefined}>`. Six call sites pass it; anything that does not renders an untouched `h2`. The recap's "Biggest Hot takes" and the profile's two divergence titles are written inline in their pages and take the two classes directly.

Which family each section gets is decided at the call site rather than derived from the title string — `describeYearRanking` already exists next to `describeSeasonRanking`, and inferring a family from a heading's words would break the first time a heading is reworded.

## Risks / Trade-offs

- **A white label on the dark theme's near-white year fill** → The concrete failure this change could ship: a selected Yearly tab printing today's hard-coded `#fff` over `#a1a1aa → #fafafa`. Mitigated by `--fam-ink` (decision 2a) and checked explicitly in tasks 3.1 and 3.8. It must be verified in the dark theme specifically — the light theme's fill is near-black and hides the bug entirely.
- **White text on a bright family fill in the dark theme, generally** → The app already sets `color: #fff` on `.recap-page__tab--active` over a dark-theme `--accent` of `#c084fc`, which is below 4.5:1 today; the season and hot families inherit that same treatment rather than inventing a different one, so this change neither improves nor worsens it. `--fam-ink` is the `--on-accent`-shaped token that would fix it for every tab at once, deliberately left defaulted to `#fff` here so no unrelated tab changes appearance.
- **`background-clip: text` failing open** → Mitigated by construction: `color` is set before and outside the `@supports` block, and a `forced-colors` query restores a system colour, so the failure mode is a solid-coloured title rather than an invisible one. This is the single highest-consequence detail in the change and is worth verifying deliberately.
- **A short title shows almost none of its ramp** → Accepted by design: the user chose the section-wide ramp knowing a short heading takes its opening slice. Mitigated by requiring each family to be identifiable from its `from` colour alone, which is why the `from` colours (silver, orange, red, blue, purple) are five plainly different openings rather than five variations that only separate at the ramp's end.
- **The achromatic family colliding with something else neutral** → Two neutrals are already in play: `--text-h`, the colour every untinted heading uses, and `--medal-silver`. The family clears the first by opening at silver rather than at the extreme, and the second by being true-neutral zinc rather than the medal's cool slate. Both are spec requirements with their own scenarios, not just design intent, and both are worth eyeballing side by side rather than trusting the hex values.
- **Five families on one page could read as noise** → Bounded by the spec: only sections whose subject a family names are tinted, and Top N / Stats / Rating distribution stay neutral. The recap page shows at most three families at once (season, year, hot take) plus whichever colour its controls carry — and one of those three has no hue at all.
- **A shared rule edited by accident** → The two class-sharing hazards (`.recap-page__tab`, `.profile-media-tabs__tab`) are called out in tasks with an explicit check that the media-type tabs, the time filter, and the Multi-year tab render unchanged.
