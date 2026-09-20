## 1. A hidden score's slot keeps one height (shared, design D1)

- [x] 1.1 In `ScoreValue.css`, add `min-height: 1lh` to `.score-value`, beside the existing `min-width: 4ch`, with a comment naming the same reason `ScoreChip.css` gives for its own floor — the reveal control has no text line of its own, so without a floor the slot is shorter hidden than shown.
- [x] 1.2 In `AnimeDetailPage.css`, add the same `min-height: 1lh` to `.anime-detail-page__rank-slot`, which is `RankValue`'s own hidden-branch wrapper and needs the floor independently of `.score-value`.
- [x] 1.3 Constrain the reveal control in the compact chip density, where one 11px line is *shorter* than the ~17px control so the floor above cannot help: scope a rule to `.score-chip--compact .reveal-control` that holds it to one line of its own context. Leave the control's icon, colour, hover, and focus ring alone at every other density.
- [x] 1.4 Verify by eye on a detail page whose anime has both a MAL score and a rank, with the global hide toggle on:
  - reveal the score alone — the box height is unchanged and the info box below does not move;
  - reload, reveal the rank alone — same;
  - reveal both — same, and the box was already at that height before anything was revealed.
- [x] 1.5 Verify the same on a compact pair — a series timeline card and a series extra tile — that revealing the MAL score neither grows nor shrinks the tile. Check the default chip density too (a Top anime showcase card, a series page average chip), which already had the floor and must be unchanged.

## 2. Top anime serves cache first (backend, design D2)

- [x] 2.1 Add a `TopAnimeRefreshResultDto` with an outcome of `Fetched` / `Skipped` / `Failed`, modelled on `SeasonRefreshResultDto` and its outcome enum.
- [x] 2.2 In `TopAnimeService`, rename `EnsureFreshAsync` to a public `RefreshAsync(string rankingType, CancellationToken)` returning that DTO. Keep its body as it stands — the `RefreshGate` lock keyed `top-anime:{type}`, the double-checked `IsFreshAsync`, the swallow-and-log around `FetchAndCacheAsync` — and have it report `Skipped` from either freshness check, `Fetched` after a successful `FetchAndCacheAsync`, and `Failed` from the catch.
- [x] 2.3 Drop the `EnsureFreshAsync` call from `GetRankingAsync`, so it reads the repository and returns. Update its comment and the `ITopAnimeService` doc comment: the read no longer refreshes, and the visit path is now the refresh call.
- [x] 2.4 Add `RefreshAsync` to `ITopAnimeService`.
- [x] 2.5 In `TopAnimeController`, add `POST /api/top-anime/refresh` taking the same `type` query parameter, rejecting an unrecognised value with the same `400` the `GET` gives before any cache read or MAL request. Update the `GET`'s doc comment — it is now a cache-only read.

## 3. Top anime backend tests

- [x] 3.1 Retarget `TopAnimeServiceTests`' refresh-behaviour tests at `RefreshAsync`: one list's fetch not marking another as fetched, refreshing one list leaving another's rows intact, the same-day revisit making no MAL call, the new-day revisit fetching again, and a failed fetch leaving the list unmarked. The last one keeps asserting that `GetRankingAsync` still serves the cached rows afterwards.
- [x] 3.2 Keep the read-shape tests (`RowsCarryAiringStatusAndEpisodesAired`, the change-detection pair, both search-index invalidation tests) working against whichever of the two calls now does that work — the detection and invalidation tests move to `RefreshAsync`, the row-shape test stays on `GetRankingAsync`.
- [x] 3.3 Add a test that `GetRankingAsync` makes no MAL call at all, even for a list whose cache is stale by days — the core of this change.
- [x] 3.4 Add outcome tests for `RefreshAsync`: `Fetched` on a first fetch, `Skipped` on a same-day second call, `Failed` when the MAL client throws.
- [x] 3.5 Update `TopAnimeControllerTests` for the two-call shape, including the new route's `400` on an unrecognised `type`.

## 4. Top anime loads cached then refreshes (frontend, design D3/D4)

- [x] 4.1 Add `TopAnimeRefreshResultDto` to `api/types.ts` and `refreshTopAnime(type)` to `api/client.ts`, posting to `/api/top-anime/refresh?type=…`, beside `refreshSeason`.
- [x] 4.2 In `TopAnimePage`, extend `loadTopAnimeOnce` so a type's first load in the session reads the cache, resolves with it immediately, and then posts the refresh; on a `fetched` outcome it re-reads and replaces `cachedItemsByType` for that type. `skipped` and `failed` change nothing. Re-selecting a list already in the module cache still makes no request.
- [x] 4.3 Make the refreshed rows reach the screen: after the re-read lands, nudge component state the way `setEntry` already does so a page displaying that type re-renders, including when the reader has since switched to another list and back.
- [x] 4.4 Keep a module-scope map of the entries `setEntry` has written this session, and re-apply it over a freshly re-read list before it replaces the cached one, so an Add or Edit made just before the refresh landed is not reverted (design D4).
- [x] 4.5 Update the module-cache comment block at the top of `TopAnimePage.tsx` to describe the new sequence — cache-first read, one background refresh per type per session, re-read only on `fetched`.
- [x] 4.6 Verify in the running app: with a list cached from a previous day, opening it paints rows with no "Loading…", and the refreshed rows replace them a moment later without a loading state; adding an anime from a row immediately before the refresh lands leaves that row reading "Edit".

## 5. MAL leads the score pair (design D5/D6)

- [x] 5.1 `MyListRow.tsx`: move the `my-list-row__mal-score` span ahead of `my-list-row__my-score`. Both keep their widths and centred alignment, so no CSS changes; confirm the columns still line up down the list and the Edit button still trails both.
- [x] 5.2 `TopAnimePage.tsx` showcase tier: put the `role="mal"` chip before the `role="mine"` chip.
- [x] 5.3 `TopAnimePage.tsx` ranks 4–10 cards: put the `score--mal` span before `score--mine`, and in `TopAnimePage.css` flip `.top-anime-card__scores .score-value` from `justify-content: flex-end` to `flex-start`, updating the comment above it — MAL now sits at the card's leading edge, my score at the trailing one.
- [x] 5.4 `TopAnimePage.tsx` flat rows: put `top-anime-row__mal-score` before `top-anime-row__my-score`. The MAL column keeps `text-align: right` and its `flex-end` reveal alignment; update the comment in `TopAnimePage.css` to say the column is aligned to its own end rather than to the row's.
- [x] 5.5 `ProfilePage.tsx` divergence lists: the trailing line becomes `MAL <score> · Me <score>`, with each label moving alongside its own value.
- [x] 5.6 `RecapPage.tsx` hot takes: put the `score--mal` span before `score--mine` in `recap-hot-take__scores`. The two fixed `ch` widths are per-class and travel with their spans, so `RecapPage.css` needs no change — confirm the scores still line up down the list.
- [x] 5.7 Confirm the six already-correct surfaces are untouched: series browser cards, the series page's average chips and entry rows, series timeline cards, series extra tiles, the profile page's Top series tiles, and the detail page's two boxes.
- [x] 5.8 With the hide toggle on, check the reveal control on each changed surface still begins or ends where its value would — centred in my-list's column, at the leading edge of a ranks-4-to-10 card, at the end of a flat row's column, right-aligned in a hot take, and at the end of a profile divergence line.

## 6. Wrap-up

- [x] 6.1 Run `dotnet build` and `dotnet test` for the backend, and the frontend type-check and build (node v22 via nvm).
- [x] 6.2 Update `CODE_GUIDE.md`: the top-anime line in the "Big picture" paragraph and the `GET /api/top-anime` endpoint row now describe a cache-first read plus a visit-triggered refresh route, and `TopAnimeService`'s entry in `Services/Library/` says the same.
- [x] 6.3 Re-read the four delta specs against what was built, and run `openspec validate --changes polish-score-layout-and-top-anime-loading`.
