## Why

Three things on the series page and the completion prompt read wrong to the user:

- The series header's year span covers every member, so an OVA that aired years before the main series, or a film years after it, stretches the span. The user wants the years of the main series only.
- The series header still crops a poster-shaped picture to its 140/198 box. `uncrop-artwork-everywhere` drew upright, square and landscape pictures whole there, but left posters cropped. Since TMDB became a second picture source, many chosen pictures are TMDB's 2:3 posters, which lose about 6% of their height in that box, often through a logo or title at the bottom. The user wants the whole picture shown, on the series page and on the detail page.
- The completion score prompt draws its picture as a small row thumbnail (96×136), and its three buttons don't fit on one line beside it. "Save and rank" wraps onto two lines and becomes a tall block. A landscape picture is worse: it can take up to 242px of the prompt's 372px width.

## What Changes

- **The series header's year span covers the main series only.** It runs from the year the first main-series entry began airing to the year the last one ended. Extras, related entries and everything else in More no longer count. Every main-series entry counts, every alternative version included, so switching the picked route does not change the years. A series with no dated main-series entry shows the existing no-year placeholder.
- **The Series browser card's year span follows the same rule**, so a card and the page it opens show the same years. The Newest and Oldest sorts order by that span's first year, so they now order by when the main series began.
- **The series header draws every picture whole.** A poster is now drawn like an upright or square picture: at the header's portrait width and at its own height, with nothing cut off. A landscape picture keeps its current treatment unchanged. It is shown wider, and the score averages and progress sit under the picture, not beside it.
- **The detail page keeps drawing its picture whole.** Its CSS already crops nothing. Its spec gains scenarios for a 2:3 TMDB poster and a narrow MAL poster, and the change verifies both on real pictures.
- **The completion prompt is restyled around a bigger picture.**
  - The prompt is wider, 640px instead of 420px.
  - The picture is on the left, drawn whole at its own proportions: 170px wide for a poster, upright or square picture, and 250px wide for a landscape one. It no longer uses the row-thumbnail rule, and it still fades in when it arrives slowly.
  - The title, hint and score dropdown sit beside the picture.
  - The buttons get a full-width row under both. Cancel is on the left. On the right are Save and rank, then Save. Each label stays on one line and all buttons are the same height. Save is always the rightmost button, so it doesn't move when Save and rank appears or disappears.
  - On a narrow screen the picture moves above the text.
- **The prompt's score dropdown uses the "mine" colour** once a score is picked, like the score controls in My list and the entry editor. It is neutral at "No score". It remains the same dropdown with the same options and behaviour.

**Deliberately unchanged:** the header's landscape layout and its strict "wider than tall" test. Timeline cards and More tiles, whose poster boxes still crop a poster slightly so every card in a row lines up. The detail page's layout. The prompt's actions, when Save and rank is offered, and what saving does. The series page's other figures.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `series-page`: the header's year span covers the main series only, with a new scenario for an extra outside it. The whole-picture requirement draws a poster whole in the header too. Timeline cards and More tiles are unchanged.
- `series-browser`: the card's year span, and so the Newest/Oldest sort key, covers the main series only, matching the page.
- `anime-detail`: the portrait-artwork requirement gains scenarios for a 2:3 poster and a narrow poster, both drawn whole. There is no behaviour change.
- `list-editing`: the completion prompt's picture is larger and whole, its buttons sit in their own row with one-line labels, and it stacks on a narrow screen.
- `artwork-presentation`: the completion prompt's picture is no longer a row picture slot. It joins the surfaces that draw whole under their own capability's rules.
- `score-presentation`: the completion prompt's score dropdown joins the score controls that carry the "mine" colour.

## Impact

**Backend:**
- `backend/AnimeTracker.Api/Services/Series/SeriesService.cs`: `YearSpan` is called with the main-line members' anime, not `allAnime`.
- `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs`: `ListedSeriesYearSpan` reads the main line.
- `backend/AnimeTracker.Api.Tests/Services/Series/SeriesListFiguresTests.cs`: `YearSpan_SpansEveryMemberIncludingExtras` is rewritten for the new rule, and tests are added for the series read and for the no-dated-main-line case.
- There is no DTO, API, schema or migration change. `firstYear`/`lastYear` keep their names and types, and only what they cover changes. Stored series need no rebuild, because both figures are computed on read.

**Frontend:**
- `frontend/src/pages/SeriesPage.tsx` / `.css`: the header picture drops `object-fit: cover` and the `--whole` distinction. It needs only a landscape test, like the detail page (`useLandscapePicture`).
- `frontend/src/components/CompletionScoreOverlay.tsx` / `.css`: a new layout. `RowPicture` is replaced by a whole-picture `<img>` in a fading frame, and the dropdown gets the "mine" class. The overlay passes `modal--wide`.
- There are no changes to `AnimeDetailPage`, `RowPicture`, `PosterPicture` or the shape hooks, and no new dependency.
