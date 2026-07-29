## 1. Card link boundary and carousel clipping

- [x] 1.1 Add a `footer?: ReactNode` prop to `AnimeCard` (`frontend/src/components/AnimeCard.tsx`), rendered as a sibling after the `<Link>`, and document the three slots in the component comment: `children` (inside link), `actions` (absolute, top-right), `footer` (below link, never navigates).
- [x] 1.2 Move the flex-column layout and 6px gap from `.anime-card__link` to `.anime-card` in `AnimeCard.css` so the link and footer share one flow with unchanged spacing.
- [x] 1.3 In `CurrentlyWatchingCarousel.tsx`, pass `<ProgressBar>` and the next-episode countdown via `footer` instead of `children`; confirm the card still reads picture → title → bar/count/+ → countdown.
- [x] 1.4 Add symmetric inner padding (≈6px) to `.carousel__track` in `CurrentlyWatchingCarousel.css` and grow its `max-width` by the horizontal padding (`5 * 160px + 4 * 16px + 2 * padding`) so exactly five cards still fit.
- [x] 1.5 Verify by hand: scroll the carousel fully right and fully left, hover the edge cards' "+" — nothing is clipped; clicking the bar, the count, or blank space in the progress row does not navigate; clicking the poster or title does.

## 2. Set-episodes save path

- [x] 2.1 In `frontend/src/context/CompletionPromptContext.tsx`, generalize `increment(target)` into `setEpisodesWatched(target, value)` that PATCHes `episodesWatched: value`, keeping the existing prompt trigger (`previousStatus !== 'Completed' && saved.status === 'Completed'`) untouched.
- [x] 2.2 Keep `increment(target)` as the `target.episodesWatched + 1` case so every existing call site is unchanged; expose both through the context value and a `useSetEpisodesWatched()` (or extended `useEpisodeIncrement()`) hook.
- [x] 2.3 Update the context's header comment to state that both the "+" and the inline count edit route through here, so the completion prompt cannot be skipped at a new call site.

## 3. Inline editable count in `ProgressBar`

- [x] 3.1 Add `onSetWatched?: (value: number) => void | Promise<void>` and `max?: number | null` props to `frontend/src/components/ProgressBar.tsx`; when `onSetWatched` is absent, render the label exactly as today.
- [x] 3.2 Render the `watched` half as a click/focus-to-edit control that swaps to a controlled `<input type="text" inputMode="numeric">` with the current value pre-selected, keeping `/{total}` as static text beside it and sizing the field from the digit count so the row does not jump.
- [x] 3.3 Filter keystrokes to digits only (`replace(/\D/g, '')`) so `-` and non-numeric text are unenterable.
- [x] 3.4 Commit on Enter and on blur; cancel on Escape restoring the stored value with no save.
- [x] 3.5 On commit, clamp to `[0, max]` when `max != null` and apply no upper cap when it is `null`; skip the save entirely when the value is empty, invalid, or equal to the stored count.
- [x] 3.6 Stop propagation and `preventDefault()` on the field's click/keydown, matching `IncrementButton` and `ScoreValue`, so no ancestor handler fires.
- [x] 3.7 Disable both the field and the "+" while `incrementPending` is true, and clear local edit state after a commit so a failed save falls back to the prop value.
- [x] 3.8 Style the count trigger and field in `ProgressBar.css`: same font size and tabular numerals as today, a hover/focus affordance showing it is editable, and identical width in both states.

## 4. Wire the editable count into the three surfaces

- [x] 4.1 `CurrentlyWatchingCarousel.tsx`: pass `onSetWatched` (calling `setEpisodesWatched` with the same target it builds for `increment`, same `onSaved`/`onCompleted`) and `max={item.totalEpisodes}`.
- [x] 4.2 `MyListPage.tsx`: pass `onSetWatched` reusing `handleSaved(item.animeId)` and `max={item.totalEpisodes}`, keeping the pending guard shared with the "+" so a row cannot be edited twice concurrently.
- [x] 4.3 `AnimeDetailPage.tsx`: pass `onSetWatched` only when `detail.entry` exists (mirroring the existing `onIncrement` gate) and `max={detail.totalEpisodes}`; the Edit button stays the path for adding an anime not yet in the list.
- [x] 4.4 Verify each surface by hand: type a mid-range number, an over-total number (clamps), and the exact total (saves, auto-completes, and raises the completion prompt); Escape cancels; empty entry reverts; my list neither reloads nor re-sorts after a save.

## 5. Hover treatment

- [x] 5.1 Add `.anime-card:hover` / `.anime-card:focus-within` in `AnimeCard.css`: accent ring on the poster plus `--shadow`, no transform, so nothing shifts and nothing exceeds the carousel's padding budget.
- [x] 5.2 Add a row hover to `.my-list-row` (`MyListPage.css`) using `--accent-bg` plus accent border — set the top/right/bottom border colours only, leaving `border-left-color` to the status modifier so the status stripe survives hover.
- [x] 5.3 Add the matching row hover to `.top-anime-row` (`TopAnimePage.css`).
- [x] 5.4 Strengthen the nav-control hovers to background + border, not colour alone: `.navbar__link`, `.navbar__toggle`, `.navbar__settings` (`Navbar.css`), `.carousel__arrow` (`CurrentlyWatchingCarousel.css`), `.pagination__arrow` / `.pagination__page` (`Pagination.css`), `.season-page__nav button` (`SeasonPage.css`), `.airing-page__nav button` (`AiringPage.css`).
- [x] 5.5 Ensure `.navbar__link--active` is ordered after the hover rule so the current page stays distinguishable while hovered.
- [x] 5.6 Verify in both light and dark schemes that hover states are clearly visible and that no card or row hover shifts surrounding layout.

## 6. Verification

- [x] 6.1 Run `npm run build` in `frontend/` under node v22 (`nvm use 22`) and confirm it typechecks and builds clean.
- [x] 6.2 Walk the scenarios in the delta specs across Home, My list, and an anime detail page — carousel clipping, non-navigating progress row, inline count rules, completion prompt on a typed total, and hover states on cards, rows, and nav controls.
- [x] 6.3 Run `openspec validate polish-card-hover-and-inline-episode-edit --strict` and confirm the change validates.

## 7. Addendum: episodes-aired ceiling, input robustness, hover visibility

Follow-up fixes made after 1-6 shipped, in response to feedback on the live app: the count could be set arbitrarily high on a still-airing show, the field's resting position and in-progress input weren't visually aligned with the static `AiringProgressBar` reference, out-of-range values were only corrected on commit rather than blocked while typing, an empty field didn't reliably revert on blur, and the card hover ring was hard to see against busy poster art.

- [x] 7.1 Add `EpisodesAired` to `CurrentlyWatchingItemDto` (`MainDashboardDto.cs`) and `MyListItemDto`, populated via the existing `IEpisodeScheduleService.EpisodesAiredAsOf` in `MainDashboardService` (already injected) and `MyListService` (newly injected); mirror the two new fields into the frontend `CurrentlyWatchingItemDto`/`MyListItemDto` types.
- [x] 7.2 In `UserAnimeEntryEditService.ApplyEpisodesWatched`, replace the `TotalEpisodes`-only ceiling with `scheduleService.EpisodesAiredAsOf(anime, now) ?? anime.TotalEpisodes`, injecting `IEpisodeScheduleService` as a new constructor dependency.
- [x] 7.3 Wire `max={item.episodesAired ?? item.totalEpisodes}` (`detail.episodesAired ?? detail.totalEpisodes` on the detail page) at all three call sites, replacing the total-only `max`.
- [x] 7.4 In `ProgressBar.tsx`, base the "+" button's disabled state on the same `max` ceiling (falling back to `total` only when `max` is omitted entirely), so it disables at the aired-so-far count for a still-airing show instead of always at the total.
- [x] 7.5 Clamp the field's value live in its `onChange` handler (not only on commit), so a value above the ceiling is never displayed even momentarily.
- [x] 7.6 Replace the one-shot "skip the next blur" ref with an `editingRef`-backed idempotent `closeEditing(save)`, so a stray second close event for the same edit session (e.g. blur following an Escape) is a no-op instead of risking a stuck-open field on the next legitimate edit.
- [x] 7.7 Fix the trigger/input's resting position in `ProgressBar.css`: pin `box-sizing: content-box` (overriding the browser's `border-box` default for `<button>`, which otherwise squeezes the digit into an unreadably thin area) and zero out padding/margin so the count sits exactly where `AiringProgressBar`'s plain-text label does, with the click/edit affordance shown only via hover/focus background.
- [x] 7.8 Replace `.anime-card`'s poster-level hover ring with a whole-card `::before` "plate" (background + border + shadow, `inset: -6px`, `z-index: -1` contained by an explicit `z-index: 0` on `.anime-card`), so the highlight no longer depends on contrast against the poster image.
- [x] 7.9 Update `proposal.md`, `design.md`, and the `list-editing`/`navigation-and-search` delta specs to reflect the aired-so-far ceiling, live clamping, the close-path fix, and the hover-plate approach.
- [x] 7.10 Rebuild and verify live against the running Docker stack: real `episodesAired` values from the API, the "+" button disabling at the aired count, live clamping while typing, empty-field revert on blur (including the specific stale-flag sequence that used to break it), and the hover plate visible in both light and dark mode against busy poster art — all with zero unintended mutating requests against the real MAL-synced data.

## 8. Addendum: profile page hover, rating-distribution percentage

- [x] 8.1 Add the shared row hover (background wash + accent border) to `.profile-list-row` in `ProfilePage.css`, covering both the "Latest updates" feed and the two opinion-divergence lists, matching `.my-list-row`/`.top-anime-row`.
- [x] 8.2 First pass on `.top-anime-strip__item`/`.rewatched-strip__item`: an inset `box-shadow` ring in `--accent-border` — found to wash out against busy poster art for the same reason as the original `AnimeCard` ring (decision 8), since it's translucent paint drawn directly over the image.
- [x] 8.3 Second pass: opaque `var(--accent)` ring plus a `rgba(0,0,0,0.2)` scrim via `::after`, reusing the badge's dark-scrim-for-guaranteed-contrast technique — fixed the visibility problem but rejected on style grounds (darkening the user's own poster art on hover).
- [x] 8.4 Final: replace both prior attempts with a plain `transform: scale(1.08)` on hover/focus-visible, no border/ring/background at all; add 8px padding to `.top-anime-strip`/`.rewatched-strip` so a scaled-up edge tile doesn't clip against the scroll boundary, and `z-index: 1` on the hovered tile so it renders above neighbours it grows into instead of being overlapped.
- [x] 8.5 Fix `ProfilePage.tsx`'s score-distribution bar width from `bucket.count / maxBucketCount` (relative to the largest bucket) to `bucket.count / totalRated` (share of every anime rated, `totalRated` summed from the buckets themselves) — no backend change, since `BuildScoreDistribution` already returns per-score counts that sum to the total rated count.
- [x] 8.6 Add a `profile-stats` delta spec (new capability touched by this change): MODIFIED "All-anime score distribution" for the share-of-total bar semantics, ADDED "Poster-tile hover in My top anime and Most rewatched" for the scale-only treatment; extend `navigation-and-search`'s "Hover highlight on anime cards and rows" to cover the profile page's list rows and explicitly carve out the poster strips as using their own treatment instead.
- [x] 8.7 Rebuild and verify live: score-distribution bar percentages match `count/totalRated` exactly against real profile data; row hover on activity feed and divergence lists; scale-up hover on both poster strips with no clipping at either scroll edge, in light and dark mode, against the busiest cover art in the list.
