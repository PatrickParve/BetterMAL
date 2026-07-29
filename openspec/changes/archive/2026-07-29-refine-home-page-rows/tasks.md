## 1. Currently-watching carousel bound

- [x] 1.1 Add `box-sizing: border-box` to `.carousel__track` in `frontend/src/components/CurrentlyWatchingCarousel.css`, keeping the existing `max-width: calc(5 * 160px + 4 * 16px + 2 * 6px)` and `padding: 6px`, so the declared 876px is the scrollport width rather than the content width.
- [x] 1.2 Update the comment above the `max-width`/`padding` pair (or add one) to record why the width is border-box: the 876px is five cards + four gaps + the two 6px hover reserves, and treating it as content width is what let a sliver of the 6th card show.
- [x] 1.3 Update the component comment at the top of `CurrentlyWatchingCarousel.tsx` so "capped to 5 visible cards" states the exact bound — no part of a 6th card visible at any resting position, at either end.

## 2. Airing today row layout

- [x] 2.1 In `frontend/src/components/AiringTodayList.css`, replace `.airing-today__thumb`'s `width: 48px; height: 66px` with `width: 72px; aspect-ratio: 2 / 3`, matching the poster ratio used by `.anime-card__picture`.
- [x] 2.2 Change `.airing-today__row` from `align-items: center` to `align-items: flex-start` so the `time : Ep N` line starts level with the top of the poster and the title follows directly beneath it.
- [x] 2.3 Update the `AiringTodayList.tsx` layout comment to describe the row as a poster-sized thumbnail beside top-aligned text (meta line, then title).

## 3. Verify in the running app

- [x] 3.1 Build/run the frontend (use nvm's node v22 for Vite) and load the home page.
- [x] 3.2 With more than 5 currently-watching entries, confirm at scroll-left-end, after each right-arrow click, and at the scroll-right-end that exactly 5 cards are visible with no sliver of a neighbouring card at either edge, and that the last card sits flush at the right edge.
- [x] 3.3 Confirm the edge cards' hover plate and plus control are still fully visible (not clipped) when the row is scrolled fully left and fully right.
- [x] 3.4 Confirm arrows still do not appear when there are exactly 5 or fewer entries, and that incrementing an episode from a card leaves the scroll position unchanged.
- [x] 3.5 Confirm "Airing today" rows show the larger 2:3 poster with the time/episode line at the top of the row and the title beneath, still clamped to two lines with an ellipsis only when the title actually overflows, and that a row with an unresolved episode number shows the time alone.
