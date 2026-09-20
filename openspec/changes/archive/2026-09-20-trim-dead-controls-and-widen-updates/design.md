## Context

Four independent polish items, three of them frontend-only and two of them
touching the backend. They share nothing but their size, so this design treats
each on its own terms; the only cross-cutting concern is the `Upcoming` removal,
which spans `SeriesStatusRules`, three frontend modules and three specs.

Current state, as read from the code:

- `CurrentlyWatchingCarousel.tsx:60-72` tracks only whether the track overflows,
  via a `ResizeObserver`; the arrows render on that one boolean and are otherwise
  always live. `scroll()` (`:88-101`) rounds to the nearest card boundary and steps
  one card, so at an end it computes a target the browser silently clamps.
- `SeriesPage.tsx:1293-1302` always renders the "In my list" control whenever
  `series.extras.length > 0`, regardless of whether any of those extras is mine.
  `myMediaTypes` (`:830-832`) already computes the "extras of mine" population that
  the new condition needs.
- `SeriesService.BuildStats` (`:604-702`) computes `studios` and `genres` from the
  `allAnime` parameter — every member, main line and extras alike — while every
  other figure in the same method reads `visibleMainLineAnime` or `extraAnime`.
  `allAnime` has no other use inside the method.
- `ProfileService` (`:20-32, 266-296`) reads a fixed 400-row raw window and collapses
  it down to at most `RecentActivityCount = 20` feed items. Nothing in the pipeline
  is time-bounded.
- `SeriesStatusRules.Compute` (`:11-26`) has five precedence steps; step 3 returns
  `Upcoming` when nothing has finished airing and something is unaired. Status is
  **computed on every read**, in `SeriesService.ComputeStatus` and again in
  `SeriesRankingIndex.ListedSeries`; it is never persisted on `Series` or
  `SeriesMember`.

## Goals / Non-Goals

**Goals:**

- A carousel arrow that cannot move the row reads as inert — no click effect, no
  hover treatment.
- The series page offers no control whose only possible outcome is an empty section,
  and its Studios/Genres stats describe the series rather than its neighbourhood.
- The profile's Latest updates feed covers a month of activity, without ever going
  empty on a quiet one.
- `Upcoming` is gone from the product: no pill, no filter button, no sort slot, no
  union member, no backend branch.

**Non-Goals:**

- Changing what enters or leaves the Currently watching row, its ordering, its
  5-card bound, or its edge-hover reserve.
- Changing the More section's grouping, media-type filter, expand/collapse control,
  or the hidden-count control.
- Changing which members the series page's *other* stats cover, or the favourites /
  most-rewatched lists, which deliberately span every member.
- Changing the full edit-history overlay, the navbar updates bell, or any other
  consumer of the activity log.
- Any persisted-data migration. Series status is derived on every read, so nothing
  stored carries `Upcoming`.

## Decisions

### D1 — The carousel tracks its scroll bounds, not just its overflow

The component grows from one boolean to three, all derived from the same three
measurements on the track (`scrollLeft`, `scrollWidth`, `clientWidth`) in a single
`updateBounds()`: `overflowing`, `atStart`, `atEnd`. That function is called from
the existing `ResizeObserver`, from a new `scroll` listener on the track, and on the
existing `items` dependency — so a resize, a smooth arrow scroll, a trackpad flick
and a data change all settle the arrows by the same path.

The end tests carry a 1px tolerance, matching the `+1` the existing overflow test
already uses: `atStart` is `scrollLeft <= 1`, `atEnd` is
`scrollLeft >= scrollWidth - clientWidth - 1`. Sub-pixel `scrollLeft` values at
fractional zoom levels are exactly why that tolerance exists.

*Alternative considered:* deciding on click, inside `scroll()` — the handler already
computes a target it could compare against the clamp. Rejected: it can only make the
click a no-op, which is what happens today. It cannot render a disabled state or
suppress the hover treatment, which is what was asked for.

*Alternative considered:* hiding the arrow at its end rather than disabling it.
Rejected: the row would shift horizontally as an arrow appears and disappears
mid-scroll, and the `main-dashboard` spec's existing rule is that the arrows are
shown exactly while scrolling is possible **for the row** — the pair still belongs
together.

The hover rule moves to `.carousel__arrow:enabled:hover`, so the disabled state is
expressed once in the selector rather than duplicated as an override. A disabled
arrow additionally takes `cursor: default` and reduced opacity, so its deactivation
is visible without a hover.

### D2 — "In my list" is withheld, and a stale filter is treated as off

The control renders only when at least one of `series.extras` carries an `entry`.
Withheld rather than disabled: the page's existing `disabled` precedent (the
media-type buttons, `SeriesPage.tsx:1324-1329`) is for a control that becomes
available again as soon as the filter is toggled off, whereas this one can never
become available while the page shows this series.

That creates one loose end. `mineOnly` is restorable state; a restored `true` with
no control on screen would leave the section filtered with nothing able to unfilter
it. So the filter's effect is taken from a derived value — `mineOnly` AND "some
extra is mine" — rather than from the stored flag directly. This is the same
graceful-staleness pattern the page already applies to a restored media type nothing
carries (`activeMediaTypes`, `:823`) and to a restored group key that no longer
exists: the stale value is ignored at read time rather than cleaned up on restore.

The population deciding this is `series.extras` — the same array the filter itself
narrows, which includes related entries shown in More alongside real extras. Using
the same population for the control's presence and for its effect is what makes
"the control is shown exactly when it can change something" true.

### D3 — Studios and Genres move to the visible main line, and `allAnime` leaves `BuildStats`

`studios` and `genres` read `visibleMainLineAnime` instead of `allAnime`. This is the
member basis the episode total, runtime, aired figure, watched figures and
entries-completed already use, so a version-slot pick moves the whole stats box
together instead of leaving two rows pinned to a different population.

`allAnime` has no other reader inside `BuildStats`, so it is removed from the
signature rather than left as an unused parameter. Both call sites
(`SeriesService.cs:173` and `:177`) drop the argument. The `SeriesStatsDto` doc
comment that currently reads "The highest-scored/most-rewatched/favourite/studios/
genres figures span every member" is corrected: the first three still do, studios and
genres no longer.

*Alternative considered:* the whole main line (`mainLineMembers`) rather than the
visible one, which would hold the lists steady across slot picks. Rejected on the
user's call — consistency with the neighbouring figures won over stability across
picks, and a slot's alternatives are usually the same studio anyway.

The `series-page` spec's "Figures no edit can change ... MAY continue to be taken
from the figures delivered for the picked route" (`spec.md:991`) already names
studios and genres as per-route figures, so `statsByPick` needs no change — it
already recomputes per combination.

### D4 — One walk decides both the 30-day window and the 20-item floor

`BuildActivityFeed` gains two parameters, a `cutoff` instant and a `floor` count, and
its termination test becomes: stop once the log being considered is older than
`cutoff` **and** the feed already holds `floor` items. Everything else about the walk
— the completion/score merge, the non-increase filter, the field-group selection, the
one-row-per-(anime, group) collapse — is untouched, which is what "the exact same
rules, only more shown" requires.

Expressing both rules in one termination test rather than as a window filter plus a
separate top-up means the two can never disagree about ordering, and the quiet-month
result is a strict continuation of the recent one rather than a separate list
stitched on.

Feeding it needs a read that is no longer a fixed count:

1. A new `IActivityLogRepository.GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows)`
   returns every log newer than the cutoff, in the same
   `Timestamp desc, Id desc` order every other read uses — that ordering is what lets
   the completion/score merge work, so it is not optional.
2. `GetProfileAsync` calls it with `DateTimeOffset.UtcNow.AddDays(-30)`, matching the
   direct `UtcNow` reads already in the method (`:61`, `:115`) — there is no clock
   abstraction in this codebase and this change is not the place to introduce one.
   Testability comes from `BuildActivityFeed` taking the cutoff as a parameter.
3. If the resulting feed holds fewer than 20 items, the existing
   `GetRecentAsync(RecentActivityFetchWindow)` read runs and the same
   `BuildActivityFeed(window, cutoff, floor)` call is made over that larger window.
   The floor arm therefore exercises exactly today's query and today's raw window;
   the second read only happens on a month quiet enough for it to be cheap.

`maxRows` is a safety valve, not a product rule: a pathological month cannot turn one
profile read into an unbounded `Include`-bearing query. It is set well above any
plausible month (5000) and is expected never to bind; the spec describes the 30-day
window, not the cap.

*Trade-off accepted:* a completion falling just outside the cutoff whose score row
falls just inside will show as a standalone score row rather than being merged away.
This is the same boundary artefact today's 400-row cap already produces at its own
edge, and it is invisible at any realistic activity level.

The frontend needs no change: `ProfilePage.tsx:504` maps `profile.recentActivity`
whole, with no slice, and the box's "five whole rows at rest" rule is CSS-driven and
count-independent.

### D5 — `Upcoming` folds into `Ongoing`, which already means "something still to come"

Removing precedence step 3 leaves `anyFinished` with no reader, so `Compute` reduces
to its final line: `anyUpcoming ? "Ongoing" : "Finished"`. A franchise where nothing
has aired lands on `Ongoing`, which is already the value for every other franchise
with an announced, unaired member — the merge is semantically exact, not a
best-available substitute.

The frontend edits are mechanical and each one is load-bearing: the `SeriesStatus`
union (`api/types.ts:820`), the pill's class map and its `--upcoming` CSS rule, the
filter option list and its value map, and `SERIES_STATUS_ORDER` (`utils/anime.ts:433`),
whose remaining values renumber to `Airing: 0, Ongoing: 1, Finished: 2`.

No migration is needed in either direction. Status is derived on every read from
`AiringStatus` strings and is never stored, so no row carries `Upcoming` to be
rewritten. On the client, a shared or bookmarked URL carrying `?status=upcoming`
degrades on its own: `parseListParam` (`SeriesBrowserPage.tsx:44-48`) filters against
the known option values and silently drops anything else, so such a link now reads as
"no status filter" rather than erroring.

One note recorded because it is easy to misread as a regression: a series every one
of whose members has a `null` `AiringStatus` — metadata never fetched — satisfies
neither `anyFinished` nor `anyUpcoming` and falls through to `Finished`. That is
today's behaviour and this change does not alter it.

## Risks / Trade-offs

**[A 30-day feed is unbounded in principle]** → `GetSinceAsync` takes a `maxRows`
safety cap (5000) so one profile read can never become an unbounded query, and the
rows are the same `Include(l => l.Anime)` shape the code already reads 400 of.

**[The second activity-log read in the floor case]** → It only fires when 30 days
produced fewer than 20 feed items, which is by definition a near-empty window; the
common case stays at one read.

**[A restored `mineOnly` with no control to clear it]** → The filter's effect is
derived rather than read straight from the stored flag (D2), so a stale `true` is
inert rather than trapping the section in a filtered state.

**[`atEnd` mis-reading mid-animation]** → The `scroll` listener fires throughout a
smooth scroll, so the arrows settle as the animation settles rather than at its
start; the 1px tolerance keeps a sub-pixel resting position from reading as
"not quite at the end".

**[Folding `Upcoming` into `Ongoing` loses a distinction]** → It is a distinction the
user reports never having seen, reachable only for a franchise with no aired member
at all, and `Ongoing` already carries the same meaning ("something still to come").
Reversing it later is a one-branch change in `SeriesStatusRules` plus the frontend
constants, since nothing is persisted.

## Migration Plan

Deploy as one unit: the `SeriesStatus` union change is a wire contract between
`SeriesStatusRules` and the frontend, so a backend still returning `Upcoming` against
a frontend that dropped the class map would render a pill with no variant class. No
data migration, no backfill, no feature flag. Rollback is a plain revert.

## Open Questions

None. The three that existed — how far to take the `Upcoming` removal, what a quiet
month should show in Latest updates, and which main line Studios/Genres should follow
— were settled with the user before this design was written (filter button *and*
status value both removed; 30 days with a 20-item floor; the visible main line).
