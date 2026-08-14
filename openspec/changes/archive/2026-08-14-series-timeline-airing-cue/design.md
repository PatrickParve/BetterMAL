## Context

`SeriesTimeline.tsx` renders one fixed-size card per main-line entry. A currently-airing card is marked today by `.series-timeline__card--airing`: a 2px `--mal` (blue) box-shadow ring plus a soft `--mal-border` outer glow, pulsing on a 2.4s loop, with a `prefers-reduced-motion` opt-out and a visually-hidden "Currently airing" span because the ring carries no text. `.series-timeline__scroll` was given 20px padding on all sides purely so the glow at its widest pulse point would not be clipped.

Two problems: blue is the page's MAL/broadcast colour (`--mal: var(--status-completed)` in `SeriesPage.css`), and a glowing ring around exactly one card in a row is the visual grammar of *selection*, not of *state*. Nothing on the card says the word "airing" either.

The user picked the treatment from mockups: a green pill in the card's air-range line, plus the card's existing border recoloured green — no glow, no pulse, no other card changes.

## Goals / Non-Goals

**Goals:**
- A currently-airing card says "airing" in words, in green, without anything being drawn over the poster.
- The card-level cue stays scannable down a long row but never reads as selection.
- Card geometry is untouched: same width, same height, same rows, same alignment with neighbours.
- Delete what the ring pulled in with it: keyframes, reduced-motion opt-out, visually-hidden text.

**Non-Goals:**
- Recolouring the broadcast (aired) fill on the card — it stays blue, per the page's colour language.
- Changing the airing cue anywhere else in the app (`AiringProgressBar`, `MyListRow`, home page, anime detail).
- Changing the timeline's scroll container padding or the row's spacing (see decision 5).
- Any backend or DTO work — `SeriesEntryDto.airingStatus` already drives this.

## Decisions

### 1. Green is a page-scoped alias, not a raw status token

Add to `.series-page` in `SeriesPage.css`, alongside the existing `--mal*` / `--mine*` aliases:

```css
--airing: var(--status-watching);
--airing-bg: var(--status-watching-bg);
--airing-border: var(--status-watching-border);
```

`--status-watching` is the app's existing green (`#16a34a` light / `#4ade80` dark) and already ships light and dark values plus `-bg` / `-border` variants, so no new colour has to be invented or tuned for two themes. Aliasing rather than using it raw follows the reasoning already written into that block: on this page green means "on air", not "my Watching status", and the two must be free to diverge later without touching every rule.

*Alternative considered:* a new `:root`-level `--airing` token. Rejected — the blast radius should stay on the series page, exactly as `--mal` / `--mine` do.

### 2. The pill goes in the air-range line and takes over from "ongoing"

`formatAirRange` currently returns `"Apr 2026 – ongoing"` for a currently-airing entry. That branch changes to return the start month/year alone (`"Apr 2026"`), and `TimelineCard` renders the pill after it in the same meta line:

```
Apr 2026 · [Airing]
```

The line is the same fixed-height, `nowrap`, ellipsised meta line that already hosts `.series-timeline__no-date-tag` for undated cards, so an inline-block pill in that slot is a proven shape for this card — no new row, no height change, no realignment. Dropping "ongoing" avoids saying the same thing twice in one line and buys back the width the pill needs.

The line's `title` attribute keeps the long form (`"Apr 2026 · Currently airing"`) so the tooltip stays informative if the text ellipsises on a narrow card.

*Alternatives considered:* a chip in the card footer (the footer already packs status text plus the Edit button, and adding to it would ellipsise the status); a third chip in the score-chip row (chips are `flex: 1 1 0`, so a third would shrink the score chips on airing cards only and break row alignment); a full-width strip above the title (would have to be reserved as blank space on *every* card to keep heights equal — exactly the "don't change the cards" line).

### 3. The card-level cue is the existing border recoloured, not a ring

`.series-timeline__card--airing` becomes `border-color: var(--airing)`. The card already has a 1px border, so this costs zero layout, needs no room outside the card, and cannot be clipped by the scroll container. A box-shadow ring — even a green, static one — keeps the ring shape that reads as selected, which is the complaint being fixed.

Hover/focus is deliberately *not* overridden: `.series-timeline__card:hover, :focus-within { border-color: var(--accent-border) }` will recolour an airing card's border like any other card's. That was worth preserving when the ring was the only cue; now the pill is the cue and is always visible, so keeping hover feedback uniform across all cards is the better trade. The spec's hover scenario is reworded accordingly (the *indicator* survives hover, the border may not).

### 4. Pill styling mirrors the no-date tag

`.series-timeline__airing-tag` copies `.series-timeline__no-date-tag`'s geometry (inline-block, `padding: 0 4px`, `border-radius: 4px`, `font-size: 10px`, `line-height: 1.5`) and swaps in green: `color: var(--airing)`, `background: var(--airing-bg)`, `border: 1px solid var(--airing-border)`. Same size and baseline as the tag it sits beside on other cards, and `--airing-bg`'s low alpha keeps it readable on both themes. The word is capitalised as `Airing` — the pill states the state, and the parent line is not a sentence.

### 5. The scroll container's 20px padding stays

Its comment claims the padding exists to keep the glow from being clipped, which stops being true here. The padding still does real work — it separates the row from the section heading and stops the first and last card butting against the container edge mid-scroll — so it stays, and the comment is rewritten to say what actually justifies it now. Shrinking it would shift the timeline's vertical rhythm and its horizontal alignment against every other section on the page, which is a layout change nobody asked for.

### 6. The visually-hidden span and its class are deleted

`{airing && <span className="series-timeline__sr-only">Currently airing</span>}` existed only because the ring was graphics-only. The pill is real text in the reading order, so keeping the span would make a screen reader announce airing twice. `.series-timeline__sr-only` has no other user in the file (verified) and goes with it. The `@keyframes series-timeline-airing-pulse` block and the `prefers-reduced-motion` rule that suppressed it are likewise deleted — nothing animates any more, so there is nothing left to suppress.

## Risks / Trade-offs

- **A 1px green border is a weaker glance-level cue than a glowing ring** → accepted deliberately: the pill carries the meaning in words, and the border only has to make the card findable while scanning, not shout. Green against `--border` grey is a clear hue break at any card size.
- **`Apr 2026 · Airing` could ellipsise on a 168px card** → the pill is ~44px and the start date ~52px at 10–11px type, well inside the ~152px content width; the longest realistic case still fits, and the line's `title` attribute preserves the full text if it ever does not.
- **Green here is also the Watching-status colour elsewhere in the app** (`SeriesEntryRow`'s left stripe, list status pills) → a timeline card carries no status colour at all (existing requirement), so within the timeline green is unambiguous; the alias in decision 1 keeps the escape hatch if the two ever need to differ.
- **`formatAirRange` no longer returns a self-contained string for airing entries** → the pill is now required to complete that line's meaning. Both live in the same component and the function's comment is updated to say so.
