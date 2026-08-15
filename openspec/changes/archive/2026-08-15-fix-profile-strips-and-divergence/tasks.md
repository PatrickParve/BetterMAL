## 1. Poster strips fit ten tiles

- [x] 1.1 Add `box-sizing: border-box` to `.top-anime-strip__item`, `.top-series-strip__item`, and `.rewatched-strip__item` in `frontend/src/pages/ProfilePage.css`, with a comment on the first of them naming what it fixes (the 1px border pushing ten tiles 20px past the strip) and pointing at the other two, in the style of the existing `CurrentlyWatchingCarousel.css` note.
- [x] 1.2 Verify in the app at a few window widths: with exactly ten entries in a strip (a media-type filter that yields ten, or the multi-entry filter on Top series), all ten tiles are whole and the tenth's trailing edge sits inside the strip.
- [x] 1.3 Verify a strip with more than ten entries still starts with ten whole tiles, still drag-scrolls, and that hovering the first and last visible tile scales them without clipping against the strip's edge.

## 2. Latest updates shows five whole rows

- [x] 2.1 In `ProfilePage.css`, make `.activity-feed` a size container: drop `max-height: 340px`, add `container-type: size`, add `min-height: calc(5 * 60px + 42px)`, and declare `--activity-row-h: calc((100cqh - 4 * 8px - 5 * 2px) / 5)` with a comment deriving the 42px (four 8px gaps plus five rows' 1px top-and-bottom borders) and stating why rows stay `content-box`.
- [x] 2.2 Add feed-scoped row rules: `.activity-feed .profile-list-row` takes `flex: 0 0 auto`, `height: var(--activity-row-h)`, `min-height: 0`; `.activity-feed .profile-list-row__picture` takes `height: var(--activity-row-h)` and `width: calc(var(--activity-row-h) * 41 / 56)`, keeping the poster proportions the existing 41×56 rule established.
- [x] 2.3 Confirm the untouched rows are untouched: the divergence lists and the edit-history overlay still render 56px-content rows with 41×56 posters.
- [x] 2.4 Verify in the app: exactly five rows visible with no sliver of a sixth, at a narrow and a wide window (the fluid `h2` changes the top row's height between them), the feed still reaching the bottom of its box, and the sixth row reachable by scrolling.

## 3. Divergence rules on standardized scores

- [x] 3.1 In `backend/AnimeTracker.Api/Services/Profile/ProfileService.cs`, add named constants for the rule: divergence threshold `1.0` SD, my-dislike ceiling `5`, my-like floor `8`, MAL-like floor / MAL-dislike ceiling `7.5`, and minimum rated pairs `10` — each with a comment giving its derivation (MAL's own score labels for 5/8 and the neutral 6–7 band they leave between them, the "Good"/"Very Good" split for 7.5, and why a small population can't be normalized).
- [x] 3.2 Rewrite `BuildOpinionDivergence` to compute μ and population σ for my scores and for MAL scores over the rated pairs, then select each list by `divergence >= threshold` plus its label gate, ordering by divergence descending with a case-insensitive title tie-break. Return both lists empty when the population is under the minimum or either σ is zero.
- [x] 3.3 Add `backend/AnimeTracker.Api.Tests/Services/Profile/ProfileServiceOpinionDivergenceTests.cs` following the existing `ProfileServiceTopSeriesTests` construction (fake repositories, `GetProfileAsync`), covering: an anime lands in each list; a title that diverges enough but fails its label gate (my 6 against a high MAL average — pinning the neutral band's lower edge; my 10 against a 7.8 MAL average) is excluded from that list; ordering runs strongest-divergence first with the title tie-break; a population under ten pairs yields two empty lists; a zero-σ population yields two empty lists rather than dividing by zero.
- [x] 3.4 Run the backend test suite (per the memory note: the local SDK is 9.0, so compile in the `sdk:10.0` Docker image rather than with the local `dotnet`).
- [x] 3.5 Sanity-check the rule against the real database before and after: both lists populate (expect roughly 22 and 24), and the top entries of each match the examples in design.md decision 3.

## 4. Divergence lists cap at ten rows and scroll

- [x] 4.1 In `ProfilePage.css`, give `.divergence-list` `max-height: calc(10 * 58px + 9 * 8px)` with a comment showing the arithmetic (ten rows at their 58px outer height plus nine gaps) and why it is `max-height` rather than `height`.
- [x] 4.2 In `ProfilePage.tsx`, add the shared `scroll-y` class to the `DivergenceList` `<ul>` so its scrollbar sits beside the rows like the feed's and the history overlay's.
- [x] 4.3 Verify in the app: a full list shows ten whole rows with no sliver of an eleventh and scrolls to the rest with the scrollbar beside the rows; a short list renders only its rows without reserving the remaining height.

## 5. Close out

- [x] 5.1 Build the frontend (per the memory note: use nvm's Node v22, not the default v16) and confirm no CSS or type errors.
- [x] 5.2 Walk the profile page end to end against the delta spec's scenarios — strips, feed, both divergence lists — including a back-navigation to the page to confirm strip scroll restoration and the media-type/basis controls still behave.
