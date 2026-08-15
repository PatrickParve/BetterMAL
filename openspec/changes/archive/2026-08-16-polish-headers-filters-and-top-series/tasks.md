## 1. Anime detail page link-button sizing

- [x] 1.1 In `AnimeDetailPage.css`, set `align-self: center` on `.anime-detail-page__links` so it stops stretching to the info-grid row's height
- [x] 1.2 Verify `.anime-detail-page__related-link` no longer needs (or still correctly uses) its internal `align-items`/`justify-content: center` now that the outer container is content-sized
- [x] 1.3 Manually check an anime with a short genre list (one line) and one with a long genre list (wraps to two lines): confirm the three buttons are the same size in both, vertically centered against the genres block in the wrapped case

## 2. Search results page: Type filter

- [x] 2.1 In `SearchPage.tsx`, derive the set of media types present in the loaded candidate results (mirroring `MyListPage`'s `presentTypes`/`hasUnknownType` derivation) and build `FilterMultiSelectOption[]` from `MEDIA_TYPE_ORDER`/`mediaTypeLabel`
- [x] 2.2 Add `typeFilter` state (URL-backed via `useSearchParams`, consistent with `sort`) and render a `FilterMultiSelect label="Type"` control in `.search-page__controls`
- [x] 2.3 Filter the displayed/visible anime cards by `typeFilter` client-side over the already-loaded candidate array, leaving series cards unaffected
- [x] 2.4 Update the result count line to reflect the type-filtered anime count
- [x] 2.5 Confirm filter state survives back-navigation (URL param round-trip) and that clearing the filter restores all loaded results

## 3. Season page: Type filter

- [x] 3.1 Add an optional `type` query parameter to the backend `/api/season/{year}/{season}` endpoint, filtering server-side, defaulted to no filter when omitted
- [x] 3.2 Add the corresponding `type` param to `getSeasonPage` in `api/client.ts`
- [x] 3.3 In `SeasonPage.tsx`, derive present media types from the currently loaded season results and add `typeFilter` state (URL-backed via `useSearchParams`, alongside `sort`/`inMyList`)
- [x] 3.4 Render a `FilterMultiSelect label="Type"` control in `.season-page__controls` beside the sort dropdown and "In my list" checkbox
- [x] 3.5 Wire `typeFilter` into the reload effect (alongside `sort`, `inMyList`, `hideHentai`) so changing it re-fetches from the first page
- [x] 3.6 Confirm the filter stays correct across infinite scroll and survives back-navigation

## 4. Page header redesign

- [x] 4.1 Wrap `SettingsPage.tsx`'s `<h1>Settings</h1>` in a `.settings-page__header` element and add matching CSS that zeroes the `h1` margin, giving the wrapper its own spacing
- [x] 4.2 Wrap `ProfilePage.tsx`'s `<h1>Profile</h1>` in a `.profile-page__header` element with the same zero-margin treatment
- [x] 4.3 Give the Settings and Profile headers a distinct standalone treatment (e.g. icon and/or subtitle) so they read as a deliberate header block rather than plain text
- [x] 4.4 Tune the title styling in the five existing header-row pages (My List, Top Anime, Seasonal Anime, Schedule, Search) — `MyListPage.css`, `TopAnimePage.css`, `SeasonPage.css`, `AiringPage.css`, `SearchPage.css` — so the `h1` is sized/weighted to share its row with adjacent controls instead of using the full-bleed global heading size
- [x] 4.5 Check each of the seven pages at desktop and narrow widths to confirm no title collides with or crowds adjacent header-row content

## 5. Top Anime page: podium styling for ranks 1-3

- [x] 5.1 In `TopAnimePage.tsx`, add a rank-based modifier class (e.g. `top-anime-row--rank-1/2/3`) to rows where `item.rank <= 3`
- [x] 5.2 In `TopAnimePage.css`, style the modifier classes with a larger poster and row height than the default `.top-anime-row`, and a distinct gold/silver/bronze rank badge or accent replacing the plain `#1`/`#2`/`#3` text
- [x] 5.3 Confirm the podium styling renders only on page 1 (ranks 1-3) and that page 2+ rows use the unmodified row styling
- [x] 5.4 Check the podium rows against both light and dark colour tokens in `index.css` for contrast

## 6. Verification

- [x] 6.1 Run the frontend build/typecheck (see memory: use nvm's Node v22 for Vite builds, not the default v16) and fix any errors
- [x] 6.2 Manually exercise: anime detail page (short and long genre lists), Search page type filter, Season page type filter (including scrolling), all seven page headers, and the Top Anime page's top 3 vs. later ranks
