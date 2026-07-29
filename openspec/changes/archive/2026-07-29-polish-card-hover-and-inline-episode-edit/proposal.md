## Why

The episode-progress row on anime cards has three rough edges that show up dozens of times a session: the last carousel card's "+" button is visually clipped by the scroll container, clicking anywhere on the progress bar or count navigates away instead of staying put, and setting a count to a specific number (after binge-watching, or to correct a mistake) requires opening the full edit overlay just to change one field. On top of that, cards and nav buttons give almost no hover feedback, so it is hard to tell what is clickable and which card the pointer is on.

## What Changes

- **Un-clip the "+" button in the currently-watching carousel.** The carousel track's `overflow-x` scroll box currently cuts the hover-scaled "+" on the last card. The track gets inner breathing room so hover effects on the edge cards render fully in both directions.
- **Make the progress row non-navigating.** On anime cards, the progress bar, the `watched/total` count, and the "+" control move out of the card's navigation link. Clicking the poster or title still opens the detail page; clicking anywhere in the progress row does not navigate.
- **Add a clear hover highlight to anime cards and nav buttons.** Anime cards (dashboard carousel, current season, search, season browser) and the equivalent clickable rows (my list, top anime) get a visible hover treatment — accent border/background lift rather than a color-only change. Nav buttons (navbar links, score-visibility toggle, settings, carousel arrows, pagination and season/airing period arrows) get a consistent, clearly visible hover state.
- **Make the watched count directly editable in place.** Everywhere the "+" appears next to an episode count — the Home currently-watching carousel, the anime detail page, and my list rows — the count becomes a click-to-edit field. Typing a number and confirming saves it through the same entry-edit path as the "+", including the started-date, activity-log, debounced-sync, and completion-prompt behavior.
- **Constrain the editable count.** Only non-negative whole numbers are accepted, clamped live as they're typed rather than only once confirmed. The enterable ceiling is the number of episodes actually available: the total once an anime has finished airing, or the number of episodes aired so far while it's still airing (which can be lower than the eventual total) — the same ceiling the "+" control disables at. With neither known, there is no upper cap. Invalid or empty input reverts to the stored value rather than saving, whether the field is closed by Enter, Escape, or clicking elsewhere.
- **Extend the hover highlight to the profile page.** The "Latest updates" feed and the two opinion-divergence rows get the same background/border row highlight as my list and top anime. The poster tiles in "My top anime" and "Most rewatched" get their own hover instead — a slight scale-up in place, deliberately without the border/background language used everywhere else.
- **Fix the rating-distribution bars.** Each score's bar now shows that score's share of all rated anime (count ÷ total rated), rather than a size relative to whichever score happens to have the most entries.

## Capabilities

### New Capabilities

None — this change refines existing surfaces.

### Modified Capabilities

- `main-dashboard`: the currently-watching card's progress row is excluded from the card's navigation link, its "+" is no longer clipped at the row's edges, its count is directly editable, and cards show a hover highlight.
- `anime-detail`: the detail page's `watched/total` count is directly editable in place, alongside the existing "+" and overlay editor.
- `library-views`: my list rows get a directly editable watched count and a hover highlight; top anime rows get the same hover highlight.
- `navigation-and-search`: the "clickable anime cards" requirement is narrowed so the progress/count/"+" region is not part of the card's link, and navbar/nav controls gain a clearly visible hover state.
- `list-editing`: the completion score prompt fires on any episode-count change that takes an entry into Completed, not only on "+" increments; the editable count's upper cap is episodes-aired-so-far for a still-airing anime rather than always its eventual total, enforced live as it's typed and again server-side.
- `profile-stats`: the "Latest updates" feed and opinion-divergence rows gain the shared row hover highlight; the "My top anime"/"Most rewatched" poster tiles gain their own scale-up-only hover; the rating-distribution bars now size to each score's share of all rated anime instead of relative to the largest bucket.

## Impact

- Frontend components: `AnimeCard.tsx`/`.css` (new non-link slot, whole-card hover highlight), `ProgressBar.tsx`/`.css` (editable count, live-clamped to the aired-so-far/total ceiling), `IncrementButton.css`, `CurrentlyWatchingCarousel.tsx`/`.css`, `Navbar.css`, `Pagination.css`, `SeasonPage.css`, `AiringPage.css`, `MyListPage.tsx`/`.css`, `TopAnimePage.css`, `AnimeDetailPage.tsx`, `ProfilePage.tsx`/`.css` (row/tile hover, score-distribution bar percentages).
- Frontend context: `CompletionPromptContext.tsx` — the increment helper generalizes to "set episodes watched to N", with increment as the +1 case, so the completion prompt covers both.
- Frontend types: `CurrentlyWatchingItemDto` and `MyListItemDto` gain `episodesAired: number | null`, mirroring the field `CurrentSeasonItemDto`/`AnimeDetailDto` already carry.
- Backend: `MainDashboardService` and `MyListService` now project `episodesAired` onto `CurrentlyWatchingItemDto`/`MyListItemDto` via the existing `IEpisodeScheduleService.EpisodesAiredAsOf` (already used for the current-season and detail DTOs, not new computation logic). `UserAnimeEntryEditService.ApplyEpisodesWatched` now caps against `EpisodesAiredAsOf` (falling back to `TotalEpisodes` when that's unknown) instead of `TotalEpisodes` alone — closing a gap where a still-airing show with a known total could otherwise be set arbitrarily far ahead of what had actually aired.
- API contract: `CurrentlyWatchingItemDto` and `MyListItemDto` (from `GET /api/dashboard` and `GET /api/my-list`) gain an `episodesAired: number | null` field; the `updateEntry` PATCH shape is unchanged, but rejects a value above the aired-so-far count for a still-airing anime, not only one above the total.
