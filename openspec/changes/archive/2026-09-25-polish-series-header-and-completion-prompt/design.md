## Context

**Year span.** The series read (`SeriesService.ProjectAsync`) computes `firstYear`/`lastYear` with `YearSpan(allAnime)`. That covers every member of the series: the main line, extras and held version neighbours. The Series browser's list read (`SeriesRankingIndex.ListedSeries`) computes the same pair with `ListedSeriesYearSpan(members)`, a copy of that function. A comment on it says it exists so that "the card's year span matches the series page's". Both take the earliest `AiredFrom` year and the latest `AiredTo ?? AiredFrom` year, over members with a known `AiredFrom`. The frontend only formats the pair (`formatYearSpan`: `2013 – 2023`, `2019`, or `—` when null). The Series browser's Newest/Oldest sorts order by `firstYear`.

**Series header picture.** `SeriesPage.tsx` reads the header `<img>`'s ratio with `useOrientationPicture`. It derives two facts from it (`uncrop-artwork-everywhere` D7):

- A strictly landscape picture (`ratio > 1`) gets the landscape grid layout: a wider picture, with the score chips and progress in `.series-page__header-below` under it.
- An upright or square picture (`0.75 < ratio ≤ 1`) gets `.series-page__picture--whole`: the portrait width, at its own height.

Anything classified as a poster (`ratio ≤ 3/4`, `POSTER_MAX_RATIO`) keeps the base rule, `aspect-ratio: 140 / 198; object-fit: cover`. That base rule crops a 2:3 TMDB poster (0.667) by about 6% of its height, and a 0.74 picture by about 4.5% of its width.

**Detail page picture.** `.anime-detail-page__picture` is `width: 260px; height: auto; aspect-ratio: auto 260 / 368` with no `object-fit`, and a landscape picture widens its column. Every shape is therefore already drawn whole. When asked, the user confirmed they had no specific detail-page case in mind.

**Completion prompt.** `CompletionScoreOverlay` sits in the default `.modal`, 420px wide with 24px padding, so its content is 372px. It is a flex row: a `RowPicture` at `--row-picture-w: 96px; --row-picture-h: 136px`, then a body column holding the title, hint, score `<select>` and the buttons. That leaves the body about 260px. Cancel + Save + Save and rank need about 270px, so "Save and rank" wraps onto two lines inside its button. A wide picture follows the row rule, up to `136 × 16/9 ≈ 242px` across, which leaves the body about 114px.

*Correction found while applying:* `.modal` is `content-box`, so its `max-width` is the width of the *content*, not of the box. The default modal has 420px of content (470px outer, with 24px padding and a 1px border each side), and `modal--wide` has 640px of content (690px outer). The figures for the old layout above, and the ones in D5 and D7, were worked out as if those were the outer widths. D5 and D7 now carry the measured figures.

## Goals / Non-Goals

**Goals:**

- The header year span, on the page and on the card, covers the main line only, and is one figure computed by one function.
- The series header crops no picture. The landscape layout, with the scores under the picture, stays exactly as it is.
- The completion prompt shows a large, whole picture on the left, keeps each button label on one line, keeps Save in a stable position, and stacks on a phone.
- The prompt's score dropdown carries the "mine" colour, like the other score controls.

**Non-Goals:**

- Timeline cards and More tiles. Their poster boxes keep cropping a poster slightly, so the cards in a row line up (`uniform-poster-boxes-in-grids-and-strips`).
- The detail page's layout, its 12px corner radius, and the series header's radius.
- What the prompt does: its actions, labels, when Save and rank is offered, and the save paths.
- The picture shape classification (`POSTER_MAX_RATIO`, `pictureShapeOf`) and `RowPicture`/`PosterPicture`.
- A fade-in for the series and detail header pictures, which have none today.

## Decisions

### D1 — One `SeriesYearSpan` helper, over the whole main line

A new static helper, `Services/Series/SeriesYearSpan.cs`, holds the rule once:

```
SeriesYearSpan.Of(IEnumerable<(DateOnly? AiredFrom, DateOnly? AiredTo)> mainLine) → (int? First, int? Last)
```

The body is today's `YearSpan` unchanged: filter to a known `AiredFrom`, take the min `AiredFrom.Year`, and take the max `(AiredTo ?? AiredFrom).Year`. `SeriesService` passes `mainLineMembers.Select(m => (m.Anime.AiredFrom, m.Anime.AiredTo))`, and `SeriesRankingIndex` passes its `mainLine` projections. The two private copies are deleted. This follows `SeriesStatusRules.Compute` and `SeriesAverages`, which already hold rules shared by the page and the card.

"Main line" means every `IsMainLine` member, every alternative in a version slot included. `mainLineMembers` in `ProjectAsync` and `mainLine` in the index are already exactly that. The span is a franchise-level figure, like the score averages, so it does not follow the picked route. It is delivered once, not per pick in `statsByPick`, and switching the picker never moves it. The card's `mainLine` is also the whole main line, not the default route, so the card and the page agree.

*Alternative rejected:* computing the span on the client from `series.mainLine`. The browser card has no member list to compute from, so the rule would live in two languages. It would also leave `firstYear`/`lastYear` in the DTO meaning something different from what the header shows.

*Alternative rejected:* following the picked route, like the episode totals. The user described the span as "the main series years". A route switch that changes the header's years would read as a bug, and it would need a span per combination in `statsByPick`.

### D2 — No fallback to extras

When no main-line member has a known `AiredFrom`, the pair is `(null, null)` and the header and card show `—`, as they already do for a series with no dated member. Falling back to the extras' years would bring back the span this change removes, in exactly the kind of series where it misleads most: one whose main series has not been dated yet. The root is always main-line, so this only happens when even the root has no start date.

### D3 — The series header draws every non-landscape picture at its own height

The header drops its three-way treatment for the detail page's two-way one:

- `SeriesPage.tsx` uses `useLandscapePicture(pictureSrc)` in place of `useOrientationPicture` + `isLandscapeRatio` + `pictureShapeOf`. `isWholePicture` and the `--whole` class go.
- `.series-page__picture` becomes `width: clamp(140px, 20vw, 200px); height: auto; aspect-ratio: auto 140 / 198`, with no `object-fit`. For a loaded `<img>`, the `auto` keyword lets the image's own ratio win. Until it has one, `140 / 198` reserves today's box, so the header does not collapse and re-expand on load. This is the rule `--whole` already applied to upright and square pictures, and the detail page applies to every portrait picture. It is now the base rule.
- The placeholder `<div>` has no intrinsic ratio, so the same `aspect-ratio` gives it today's box. Its rule keeps only its border.
- `.series-page__picture--landscape`, `.series-page__header--landscape`, `.series-page__header-below` and the `≤900px` rules are unchanged. The strict `ratio > 1` test is unchanged too, so a square picture keeps the portrait layout with the scores beside it, as the user asked.

`useOrientationPicture` stays exported, because the completion prompt now uses it (D6). Its comment is updated to name that host in place of the series header.

*Alternative rejected:* drawing the header through `PosterPicture` with `whole`. That draws a picture whole *inside a fixed box*, letterboxed with `contain`. The header should instead take the picture's own height, as the detail page's does.

### D4 — The detail page gets no code change

The detail page's CSS already satisfies the requirement. The change adds spec scenarios, for a 2:3 poster and a 0.74 poster, and a verification task that measures the drawn `<img>` against the picture's natural ratio on real pictures of both kinds. No code change is planned. If verification finds a crop, it is fixed under this change.

### D5 — Completion prompt layout: a two-column grid in `modal--wide`

The overlay passes `className="modal--wide"` (an existing size: 640px of content, 690px outer). `.completion-score` becomes a grid:

```
grid-template-columns: var(--completion-picture-w) minmax(0, 1fr);
column-gap: 20px; row-gap: 20px;
```

- `--completion-picture-w` is `170px`, or `250px` under `.completion-score--landscape`. These are the only two widths (spec: never per picture). A 2:3 poster is drawn at 170×255, a MAL poster at about 170×240, and a 16:9 picture at 250×141. That leaves the text column 450px beside a portrait picture and 370px beside a landscape one.
- The body, with the title, hint, score field and error, sits in column 2, `align-self: start`. The title keeps 20px and becomes weight 600. The hint stays 14px, muted.
- The action row spans `1 / -1`. It is separated by `padding-top: 16px` and a `1px var(--border)` top rule, so the actions read as the prompt's footer rather than part of the text column.
- At `@media (max-width: 600px)` the grid becomes one column. The picture frame is `justify-self: center`, a landscape picture's width is capped at `min(250px, 100%)`, and the body follows, then the action row. Below 690px of viewport the modal is limited by the viewport, not by `max-width`: 24px of backdrop padding, 24px of modal padding and a 1px border on each side leave a viewport minus 98px of content. At 601px that is 503px, which leaves the text 233px beside a landscape picture. That is the narrowest width at which side by side still reads well, so it is the breakpoint. A phone at 390px has 292px of content. The three buttons need about 301px there (Cancel 83, Save and rank 134, Save 68, plus two 8px gaps), so on a phone Save moves whole onto a second line, flush right, under Save and rank. That is the wrap D7 allows. The buttons could take a smaller padding under 600px to fit on one line at 390px, but not at 360px, so the change keeps one padding.

*Alternative considered and not chosen by the user:* picture on top as a banner. It handles a wide picture more naturally, but the user chose the picture on the left. The landscape width in D5 is the answer to "a wide picture would still be small here".

### D6 — The prompt's picture: an `<img>` in a fading frame, not `RowPicture`

The prompt's picture takes a fixed width and its own height, which neither shared leaf does:

- `RowPicture` is height-bound. A bigger `--row-picture-h` would still cap a wide picture at 16/9 of that height, and would size a poster by height rather than width.
- `PosterPicture` fits the picture inside a box the host sizes, with letterbox and fill.

So the overlay draws it itself, the way the detail page does, and adds the fade the row slot used to give it:

- `useDisplayPicture(pictureUrl, 'tile')`: the widest drawn width is 250px, and `tile` (TMDB `w500`) is the tier for at most about 250px. The fallback to the original on error is kept.
- `useOrientationPicture(displaySrc)` supplies the ratio, and so `isLandscapeRatio`, and the `arrival`. One callback ref serves both, the reason that hook is exported.
- The markup is `<span class="completion-score__picture-frame completion-score__picture-frame--{arrival}"><img class="completion-score__picture"></span>`. The frame carries `var(--code-bg)`, the radius and `overflow: hidden`. The `<img>` is `width: 100%; height: auto; aspect-ratio: auto 170 / 240`. While `--pending`, the `<img>` is `opacity: 0`. While `--loaded`, it runs a 180ms `completion-score-picture-fade-in` keyframe, with none under `prefers-reduced-motion`. This copies `RowPicture.css`'s frame recipe: an `<img>` cannot fade over its own background. `PICTURE_FADE_DELAY_MS` is shared through the hook, so the timing matches every other surface.
- With no picture, a `<div class="completion-score__picture completion-score__picture--placeholder">` sits at the portrait width with `aspect-ratio: 170 / 240` and a border.

The overlay is keyed by `animeId` (`CompletionPromptContext`), so the hook state never carries over from one anime to the next.

### D7 — Actions: Cancel · Save and rank · Save, one line each

The DOM order becomes Cancel, then Save and rank when offered, then Save. The row is `display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px`, and Cancel has `margin-right: auto`. Every button gets `white-space: nowrap` and the shared `padding: 8px 16px`, so all are one height with one-line labels. `flex-wrap` means a too-narrow row moves a whole button down, and never wraps a label. Save is the solid primary, and it stays at the far end whether or not Save and rank is shown. So a score change to "No score" removes the button beside Save without moving Save under the pointer.

The colours and hovers carry over: Save is solid accent, Save and rank is outlined accent, and Cancel is outlined neutral. The resting colours of Save and Save and rank never actually rendered before this change, since `.completion-score__buttons button` (0,1,1) outranked the bare `.completion-score__save` (0,1,0), so all three rested as the same plain outlined button and only the hover rules, which were qualified with the button type, took effect. Both resting rules are now qualified the same way, so the look this decision describes is what shows. Both save buttons still read "Saving…" while a save is in flight, as today.

*Alternative rejected:* keeping Save before Save and rank with the row right-aligned. Save would then jump right by about 130px whenever the score went to "No score".

### D8 — The prompt's dropdown takes the "mine" class

The `<select>` gets `completion-score__select--mine` while `score > 0`, with `border-color: var(--mine-border); background: var(--mine-bg); color: var(--mine)`. This is the entry editor's `entry-editor__score-select--mine` recipe. The dropdown is neutral at "No score". It keeps its options, its pre-selection and native `<select>` behaviour. It spans the text column's full width.

## Risks / Trade-offs

- [The series header grows a little on load] A 2:3 poster is about 6% taller than the reserved 140/198 box, so the content beneath the header moves down a few pixels once the picture arrives. → The detail page already does this. The reserve keeps it to that small shift rather than a collapse, and a cached picture is measured before first paint.
- [A very tall header picture] With no maximum height, an unusually tall picture makes a tall header. → The spec requires the whole picture. Such art is rare, and the detail page has made the same trade.
- [The prompt resizes once for a landscape picture] The prompt reserves a portrait box, so a picture that loads as landscape widens its column from 170 to 250px and shortens it. → This happens once, as the picture appears. A MAL picture is usually already in the cache from the row that opened the prompt, so it is measured before first paint. A TMDB `tile` rendition may not be.
- [Newest/Oldest order changes] A series whose extra aired before its main series now sorts by the main series' start. → That is the rule the user chose (cards follow the page), and it is stated in the series-browser spec.
- [Stored series] Both figures are computed on read from stored members, so every series shows the new span on its next read, with no rebuild and no migration.

## Migration Plan

There is no data migration and no API shape change. It ships as one commit that touches the backend and the frontend. The user's Docker stack needs `docker compose up -d --build`, because the backend image changes. Rollback is reverting the commit and rebuilding.

## Open Questions

- Should timeline cards and More tiles also stop cropping posters? This change leaves them as they are (Non-Goals). Doing it would give up the aligned card rows that `uniform-poster-boxes-in-grids-and-strips` introduced, so it would be a separate change if wanted.
