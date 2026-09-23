## Context

The Airing page lays a week out as seven equal flex columns (`.airing-page__grid`, `flex: 1 1 0`). Each slot is a row: the time above, then `RowPicture` (`--row-picture-w: 52px; --row-picture-h: 72px`) beside an info column holding the two-line title and the episode pill. `artwork-presentation` lists these slots as row picture slots, so a wide picture takes `width: auto` up to `calc(72px * 16 / 9)` = 128px.

Column width is `(viewport − 122px) / 7`: 24px page padding on each side, the `#root` border, and six 12px gaps. The slot's inner width is 18px less (8px padding on each side and the border). What the title gets beside the picture, with the 10px gap:

| Viewport | Slot inner | Title beside a poster (52px) | Title beside 16:9 art (128px) |
|---|---|---|---|
| 801px | 79px | 17px | none |
| 1024px | 111px | 49px | none |
| 1280px | 147px | 85px | 9px |
| 1440px | 170px | 108px | 32px |
| 1920px | 239px | 177px | 101px |

So the row rule's premise, "whatever follows the picture moves along", fails in this column. There's nowhere for it to move to. The episode pill makes it worse. It's `content-box` (there is no global `border-box`) with `max-width: 100%` and `padding: 1px 7px`, so its border box is 14px wider than the column it is bounded by, and it pokes out past the slot's edge. The table also shows the layout is poor for posters below about 1100px, so this is a slot-layout problem as much as a landscape one.

There are no frontend tests. Verification is a type-check, lint, build, and a look in the browser.

## Goals / Non-Goals

**Goals:**

- The title and episode label never share their width with the picture, at any column width or picture shape.
- Landscape art, the case that breaks today, gets its best presentation rather than a tolerated one.
- Every slot in a week stays the same size, and nothing moves as pictures load.
- Pictures are drawn through `artwork-presentation`'s shared component and never cropped. Nothing is drawn over a picture, and no blurred fill sits behind it.

**Non-Goals:**

- The dashboard's "Airing today" list, which stays a row slot. Its container is wide, and the user didn't raise it.
- Header, navigation, jump selectors, today mark, grouping, ranges and data. None change.
- Any backend or DTO change, or new data such as airing status or a "now" marker.
- Changing how `PosterPicture` behaves for any existing host.

## Decisions

### D1 — The slot becomes a card: header strip, picture band, title

```
┌─────────────────────┐
│ 23:30      ( Ep 12 )│  ← header strip: time left, episode pill right
├─────────────────────┤
│      ▓▓▓▓▓▓▓▓▓      │  ← band: picture whole, on a flat background
│      ▓▓▓▓▓▓▓▓▓      │
├─────────────────────┤
│ Sousou no Frieren   │  ← title: full card width, 2 lines reserved
│ Season 2            │
└─────────────────────┘
```

The strip, the band and the title each span the card's full width. Everything is stacked, so neither the picture nor the text takes width from the other.

Alternatives considered:

- **Keep the row and bound the picture by the room left**, for example `flex-shrink` the picture against a minimum title width. At 1280px a 16:9 picture would shrink to about 40×23 inside a 72px-tall box. That's technically whole, but it's a postage stamp with grey bands, and posters would still crowd the title below about 1100px.
- **Switch layout per shape** (row for posters, stacked for wide). Two slot layouts in one column look inconsistent, and they can't share a height without dead space in one of them, which breaks the "every slot the same height" rule.
- **The picture as the whole card's backdrop, with the title on a scrim.** This is striking, but it puts body text over arbitrary artwork, and a poster still needs somewhere to sit. It trades a width problem for a contrast problem.
- **Time and episode as chips over the band's corners, with a blurred copy of the picture behind it.** This was the first implementation, and it looked wrong on real data. Most pictures are posters, which are narrower than the band, so the chips sat on top of the poster and hid a corner of it, and the blurred backdrop read as clutter rather than polish. Landscape art was the only shape it suited. Moving the chips into their own strip and leaving the band flat fixes both, at the cost of about 40px of card height.

The stacked card is the one layout where a wide picture and a narrow column stop competing, and it also fixes the cramped-poster case.

### D2 — The band is a banner box, drawn through `PosterPicture` in a new opt-in `whole` mode

`PosterPicture` is "the one place a card, tile or poster box draws its picture". It already has the wrapper, the shape classes and the `contain` path. What it does for a poster, `object-fit: cover` with no fill, is right for portrait boxes and wrong for a landscape band, where `cover` would cut about two thirds off a poster's height. For an upright or wide picture it also mounts the blurred fill, which the airing band doesn't want.

It gets an optional `whole` prop:

- The wrapper gains `poster-picture--whole`. `PosterPicture.css` adds `.poster-picture--whole > .poster-picture__art { object-fit: contain; }`, so every shape, poster included, is drawn whole and centred.
- With `whole`, no fill is mounted for any shape. The space around the picture is the wrapper's own `var(--code-bg)`.
- The hook stays `usePictureShape`, and the fill condition for everything else is untouched, so hosts that don't pass `whole` take exactly today's path.

Rejected alternatives:

- **Host-side CSS overrides.** Overriding the art's `object-fit` works, but the fill would still be mounted for a non-poster and only hidden, leaving an extra `<img>` and a blur filter per slot for nothing.
- **A new `BannerPicture` component.** It would duplicate the shape and placeholder logic that `PosterPicture` exists to hold once.
- **Keeping `RowPicture`.** It is height-bound by design, so it is exactly the wrong geometry.

### D3 — Band height is derived from the card's width with container units, clamped to 64–96px

`.airing-day__slots > li { container-type: inline-size; }` and the band is `height: clamp(64px, 56.25cqi, 96px)` (56.25 = 9/16). A 16:9 key visual therefore fills the band exactly across the common range: about 1170px to 1320px viewport, where the band runs from 90px to 96px tall. Above that the band stops at 96px and the leftover sits at the sides. Below it the band stops at 64px, so a very narrow column never gets a sliver-thin band.

Why `cqi` and not the alternatives:

- **`aspect-ratio: 16/9` with `min-height` and `max-height`**: the band is a grid item (D4). In grid, `normal` self-alignment behaves as `start` for items with a preferred aspect ratio, so the band would shrink to fit its content unless `width` is forced, and the clamps interact with the ratio in ways that are easy to get subtly wrong. `cqi` is a plain length.
- **A fixed height**, for example 84px: this works too, but wastes the band at the widths people actually use. 16:9 art would never quite fill it.

Why the container is the `li` and not `.airing-day`: an element can't query its own size, so the container must be an ancestor of the band. In the narrow tiled layout (D5), a card is narrower than its day, and the `li` is the card's width in both layouts. Inline-size containment is safe on it, because its width always comes from its parent (a flex column stretch or a grid track), never from its content. Container queries are already in use (`SeasonPage.css`, `CurrentSeasonSection.css`).

Every `li` in a week has the same width, so every band, and therefore every slot, is the same height. That keeps the airing-schedule "same height" requirement.

### D4 — The card is a small CSS grid; time and episode form a header strip

```css
.airing-slot {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  grid-template-areas:
    "time episode"
    "band band"
    "title title";
}
.airing-slot__time    { grid-area: time; }
.airing-slot__episode { grid-area: episode; justify-self: end; }
.airing-slot__art     { grid-area: band; }
.airing-slot__title   { grid-area: title; }
```

Grid placement keeps DOM order free: the markup stays time → band → title → episode, so the link's accessible name still reads "23:30, title, Ep 12", as today. `minmax(0, 1fr)` stops a long unbreakable title from widening the card.

The strip:

- The time is plain muted text, 12px with tabular numerals, as it was before.
- The episode label is the plain pill it was before: `var(--code-bg)`, 12px, fully rounded. It is now `box-sizing: border-box` with `max-width` set to its column minus its margins, plus `nowrap` and an ellipsis. A percentage max-width on a grid item resolves against the grid area, which is the episode column. This is the structural fix for the pill running off the edge, and the time column being `auto` means the episode label is what gives way.
- Its spacing is three custom properties on `.airing-slot` (`--strip-x`, `--strip-mid`, `--strip-y`), so the pill's `max-width` and its margins can't drift apart.
- At a card width of 120px or less (a `@container` query on the same `li` that D3 makes the container), the strip tightens: 11px text, a smaller pill padding, and smaller gaps. The narrowest seven-column card is about 95px wide, where the default strip would squeeze even `Ep 12` to an ellipsis. Tightened, ordinary labels such as `Ep 12` and `Ep 8-9` fit there, and only a three-digit range is cut short.

The band is flush with the strip above and the title below, so it needs no corner radius and no `overflow: hidden` on the link. That leaves the link's focus outline and hover untouched. The title keeps its 13px size, `line-height: 1.3`, two-line clamp and two-line `min-height`, with `padding: 8px 10px 0` and an 8px bottom margin. The bottom gap is a margin because `overflow: hidden` clips at the padding box, so bottom padding would show the top of a clamped third line.

### D5 — The narrow layout tiles each day's cards with an `auto-fill` grid

Inside the existing `@media (max-width: 800px)`, where `.airing-page__grid` already turns into a column, the following is added:

```css
.airing-day__slots {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
}
```

The same block sets `.airing-page__grid` to `align-items: stretch`. The base rule's `flex-start` is right for seven columns, but once the grid is a column it would shrink each day to its content, and the cards would have no width to tile across.

`auto-fill` rather than `auto-fit` keeps the empty tracks, so a day with one slot doesn't stretch it across the row. Every day's list is the same width, so every day gets identical tracks, and all cards in the week stay the same size. Using the same media query as the column switch keeps one breakpoint for both.

Without this, a card stretches across the whole day at 800px and below, leaving a 700px band with a 68px poster in the middle.

### D6 — Placeholder and loading states

`PosterPicture` without a `src` renders the placeholder `div` with the host class, so `.airing-slot__art` gives it the band's size and `var(--code-bg)`. It carries both the host class and its `--placeholder` modifier, so no separate `--placeholder` rule is needed. Before load, the band is that same empty box. The box never changes size, so the "a list of posters does not shift" guarantee becomes "nothing shifts" here. `loading="lazy"` is passed through, keeping the native deferral the slots had as row slots.

## Risks / Trade-offs

- [Slots get taller: about 113px today, and about 152px at 801px, 185px at 1280px and 188px at 1440px and above] → The header strip costs about 40px against the chips-over-band version. The band is capped at 96px. A busy day scrolls a bit further, in exchange for titles that can be read and pictures that nothing covers.
- [At the narrowest seven-column width (801px) a poster draws at 45×64, smaller than today's 52×72] → The title goes from 17px to about 75px there, which is a far better trade. From about 1000px up, posters draw larger than today.
- [A poster in a wide band leaves flat space at its sides] → It's the band's own flat background, the same box a placeholder gets. It reads calmer than a blurred fill and keeps every band the same box whatever it holds.
- [Long episode labels at the narrowest width] → A three-digit range such as `Ep 112-118` is cut short with an ellipsis at 801px, which the spec allows. The tightened strip keeps ordinary labels whole.
- [`whole` mode leaks into another host] → It is opt-in and defaults off, and the default path is the same code as before. Screenshots of every other `PosterPicture` page were compared pixel for pixel against `HEAD` during apply.
- [Container-query support] → Needs Safari 16 or Chrome 105 and later, which the app already requires elsewhere.

## Migration Plan

Frontend only, with no data or API change. Deploys with the next frontend build, and rollback is a revert.

## Open Questions

- The 64px and 96px band bounds, the 150px narrow-layout track minimum, the 120px strip-tightening threshold and the strip spacing are starting values, to be tuned by eye during apply. The spec only requires that the band is bounded, derived from the card's width and uniform across the week.
