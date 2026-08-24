## Why

The Series page and the series detail page's personal badge only ever say "Completed", "Caught up", "N behind", or nothing at all. Two real situations read as silence today: a franchise I dropped partway through and never picked back up, and a franchise I have not started at all. Both currently show no badge, which reads as "nothing to report" when there is something to report. Separately, "N behind" only ever counts a *currently-airing* entry's unwatched broadcast episodes — a finished season I watched five of twelve episodes of and never marked Completed also shows no badge today, even though "7 behind" is exactly the information the badge exists to carry. The status pill and the personal badge also render at whatever width their own label happens to need, so a row of badges looks ragged rather than aligned. Finally, the Series page has no way to narrow the list to just what I'm behind on, or just what I dropped — every series is a wall of cards I scroll through by eye.

## What Changes

- The personal badge (series detail page header and Series page cards) gains two new states: **Dropped** (I dropped a main-line entry and never watched anything released after it) and **Unwatched** (something in the main line has aired, but I have watched none of it). Both replace what used to be silence.
- **N behind** is generalized: it is no longer limited to a currently-airing entry's own broadcast count. Once I have watched at least one main-line episode, the badge shows the shortfall between total broadcast main-line episodes and what I've watched, whether that shortfall sits on a still-airing season or a finished one I never finished watching.
- **Completed** changes colour to the app's existing "Completed" status colour (blue) rather than the accent purple it borrowed before. **Dropped** takes the existing "Dropped" status colour (red); **Unwatched** takes the existing "Plan to watch" status colour (purple).
- The status pill and the personal badge render at a consistent, uniform size regardless of label length, on both the series detail page and the Series page's cards.
- The Series page gains filter buttons: a progress filter (**Watched** — Completed or Caught up, **Behind**, **Dropped**, **Unwatched**) and a status filter (**Airing**, **Ongoing**, **Upcoming**, **Finished**), both multi-select — several buttons within one group OR together, the two groups AND together — applied to the whole loaded list with no re-fetch, alongside the existing sort.

## Capabilities

### Modified Capabilities
- `series-page`: the header's personal-badge precedence gains Dropped and Unwatched, generalizes the behind-count beyond currently-airing entries, and both the status pill and personal badge gain a uniform-size and updated-colour rule.
- `series-browser`: the card's progress badge follows the same widened precedence and colours; a new filtering requirement adds the progress/status filter buttons; the page-furniture requirement's description of the filter/sort cluster and back-navigation restoration is extended to cover the new filter selections.

## Impact

- **Backend**: `SeriesProgressBadge` gains `Dropped`/`Unwatched`; `SeriesRankingIndex`'s badge computation is rewritten to the new precedence, reusing the existing `Order` field already projected per member; `SeriesService.ComputeStatus`/`SeriesStatusRules` are untouched (status pill logic doesn't change, only its colours/sizing at the CSS layer).
- **Frontend**: `SeriesPage.tsx`'s client-side `completionBadge` is rewritten to the identical new precedence (design.md pairs both sides against the same case set, as the prior change did); `SeriesCompletionBadge`/`SeriesStatusPill` gain new states/sizing; `SeriesBrowserPage.tsx` gains two multi-select filter-button groups, both persisted in the URL like sort already is.
- **Not affected**: series composition, main-line classification, the status *pill's* own four-value precedence, sorting, the list endpoint's shape (badge value is still one field, now with two more possible strings), and score-reveal rules.
