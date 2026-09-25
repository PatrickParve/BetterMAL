## 1. Year span over the main line (backend)

- [x] 1.1 Add `backend/AnimeTracker.Api/Services/Series/SeriesYearSpan.cs` (design D1). `SeriesYearSpan.Of(IEnumerable<(DateOnly? AiredFrom, DateOnly? AiredTo)> mainLine)` returns `(int? First, int? Last)`. The body is today's `YearSpan`: filter to a known `AiredFrom`, return `(null, null)` when none is left, take the first year from the min `AiredFrom.Year`, and take the last from the max `(AiredTo ?? AiredFrom).Year`. The comment says it covers the whole main line, alternatives included, with no fallback to extras (D2), and that the page and the card share it.
- [x] 1.2 `SeriesService.ProjectAsync`: replace `YearSpan(allAnime)` with `SeriesYearSpan.Of(mainLineMembers.Select(m => (m.Anime.AiredFrom, m.Anime.AiredTo)))`. Delete the private `YearSpan` and move its comment to the helper.
- [x] 1.3 `SeriesRankingIndex.ListedSeries`: replace `ListedSeriesYearSpan(members)` with `SeriesYearSpan.Of(mainLine.Select(m => (m.AiredFrom, m.AiredTo)))`. Delete `ListedSeriesYearSpan`. Confirm `SeriesRankingMemberProjection` carries `AiredTo` (it is read today), and add it if it does not.
- [x] 1.4 Add `backend/AnimeTracker.Api.Tests/Services/Series/SeriesYearSpanTests.cs` with cases for:
  - a single entry;
  - several entries, with the first year from the earliest `AiredFrom` and the last from the latest `AiredTo`;
  - an entry with no `AiredTo`, which ends the span at its `AiredFrom` year;
  - an undated entry, which is ignored;
  - no dated entry, which gives `(null, null)`;
  - an empty input.
- [x] 1.5 `SeriesListFiguresTests`: rewrite `YearSpan_SpansEveryMemberIncludingExtras` as `YearSpan_CoversTheMainLineOnly`. The 2022–2023 extra no longer stretches the span, which stays `2013`/`2013`. Add these tests:
  - an extra that aired *before* the main line doesn't lower the first year;
  - two main-line alternatives in different years both count;
  - a series whose only dated member is an extra gives `null`/`null`.

  Keep `YearSpan_SingleYearWhenEveryEntryAiredInOneYear`.
- [x] 1.6 Add a series-read test (beside `SeriesServiceRelatedEntriesTests`, using its harness): a series with a main line from 2013 to 2019 and an extra from 2023 returns `FirstYear = 2013`, `LastYear = 2019` from `GetSeriesAsync`. This pins the page and the card to one rule.
- [x] 1.7 Run `dotnet test` from `backend/`. The whole suite passes. Note the new total against the last recorded 1919.

## 2. Series header draws every picture whole (frontend)

- [x] 2.1 `SeriesPage.tsx` (design D3): replace `useOrientationPicture` + `isLandscapeRatio` + `pictureShapeOf` with `const [pictureRef, isLandscapePicture] = useLandscapePicture(pictureSrc)`. Remove `isWholePicture` and the `series-page__picture--whole` branch from the `<img>` className, and remove the now-unused imports. Rewrite the comment above it: every non-landscape picture is drawn at its own height, and strict landscape keeps the grid layout.
- [x] 2.2 `SeriesPage.css`:
  - `.series-page__picture` becomes `width: clamp(140px, 20vw, 200px); height: auto; aspect-ratio: auto 140 / 198`, with no `object-fit`.
  - Delete the `.series-page__picture--whole` rule and fold its comment into the base rule's: the `auto` keyword lets a loaded image's ratio win, and 140/198 holds the box until then.
  - Check that the placeholder `<div>` still gets today's box from `aspect-ratio` alone.
  - Leave every `--landscape`, `header-below` and `≤900px` rule untouched.
- [x] 2.3 `hooks/useLandscapePicture.ts`: update `useOrientationPicture`'s "Exported for a host that needs…" comment to name the completion prompt (landscape + arrival from one `<img>`) in place of the series header.

## 3. Completion prompt restyle (frontend)

- [x] 3.1 `CompletionScoreOverlay.tsx` picture (design D6):
  - Drop `RowPicture`. Use `useDisplayPicture(pictureUrl, 'tile')` and `useOrientationPicture(displaySrc)`, and derive `isLandscape = isLandscapeRatio(ratio)`.
  - Render `<span className={`completion-score__picture-frame completion-score__picture-frame--${arrival}`}><img ref className="completion-score__picture" src={displaySrc} alt="" onError={onError} /></span>`.
  - With no picture, render the `completion-score__picture completion-score__picture--placeholder` `<div aria-hidden>`.
  - Put `completion-score--landscape` on the root when `isLandscape`.
  - Update the component comment: the picture is drawn whole under list-editing's prompt rule, and is no longer a row slot.
- [x] 3.2 `CompletionScoreOverlay.tsx` layout and controls (design D5, D7, D8):
  - Pass `className="modal--wide"` to `Modal`.
  - Move the buttons out of `.completion-score__body` into a sibling `.completion-score__buttons` in the order Cancel, Save and rank (when `canRank`), Save.
  - Give the `<select>` `completion-score__select` plus `completion-score__select--mine` while `score > 0`.
  - Leave the handlers, labels, `canRank` and the error line unchanged.
- [x] 3.3 `CompletionScoreOverlay.css`, rewritten to match D5 through D8:
  - The grid, with `--completion-picture-w: 170px` and `250px` under `--landscape`, and `column-gap`/`row-gap` of 20px.
  - The frame, with `var(--code-bg)`, a 10px radius, `overflow: hidden` and `align-self: start`.
  - The `<img>`, with `width: 100%; height: auto; aspect-ratio: auto 170 / 240`.
  - The placeholder, with the same box and a border.
  - The fade: `--pending` sets `opacity: 0`, `--loaded` runs a 180ms `completion-score-picture-fade-in` keyframe, and `prefers-reduced-motion` sets `animation: none`.
  - The body, with `align-self: start`, the title at 20px/600 and the hint muted.
  - The select, full width, with the `--mine` variant taking `--mine-border`/`--mine-bg`/`--mine`.
  - The action row, spanning `1 / -1`, with `padding-top: 16px`, a `1px var(--border)` top rule, flex with `wrap` and `justify-content: flex-end`, and Cancel at `margin-right: auto`.
  - Every button `white-space: nowrap`, with the existing padding, colours and hover rules kept. The resting Save and Save and rank rules are qualified with the button type (`button.completion-score__save`), like their hover rules, so they outrank `.completion-score__buttons button`. They didn't before.
  - `@media (max-width: 600px)`: one column, the frame centred, and a landscape width capped at `min(250px, 100%)`.
- [x] 3.4 Check that nothing else imports `.completion-score__*` classes and that `RowPicture` still has other callers. It does (rows across the app), so it stays.

## 4. Build and checks

- [x] 4.1 `npm run build` in `frontend/` with nvm's Node 22: no type errors. Note the bundle size against the last recorded 500.9 kB.
- [x] 4.2 `npm run lint` in `frontend/`: no new warnings beyond the pre-existing 28.
- [x] 4.3 Update `CODE_GUIDE.md` (local only, gitignored):
  - the series header's picture rule: every non-landscape picture at its own height;
  - the completion prompt's picture and layout;
  - `SeriesYearSpan` as the one home of the year-span rule.

## 5. Verification in a real browser

Use the headless Chrome recipe against a scratch backend (never the user's stack). Serve synthetic images at exact ratios: 2:3 (TMDB poster), 0.64, 0.708 (MAL), 0.74, 4:5, 1:1, 16:9 and 21:9.

- [x] 5.1 Series header, at 1440px and 390px wide:
  - For each ratio, the drawn `<img>`'s `clientWidth / clientHeight` equals the image's natural ratio within 1%, and `object-fit` computes to `fill`, so nothing is cropped.
  - A non-landscape picture is at the portrait width.
  - 16:9 and 21:9 get the landscape layout, with `.series-page__header-below` present and the score chips under the picture.
  - 1:1 keeps the scores beside the picture.
  - Take a screenshot for each and read it back.
- [x] 5.2 Series header while loading: hold an image request. The header reserves the 140/198 box, and after release it takes the picture's own height. No placeholder regression with no picture.
- [x] 5.3 Year span: seed a series with a main line from 2013 to 2019, a 2023 film extra and a 2011 pilot extra. The header and its Series browser card both read `2013 – 2019`. Sort by Newest and Oldest and check the series sits by 2013. Switch a two-version slot and check the span does not change.
- [x] 5.4 Detail page (design D4): the same ratio set at 1440px and 390px. The drawn ratio equals the natural ratio within 1% for every shape, 2:3 and 0.74 in particular. The layout beside the picture is unchanged. If any crop shows up, fix it here and record what it was.
- [x] 5.5 Completion prompt, opened from a My list row's "+" on a final episode, at 1440px and 390px:
  - A poster, a 1:1 and a 16:9 picture are drawn whole at 170px, 170px and 250px wide.
  - The title, hint and select sit beside the picture on desktop and stack beneath it at 390px.
  - Every button's `getClientRects().length` of its label text is one line, and all buttons share one height.
  - Switching the select from 8 to "No score" removes Save and rank, and Save's `getBoundingClientRect().right` is unchanged.
  - The select carries `--mine` at 8 and is neutral at "No score".
  - With no picture, the placeholder box shows.
  - A held slow image fades in, while a cached one shows at once.
  - Take screenshots and read them back.
- [x] 5.6 Regression checks on the prompt: Save, Save and rank (the ranking editor opens on the score), Cancel, Esc and a click outside behave exactly as before, and a failed save still shows the error and keeps the prompt open.
- [x] 5.7 Tear down the scratch stack and confirm the user's Docker stack is still up.
- [x] 5.8 Ask the user to rebuild (`docker compose up -d --build`) and check real pictures: a series with a TMDB poster header, a series with an extra outside its main-series years, and a completion prompt with a wide picture.
