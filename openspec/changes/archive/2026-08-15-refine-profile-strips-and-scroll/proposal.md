## Why

Four rough edges on the profile page, all in the same area — the poster strips and getting back to where you were:

- Going back from an anime's detail page **sometimes lands at the top of the profile page** instead of where the tile was clicked. Scroll restoration exists, but it records the departing page's position on a deferred animation frame that can fire *after* the next page has already been scrolled to the top — so a zero gets written into the page you just left. Nothing is restored for the horizontal strips at all: a strip scrolled to its 20th tile is back at its first one on return.
- A strip holding exactly ten entries — every media-type tab except All, once the minimum-of-ten fill is doing its job — is **scrollable by a fraction of a pixel**, so it shows a scrollbar and drags a few pixels for no reason. Only a strip with more than the ten tiles that fit across it should scroll.
- The strips' scrollbars are **visual noise** under the posters; the strips are drag-scrollable and wheel-scrollable without one.
- **Top series ranks franchises I've barely watched.** A series whose main line is three aired seasons, of which one is in my list, is ranked on that one season's score alongside franchises I've actually followed. The existing "Multi-entry only" toggle doesn't catch it — that filter asks how many entries the franchise *has*, not how many of them I've seen.

## What Changes

- **Scroll restoration is made reliable.** The scroll position recorded for a history entry is the position that entry was actually left at — never overwritten by the client's own scroll-to-top of the page navigated to — and a restore in progress is abandoned only on real user input, not on the scroll events the restore itself produces.
- **The three poster strips remember their horizontal position** across back/forward navigation, alongside the page's vertical position. A fresh visit still starts every strip at its first tile.
- **A strip is scrollable only when it holds more entries than fit across it.** With ten or fewer, the strip does not scroll or drag at all, and its cursor stops advertising that it does.
- **The strips' scrollbars are hidden**, on all three, while wheel and drag scrolling keep working. The vertical lists on the page (Latest updates, edit history) keep their scrollbars.
- **Top series excludes franchises I've barely watched**: when a series' main line has two or more entries that have started airing and *at most one* of them is in my list, the series is not listed. A series whose main line has only one aired entry is unaffected — a confirmed-but-unaired second season doesn't count, so Cyberpunk: Edgerunners stays while To Your Eternity (three aired seasons, one in my list) goes.
  - This is **always on**, not a control: there is no way to switch it off, and it composes with — but is independent of — the existing "Multi-entry only" toggle.
  - "In my list" means an entry exists for that anime in **any** status, plan-to-watch included. A season sitting in plan-to-watch counts as coverage of the franchise.
  - Taken slightly wider than the literal ask: *at most one* rather than *exactly one*, so a franchise whose main line I know only through an extra (zero main-line entries in my list, two or more aired) is excluded for the same reason.

## Capabilities

### New Capabilities

None — both capabilities involved already exist.

### Modified Capabilities

- `page-state-restoration`: strengthens the scroll-position requirement so the recorded position can't be clobbered by the next page's scroll-to-top and a restore isn't abandoned by its own scrolling; adds a requirement that horizontally scrollable strips restore their offset with the page.
- `profile-stats`: modifies the Top series eligibility requirement with the watched-coverage rule; modifies the fixed-tile-size strip requirement so a strip that fits is not scrollable; adds a requirement that the strips show no scrollbar.

## Impact

- `frontend/src/hooks/useScrollRestoration.ts` — synchronous recording against the current history entry, and a cancel condition based on user input rather than on scroll events.
- `frontend/src/state/pageStateStore.ts` — per-element scroll offsets in the snapshot, and recency-aware eviction so an entry still reachable by back-navigation isn't dropped from the store mid-session.
- `frontend/src/pages/ProfilePage.tsx` — the drag-scroll hook grows strip-scroll restoration and a restore key; the three strips gain a "fits, so doesn't scroll" modifier driven by their entry count.
- `frontend/src/pages/ProfilePage.css` — hidden scrollbars on the three strips, and the non-scrollable modifier.
- `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs` — the watched-coverage condition alongside the existing eligibility check, computed from the entry status and airing status already loaded.
- `backend/AnimeTracker.Api.Tests` — cases for the new eligibility rule in the Top series service tests.
- No API shape change, no database change, and no change to the series page, search, or any other surface.
