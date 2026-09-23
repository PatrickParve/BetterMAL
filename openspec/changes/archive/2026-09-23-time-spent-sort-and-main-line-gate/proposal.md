## Why

**"Most time spent" lists franchises I have never actually started.** A franchise is listed as soon as any member has watch time, and extras count. Watching one movie, OVA or spin-off is enough to list a franchise whose main series I have not seen a single episode of. The strip is meant to answer "which series have I spent the most time on", and a franchise I have only watched a side story of is not one of my series in that sense.

**The Series page cannot be ordered by time spent.** The app already works out each franchise's total watch time for the profile, but the Series page's sort control has no option for it. Finding where a given franchise sits means scrolling the profile strip.

## What Changes

**"Most time spent" requires the main series to have been watched**

- A franchise is listed only when at least one **main-line** member is in my list with at least one episode watched. Any watch status qualifies (Dropped, On-hold, Watching, Completed, Rewatching), so one episode of the main line is enough.
- A member marked **Rewatching** counts as watched, as it already does in the franchise total (its first viewing is one complete run).
- Any main-line member counts, **including a non-default alternative** in a version slot. Watching a different telling of the main story is still watching the main series.
- Only what gets listed changes. A listed franchise's total still covers every member, main line and extras alike, so a movie I watched still adds to the total once the franchise qualifies.
- A franchise whose only watched members are extras is now **omitted**. So is a franchise whose only watched member is a version neighbour, since a version neighbour is never main line.
- The empty-state message is reworded to fit the stricter rule.

**The Series page gains a "Time spent" sort**

- A new **Time spent** sort orders series by the same franchise total "Most time spent" ranks by: first viewings plus rewatches, summed over every member, valued at each member's episode duration. The highest total comes first.
- The sort key is the figure itself, not the profile's listing rule. Every series the Series page lists is ordered by its own total, including one the profile omits because only its extras have been watched. A series with nothing watched has a total of zero and sorts after every series with time spent. Ties break on display title, as every other sort except My average does.
- The series list endpoint adds one field per series, its total watched seconds. The client sorts on it locally with no new request, as it does for every other sort.
- The sort is added to the existing control and stored in the URL like the others. The default sort stays **My average**. Cards show nothing new.

No **BREAKING** changes. The series list response gains one field, and no existing field changes meaning. No stored state changes shape.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `profile-stats`: "Most time spent by series" changes its listing rule from "total above zero" to "at least one main-line member watched". Its empty state is reworded to match.
- `series-browser`: "Series sorting" gains a Time spent order. "Series list read endpoint" carries each series' total watch time.

## Impact

- **Backend**: `SeriesRankingIndex.TimeSpentSeries()` gains the main-line gate. `SeriesRankingIndex.ListedSeries()` computes the same per-series total through a shared helper, so the two surfaces cannot drift apart. `SeriesListItemDto` gains `WatchedSeconds`. Doc comments on `TimeSpentSeriesItemDto`/`TimeSpentSeriesSectionDto` and the `time-spent-series` controller action are updated.
- **Frontend**: `SeriesListItemDto` type gains `watchedSeconds`. `SeriesSortKey` and its comparator table gain `timeSpent`. The Series page's sort options gain "Time spent". The profile's "Most time spent" empty message is reworded.
- **Tests**: `ProfileServiceTimeSpentSeriesTests` gets new eligibility cases, and its existing extras-only case is rewritten. `SeriesListFiguresTests` gains cases for the new figure, including one that pins it equal to the profile total.
- No migration, no new endpoint, no new MyAnimeList calls.
