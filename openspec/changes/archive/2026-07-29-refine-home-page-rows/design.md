## Context

Both rows live on the home page (`frontend/src/pages/HomePage.tsx`) and are pure CSS-layout problems — the markup and the dashboard payload already carry everything needed.

**Currently watching.** `.carousel__track` (`CurrentlyWatchingCarousel.css`) declares:

```css
max-width: calc(5 * 160px + 4 * 16px + 2 * 6px);  /* 876px */
padding: 6px;
```

The 6px padding is the hover reserve: `.anime-card::before` draws its highlight plate at `inset: -6px`, so the track must keep 6px inside its scroll boundary at each end or the edge cards' hover state is clipped. But the project has no global `box-sizing: border-box` reset — only `#root` and `.page-content` set it individually — so `.carousel__track` is content-box and that 876px is the **content** width. The scrollport (padding box) is therefore 876 + 12 = **888px**, while five cards plus four gaps come to 864px. Placed after the 6px left padding, the fifth card ends at 870px, leaving 18px of scrollport past it: the 16px gap plus 2px of the sixth card's poster.

The same 12px surplus explains the right end. Card pitch is 176px (160 + 16 gap). For N cards, scrollable width is 176N − 4, so the maximum scroll offset is 176N − 4 − 888 = 176(N−5) − 12 — twelve pixels short of a whole number of card steps. The arrow handler steps by exactly one pitch from a pitch-aligned position, so the final click clamps to that off-grid maximum and the row comes to rest showing a 12px sliver of the card before the leftmost visible one.

**Airing today.** `.airing-today__thumb` is 48×66 (not the 2:3 poster ratio used by `.anime-card__picture`) inside a row with `align-items: center`. At that size the poster reads as an icon, and centring puts the `time : Ep N` line and the title in the middle of the row rather than starting at the top. The markup in `AiringTodayList.tsx` already stacks meta above title, so only CSS changes.

## Goals / Non-Goals

**Goals:**
- Make the carousel's visible strip exactly five cards wide at every resting scroll position, at both ends, without clipping edge-card hover treatments.
- Keep arrow stepping landing flush on card boundaries, including the final step at the right end.
- Give "Airing today" a poster-sized 2:3 thumbnail and top-aligned text.

**Non-Goals:**
- Changing the number of visible cards, making the carousel responsive to viewport width, or introducing CSS scroll-snap.
- Touching the AnimeCard component, the hover plate, or any other consumer of `.anime-card`.
- Backend, DTO, or data-shape changes; and no change to the airing-today filter, ordering, or two-line title clamp.

## Decisions

### Carousel: make the declared width the border-box width

Add `box-sizing: border-box` to `.carousel__track` and keep the existing `calc(5 * 160px + 4 * 16px + 2 * 6px)`. The declaration then means what it reads as: the scrollport is 876px = five cards + four gaps + the two 6px hover reserves. Content width becomes 864px, exactly the five cards, and the surplus past the fifth card drops from 18px to the 6px hover reserve — empty space, never poster.

This also fixes the right end for free: scroll maximum becomes 176N − 4 − 876 = 176(N−5), an exact multiple of the card pitch, so the last arrow click lands flush on the final card instead of 12px short.

*Alternative — drop the padding to 864px content-box and give the cards margin instead:* same arithmetic, but it moves the hover reserve out of the track and into every card, and `.anime-card::before` is already sized against "the padding budget the carousel track reserves" (its comment). Rejected as a wider blast radius for identical geometry.

*Alternative — `scroll-snap-type: x mandatory` on the track:* would hide the off-grid resting position rather than fix it, and it fights the "stops at the ends" behaviour that currently works. Rejected; the geometry is the actual bug.

### Carousel: leave `scroll()` and the overflow detection alone

The arrow handler measures the pitch from the first two children at click time and rounds to the nearest boundary, so it needs no constant updated. The `ResizeObserver` overflow check (`scrollWidth > clientWidth + 1`) still reports overflow only past five cards: at exactly five, content 864 + padding 12 = scrollWidth 876 equals clientWidth 876, so the arrows stay hidden — the existing "arrows hidden when everything fits" behaviour is preserved.

### Airing today: 72×108 thumbnail at a fixed 2:3 ratio

Set the thumb to `width: 72px; aspect-ratio: 2 / 3` (108px tall), replacing the fixed 48×66. 72px is 1.5× the current width and fits comfortably in the 320px aside: 320 − 16 row padding − 72 thumb − 10 gap leaves 222px for text, still wide enough for the two-line title clamp to read well. Using `aspect-ratio` instead of a hard height matches `.anime-card__picture` and keeps posters from being squashed off-ratio.

The row's height is then set by the poster (108px) rather than by the text, which satisfies "the poster anchors the row" and leaves the title's two-line clamp untouched.

### Airing today: top-align via `align-items: flex-start`

Switch `.airing-today__row` from `align-items: center` to `flex-start`. The meta line then starts level with the top of the poster and the title follows directly beneath, with the slack falling below the text. No change to `.airing-today__text`'s internal stacking — it is already `column` with meta first.

## Risks / Trade-offs

- **[The 876px constant now silently depends on the 160px card width and 16px gap]** → It already did; the calc spells out both factors, and the design keeps them in the same expression so a card-width change is a one-line edit.
- **[Taller airing-today rows mean fewer visible before scrolling]** → Accepted: the user asked for a bigger image, and the section is a short same-day list, not a long feed.
- **[Fractional device-pixel rounding could still leave a hairline at some zoom levels]** → All values are whole pixels and the surplus is a full 6px of empty reserve, so a sub-pixel rounding error cannot expose poster art.
- **[No automated tests in the frontend]** → Verification is manual in the running app: check the row at 5, 6, and 10+ currently-watching entries at both scroll ends.
