## Why

On the main page's "Currently watching" row, incrementing an episode on any card snaps the row back to the first card whenever there are more than 5 entries — so after ticking off episode counts you lose your place and have to scroll back every time. The cause is the infinite-loop carousel: the increment updates the dashboard state, which hands the carousel a new `items` array, which re-runs the looping effect and re-centers the scroll position. The endless "spins around and around" behaviour is also unwanted on its own — a plain bounded row that stops at the first and last card is the desired feel.

## What Changes

- **BREAKING** (behaviour): Remove the infinite-loop scrolling from the "Currently watching" carousel. The row becomes a plain horizontal scroller that stops at the first card and at the last card, in both arrow-click and native trackpad/touch scrolling.
- Drop the three-copies-of-the-list rendering, the re-centering scroll listener, and the associated repositioning logic. Cards render exactly once each.
- The scroll position is preserved across an episode increment: incrementing a card leaves the row exactly where it was, regardless of entry count.
- Unchanged: at most 5 cards visible, the row bounded to 5 cards wide, arrows shown only when the row overflows, one-card-per-click arrow stepping, click-to-open-detail, and the inline progress bar with its plus control.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `main-dashboard`: The "Currently watching horizontal carousel" requirement drops infinite looping in favour of bounded scrolling, and gains an explicit guarantee that the scroll position survives an episode increment.

## Impact

- `frontend/src/components/CurrentlyWatchingCarousel.tsx` — remove `LOOP_COPIES`, the `looping`/`copies` rendering, and the re-centering effect; simplify card keys to the anime id.
- `frontend/src/components/CurrentlyWatchingCarousel.css` — no change expected; the existing `max-width` / `overflow-x: auto` track already produces bounded scrolling.
- No backend, API, or DTO changes. `HomePage`'s increment patching stays as-is.
