## 1. Shared badge logic (backend)

- [x] 1.1 Add `Dropped` and `Unwatched` to the `SeriesProgressBadge` enum in `Services/Series/SeriesListDto.cs`, updating its doc comment to describe all six outcomes (design.md D7).
- [x] 1.2 Rewrite `SeriesRankingIndex`'s `ProgressBadge` helper to the six-step precedence (design.md D1): Completed (unchanged gate), Dropped (D2 — most-recently-aired drop with nothing watched after it), Caught up/N behind generalized over every aired main-line entry rather than currently-airing only (D1 rule 3/4), Unwatched (D3 — decided before the unknown-broadcast-count guard can block it), no badge as the final fallback. Reuse the `Order` field already on `SeriesRankingMemberProjection`.
- [x] 1.3 Confirm the unknown-broadcast-count guard (D4) only blocks the Caught-up/Behind computation, never the Dropped or Unwatched checks.

## 2. Backend tests

- [x] 2.1 In `SeriesListProgressBadgeTests.cs`, replace the now-outdated "unwatched finished entry" and "unknown broadcast count" cases with the new precedence's full case set: Completed (unchanged), Dropped with nothing watched after it, a drop later resumed (not Dropped), Unwatched (nothing watched, something aired), a partially-watched finished entry reading as "N behind" (not silence), Caught up/Behind generalized across finished+currently-airing entries together, and the unknown-broadcast-count guard firing only once Dropped/Unwatched are ruled out.
- [x] 2.2 Add a case confirming a dropped entry that is itself the only aired main-line entry (nothing after it, vacuously) reads as Dropped.

## 3. Shared badge logic (frontend)

- [x] 3.1 Add `'Dropped' | 'Unwatched'` to the `SeriesProgressBadge` type in `frontend/src/api/types.ts`.
- [x] 3.2 Rewrite `SeriesPage.tsx`'s `completionBadge` function to the identical six-step precedence (design.md D1/D2/D3/D4), operating on `series.mainLine`'s existing `order`, `airingStatus`, `entry?.status`, `entry?.episodesWatched`, and `airedEpisodes` fields — no new data needed.

## 4. Badge/pill presentation

- [x] 4.1 In `SeriesCompletionBadge.tsx`/`.css`, add `Dropped`/`Unwatched` to the label and colour-class maps: `Dropped` → the app's Dropped-status red, `Unwatched` → the app's Plan-to-watch purple, `Completed` → the app's Completed-status blue (replacing the accent purple it uses today).
- [x] 4.2 Give `SeriesStatusPill.css` and `SeriesCompletionBadge.css` a shared `min-width` plus centred text, at both the series-page header scale and the Series page card scale (`SeriesCard.css`'s existing size overrides), so pills/badges of different label lengths render at a consistent width.

## 5. Series page filter buttons (frontend)

- [x] 5.1 Add a `progressFilter`/`statusFilter` pair of multi-select button groups to `SeriesBrowserPage.tsx`'s header controls, values persisted in `?progress=`/`?status=` URL params (comma-separated, mirroring `SeasonPage`'s `?type=` handling) so they survive back-navigation.
- [x] 5.2 Add a pure `filterSeries(items, progressFilter, statusFilter)` module function (alongside `sortSeries` in `utils/anime.ts`): `Watched` matches badge `Completed` or `CaughtUp`; `Behind`/`Dropped`/`Unwatched` match their own badge value; a badge of `None` matches no Progress button; status filtering matches the card's `status` field directly; empty selections in a group apply no filter for that group; selections across the two groups AND together, selections within one group OR together.
- [x] 5.3 Apply `filterSeries` before `sortSeries` (or after — order is immaterial, both are pure array transforms) and before the revealed-count slice, so switching a filter re-slices from the top of the filtered-and-sorted array, matching how a sort change already behaves.
- [x] 5.4 Style the filter buttons at the shared control height alongside the sort `<select>`, per `page-header-design`'s filter/sort cluster rule.

## 6. Verification

- [x] 6.1 `dotnet test` — all backend tests green.
- [x] 6.2 `npm run build` and `npm run lint` in `frontend/` clean (use nvm's Node v22).
- [x] 6.3 Manual pass: the series detail page and a Series-page card for the same franchise show matching badges across a Completed, a Dropped, an Unwatched, a Caught-up, and a Behind example; badges/pills read as uniform-width; selecting Progress/Status filter buttons narrows the grid correctly and combines (AND across groups, OR within a group); filters and sort both survive back-navigation with scroll position.
