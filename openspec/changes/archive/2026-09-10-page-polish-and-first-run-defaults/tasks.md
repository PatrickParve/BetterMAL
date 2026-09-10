## 1. Next-episode countdown rounds up (backend, shared)

- [x] 1.1 Add one shared static helper beside `NextEpisodeEtaDto` (for example `NextEpisodeEta.From(DateTimeOffset? next, DateTimeOffset now)`). It returns null for no instant, otherwise `totalHours = (int)Math.Ceiling((next - now).TotalHours)` split into `Days = totalHours / 24` and `Hours = totalHours % 24` (design D1).
- [x] 1.2 Replace the private `ToEta` in `MainDashboardService` and in `AnimeDetailService` with calls to the shared helper, and delete both copies.
- [x] 1.3 Add unit tests for the helper:
  - null → null
  - 1 min → 0d 1h
  - 40 min → 0d 1h
  - exactly 2d 7h → 2d 7h
  - 2d 6h 20m → 2d 7h
  - 23h 30m → 1d 0h
  - exactly 10d → 10d 0h
- [x] 1.4 Confirm the frontend formats are untouched: the detail page shows `next in Xd Yh` and the carousel shows `Next ep: in X days, Y h`.

## 2. Anime detail synopsis/background box

- [x] 2.1 In `AnimeDetailPage.tsx`, derive `hasSynopsis` / `hasBackground`: non-null and non-whitespace after trim.
- [x] 2.2 Render the Synopsis section only when `hasSynopsis`, the Background section only when `hasBackground`, and omit the whole `detail-box` section when neither is true. Remove the "No synopsis available." fallback.

## 3. Recap: the year column leads in multi-year recaps

- [x] 3.1 In `RecapPage.renderRankings`, in the multi-year branch:
  - Give the year column grid column 1 and the season column grid column 2 when both are present. A lone column still takes column 1.
  - Emit the year sections before the season sections in DOM order.
  - Keep score rankings on grid row 1 and time rankings on row 2.
- [x] 3.2 Leave the yearly branch unchanged. Update the rankings comment in `RecapPage.tsx` and the `.recap-page__rankings` comment in `RecapPage.css` to say year-first, including for the narrow stack.

## 4. Profile stats: episodes split and Mean score removed (backend)

- [x] 4.1 Add `WatchMath.FirstViewingEpisodes(UserAnimeEntry)`. It returns `EpisodesWatched`, or `TotalEpisodes ?? EpisodesWatched` for an entry whose status is Rewatching (design D4).
- [x] 4.2 Change `AnimeStatsDto`: remove `MeanScore` and add `int RewatchedEpisodes`.
- [x] 4.3 Update `ProfileService.BuildStats`:
  - `Episodes` = Σ `FirstViewingEpisodes` over entries that are neither movie nor music.
  - `RewatchedEpisodes` = Σ `RewatchEpisodesIncludingCurrentRun` over all entries.
  - `Days` = Σ (first viewing + rewatched) × `EpisodeSeconds` over all entries.
  - Update the method's comment.
- [x] 4.4 Update `ProfileServiceStatsTests`:
  - A 12-episode completed series with 1 rewatch → Episodes 12, RewatchedEpisodes 12, and Days covers 24 episodes.
  - A Rewatching entry with count 2 at episode 1 → Episodes 12, RewatchedEpisodes 25, and Days covers 37 episodes. This replaces the old expectation of 25 Episodes.
  - Add tests: a film with rewatch count 2 → RewatchedEpisodes 2 and Episodes 0; no published total with 8 watched and 1 rewatch → 8 / 8; a never-rewatched entry → 0 rewatched.
- [x] 4.5 Fix any other compile references to `AnimeStatsDto.MeanScore` or its positional constructor, in services, tests, or export code.

## 5. Profile page (frontend)

- [x] 5.1 In `api/types.ts` `AnimeStatsDto`, remove `meanScore` and add `rewatchedEpisodes: number`.
- [x] 5.2 In `ProfilePage.tsx` `STAT_LABELS`, remove the Mean score row and add `{ key: 'rewatchedEpisodes', label: 'Rewatched episodes' }` directly after Rewatched, so Movies stays directly after Episodes. Drop the `meanScore` branch from `formatStatValue`.
- [x] 5.3 Add an optional `scoredCount?: number` prop to `ScoreDistribution`. When it is provided, render a `Scored: N` line directly above the `Mean score:` line, styled like the mean line.
- [x] 5.4 In `ProfilePage`, pass `scoredCount` as the sum of `profile.scoreDistribution.buckets[].count`. The recap's `ScoreDistribution` usage stays unchanged.

## 6. My list popularity sort

- [x] 6.1 Backend: add `int? PopularityRank` to `MyListItemDto` and populate it in `MyListService` from `entry.Anime.PopularityRank`. Update any tests or fixtures that construct the record.
- [x] 6.2 Frontend: add `popularityRank: number | null` to `MyListItemDto` in `api/types.ts`, and to the sortable item shape in `utils/anime.ts` (`SortableListItem`) and wherever my-list items are mapped into it.
- [x] 6.3 In `utils/anime.ts`, add `'popularity'` to `SortKey`, and add a `popularity` entry to the key comparators: `nullsLast` on `popularityRank` with `(a, b) => a - b` (rank 1 first) as its natural direction, so a flipped direction lists the least popular first and unknown ranks stay last. Update the `SortDirection` comment's list of natural directions to include Popularity.
- [x] 6.4 In `MyListPage.tsx`, add `{ value: 'popularity', label: 'Popularity' }` to `SORT_OPTIONS` directly after MAL score, so it appears in both the primary and tiebreaker selectors.

## 7. First-run preference defaults

- [x] 7.1 In `ContentFilterContext.tsx`, make `readInitialHideHentai` return `true` when the key is absent or `window` is undefined, and otherwise `stored === 'true'`.
- [x] 7.2 In `ScoreVisibilityContext.tsx`, make `readInitialHidden` and `readInitialAlwaysShowCompletedScores` default to `true` in the same way. Keep the storage keys unchanged.
- [x] 7.3 Check that the Settings page's Hide NSFW and Always-show checkboxes and the navbar switch reflect the new defaults with no other changes needed.

## 8. Verification

- [x] 8.1 Build and run backend tests in the .NET 10 SDK container, per the project's local build notes.
- [x] 8.2 Type-check and build the frontend with Node 22.
- [ ] 8.3 Manual checks:
  - A detail page with no synopsis and no background shows no box.
  - Countdowns under an hour read `0d 1h` / `0 days, 1 h`.
  - A multi-year recap shows the year column on the left and stacks year-first when narrow.
  - The profile shows no Mean score row, shows Rewatched episodes under Rewatched, and shows Scored above Mean score.
  - Sorting my list by Popularity lists most popular first, the direction control reverses it, entries with no rank stay last, and it works as the tiebreaker too.
  - A private window starts with scores hidden, Always-show on, and Hide NSFW on, and toggling each persists across a reload.
- [x] 8.4 Run `openspec validate page-polish-and-first-run-defaults` and fix any reported issues.
