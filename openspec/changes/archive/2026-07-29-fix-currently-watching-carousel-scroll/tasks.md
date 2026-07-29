## 1. Remove the looping machinery

- [x] 1.1 In `frontend/src/components/CurrentlyWatchingCarousel.tsx`, delete the `LOOP_COPIES` constant, the `looping` derived value, and the `copies` array
- [x] 1.2 Delete the re-centering effect in full (`oneCopyWidth`, `reposition`, `center`, `handleScroll`, its `scroll` listener and `ResizeObserver`)
- [x] 1.3 Replace the `copies.flatMap(...)` render with a direct `items.map(...)` and change the card key from `` `${copy}:${item.animeId}` `` to `item.animeId`

## 2. Simplify what remains

- [x] 2.1 Reduce the overflow effect's check to `node.scrollWidth > node.clientWidth + 1`, dropping the now-dead copy-count arithmetic; keep its `ResizeObserver` so arrow visibility still tracks resizes
- [x] 2.2 Rewrite the component's leading block comment so it describes a bounded 5-card row instead of the three-copy loop, keeping the notes on click-to-navigate vs. plus-to-increment
- [x] 2.3 Confirm `scroll()` and `CurrentlyWatchingCarousel.css` need no edits (arrow stepping and the 5-card `max-width` / `overflow-x: auto` track already give bounded scrolling)

## 3. Verify

- [x] 3.1 Type-check and build the frontend (`nvm use 22` first — the default node is v16 and Vite needs v22)
- [x] 3.2 With more than 5 currently-watching entries: scroll to a later card, click its plus control, and confirm the count and bar update while the row stays put
- [x] 3.3 Confirm the row stops at the first and last card in both directions (arrows and trackpad) and that no anime appears twice
- [x] 3.4 Confirm arrows are still hidden when 5 or fewer cards fit, and that clicking a card still opens its detail page without incrementing
