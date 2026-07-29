## Why

Two home-page rows read wrong at their edges. The "Currently watching" carousel is supposed to be bounded to exactly 5 cards, but its visible strip is 12px wider than the 5-card budget, so a sliver of the 6th card shows past the fifth one — and at the far right end the row settles 12px off-grid, showing a sliver of the previous card. "Airing today" gives its poster a 48px thumbnail that reads as an inline icon rather than the row's anchor, and its text block is vertically centred against that thumbnail instead of starting at the top of the row.

## What Changes

- Bound the currently-watching row's visible strip to exactly 5 cards plus the hover-overflow reserve, so no part of a 6th card is visible when the row is scrolled fully left, when the leftmost card sits flush at the edge after an arrow click, or when the row is scrolled fully right.
- Enlarge the "Airing today" row thumbnail so the poster is clearly the row's anchor, at the same 2:3 poster aspect used elsewhere.
- Top-align the "Airing today" row text: the `time : Ep N` line sits at the top of the row, with the title starting directly beneath it, instead of the pair being centred against the thumbnail.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `main-dashboard`: the "Currently watching horizontal carousel" requirement gains an explicit bound that no part of a 6th card is visible at any scroll position; the "Airing today filtered to my list in local time" requirement tightens the row layout to a large poster-aspect thumbnail with top-aligned text.

## Impact

- `frontend/src/components/CurrentlyWatchingCarousel.css` — track width/box-sizing.
- `frontend/src/components/AiringTodayList.css` — thumbnail size and row alignment.
- `frontend/src/components/AiringTodayList.tsx` — layout comment only; markup already orders meta above title.
- No backend, API, or DTO changes; both dashboard payloads already carry everything these rows render.
