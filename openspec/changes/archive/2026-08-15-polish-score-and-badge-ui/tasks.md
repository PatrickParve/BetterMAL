## 1. Hidden MAL scores show the eye alone, in the score's slot

- [x] 1.1 In `frontend/src/components/ScoreValue.tsx`, delete the `score-value__blur` span so the hidden branch renders only the reveal button, and update the component's leading comment to describe the placeholder as the control alone (the value still never enters the DOM).
- [x] 1.2 In `frontend/src/components/ScoreValue.css`, give `.score-value` the shared slot — `display: inline-flex; justify-content: center; min-width: 4ch` alongside its existing tabular figures — so the shown value and the hidden control occupy the same box (design decision 1). Drop the now-unused `.score-value__blur` rule.
- [x] 1.3 In the same file, change `.score-value__reveal`'s `color: var(--text)` to `color: inherit` so the control carries whatever score colour surrounds it (design decision 2), keeping its existing opacity, hover, and focus-visible treatment.
- [x] 1.4 Delete `.score--mal .score-value__blur` from `frontend/src/index.css` and `.score-chip--mal .score-value__blur` from `frontend/src/components/ScoreChip.css`, and update the surrounding comments in both files, which explain the blur placeholder's colour handling.
- [x] 1.5 Grep the frontend for `score-value__blur` and for `••` to confirm no other rule, markup, or test references the removed element.
- [x] 1.6 Verify in the running app with the hide toggle on: My List, Top anime, anime detail, series page (entry rows, timeline cards, extra tiles, average chips), and profile (both divergence lists, top-series chips) each show a centred eye in a score-width slot, and revealing one shifts nothing around it.
- [x] 1.7 Verify the "always show completed scores" setting still shows completed scores in full with no control, and that a revealed score re-hides after navigating away and back.

## 2. Broadcast progress on currently-watching cards

- [x] 2.1 Add `bool CurrentlyAiring` to `CurrentlyWatchingItemDto` in `backend/AnimeTracker.Api/Services/Dashboard/MainDashboardDto.cs`.
- [x] 2.2 In `MainDashboardService.GetDashboardAsync`, populate it from `e.Anime.AiringStatus == "currently_airing"`, matching how the current-season loop derives `FinishedAiring`.
- [x] 2.3 Add `currentlyAiring: boolean` to `CurrentlyWatchingItemDto` in `frontend/src/api/types.ts`.
- [x] 2.4 In `frontend/src/components/CurrentlyWatchingCarousel.tsx`, pass `aired={item.currentlyAiring ? item.episodesAired : null}` to the card's `ProgressBar`, and note in the component comment why the fill is gated on airing status. Leave `max`, `onIncrement`, `onSetWatched`, and the countdown untouched.
- [x] 2.5 Build the backend against the .NET 10 SDK image and run the existing test suite to confirm the DTO change compiles and nothing that constructs `CurrentlyWatchingItemDto` was missed.
- [x] 2.6 Verify on the home page: an airing show's card shows the blue aired fill behind the purple watched fill on one bar of unchanged height; a finished show's card shows no blue fill; incrementing grows only the purple fill and leaves the row's scroll position where it was.

## 3. Anime detail score boxes sized to content

- [x] 3.1 In `frontend/src/pages/AnimeDetailPage.css`, change `.anime-detail-page__score-boxes .detail-box` from `flex: 1 1 0` to `flex: 0 0 auto` and reduce its padding so the box fits its few short lines (design decision 4).
- [x] 3.2 Add a scoped `.anime-detail-page__score-boxes .score-chip { min-width: 0 }` so the shared chip's 130px floor stops setting the box width, leaving the default `.score-chip` untouched for the series page.
- [x] 3.3 Verify both boxes read as a compact pair beside the title, that the info and synopsis boxes below keep their full width, that the entry-less case (no "my score" box) still looks right, and that the pair still stacks under 1024px.

## 4. Series badge stops adding height to search cards

- [x] 4.1 In `frontend/src/components/SeriesBadge.css`, scale `.series-badge__pill` down (smaller font, no vertical padding, tight line-height) and bring the count text alongside it down to match, so the badge's line box is no taller than `.anime-card__meta`'s (design decision 5).
- [x] 4.2 Verify on the search results page that a row mixing series and anime cards is no taller than a row of anime cards alone, and that the pill is still legible and still reads as a badge.
- [x] 4.3 Verify the type-ahead dropdown's series rows with the smaller badge — the row keeps its two-line layout and its thumbnail alignment.

## 5. Most-rewatched badge in white/silver

- [x] 5.1 In `frontend/src/pages/ProfilePage.css`, give `.rewatched-strip__count` a silver border and silver-white digits over its existing dark tint, mirroring `.top-anime-strip__score`'s structure without using a score role (design decision 6), and comment why this badge is not given a colour token.
- [x] 5.2 Verify on the profile page in both light and dark themes that the badge is legible over bright, pale, and busy posters, and that it reads as a count rather than as a score next to the purple top-anime badges.

## 6. Wrap-up

- [x] 6.1 Run the frontend build (Vite, under Node 22) and confirm it is clean.
- [x] 6.2 Re-read the five delta specs against the running app and confirm each scenario holds, then run `openspec validate polish-score-and-badge-ui --strict`.
